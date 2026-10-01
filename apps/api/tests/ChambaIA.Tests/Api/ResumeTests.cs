using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ChambaIA.Infrastructure.Resumes;
using ChambaIA.Tests.Domain;

namespace ChambaIA.Tests.Api;

[Collection(ApiCollection.Name)]
public class ResumeTests(ApiFactory factory)
{
    private const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static MultipartFormDataContent Form(byte[] bytes, string filename, string mime)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(mime);
        return new MultipartFormDataContent { { file, "file", filename } };
    }

    private int StoredFiles() => Directory.Exists(factory.ResumesDir) ? Directory.GetFiles(factory.ResumesDir).Length : 0;

    [Fact]
    public async Task Uploading_a_docx_returns_the_detected_profile_without_touching_it()
    {
        var (client, _) = await factory.NewUserAsync();

        var response = await client.PostAsync("/api/v1/resumes", Form(CvFiles.Docx(ResumeParserTests.SampleCv), "Mi CV (final).docx", DocxMime));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var detected = body.GetProperty("detected");
        Assert.Equal(2, detected.GetProperty("experience").GetArrayLength());
        Assert.Equal(55, detected.GetProperty("experienceMonths").GetInt32());
        Assert.Equal("university", detected.GetProperty("educationLevel").GetString());
        Assert.Contains(detected.GetProperty("skills").EnumerateArray(), s => s.GetProperty("key").GetString() == "facturacion");
        Assert.Contains("Asistente Administrativo", detected.GetProperty("suggestedRoles").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal("Mi CV (final).docx", body.GetProperty("resume").GetProperty("originalFilename").GetString());

        // Detection is a proposal: the profile stays empty until the user saves their reviewed version.
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/v1/profile");
        Assert.Equal(0, profile.GetProperty("experienceMonths").GetInt32());
        Assert.Equal(0, profile.GetProperty("skills").GetArrayLength());
    }

    [Fact]
    public async Task Uploading_a_real_pdf_extracts_text_and_structure()
    {
        var (client, _) = await factory.NewUserAsync();

        var response = await client.PostAsync("/api/v1/resumes", Form(CvFiles.Pdf(ResumeParserTests.SampleCv), "cv.pdf", "application/pdf"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var detected = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detected");
        Assert.Equal(2, detected.GetProperty("experience").GetArrayLength());
        Assert.True(detected.GetProperty("skills").GetArrayLength() >= 6);
        Assert.Equal(55, detected.GetProperty("experienceMonths").GetInt32());
    }

    [Fact]
    public async Task The_file_is_stored_under_a_random_name_and_deleting_the_cv_erases_it()
    {
        var (client, _) = await factory.NewUserAsync();
        var before = StoredFiles();

        var created = await client.PostAsync("/api/v1/resumes", Form(CvFiles.Docx(ResumeParserTests.SampleCv), "../../etc/passwd.docx", DocxMime));
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("resume").GetProperty("id").GetGuid();

        Assert.Equal(before + 1, StoredFiles());
        Assert.DoesNotContain("..", body.GetProperty("resume").GetProperty("originalFilename").GetString());
        Assert.All(Directory.GetFiles(factory.ResumesDir), f => Assert.Matches(@"^[0-9a-f]{32}\.(pdf|docx)$", Path.GetFileName(f)));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/resumes/{id}")).StatusCode);
        Assert.Equal(before, StoredFiles());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/resumes/{id}")).StatusCode);
    }

    [Fact]
    public async Task A_single_cv_plan_replaces_the_previous_one()
    {
        var (client, _) = await factory.NewUserAsync();

        await client.PostAsync("/api/v1/resumes", Form(CvFiles.Docx(ResumeParserTests.SampleCv), "primero.docx", DocxMime));
        var filesAfterFirst = StoredFiles();
        await client.PostAsync("/api/v1/resumes", Form(CvFiles.Docx(ResumeParserTests.SampleCv), "segundo.docx", DocxMime));

        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/resumes");
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("segundo.docx", list[0].GetProperty("originalFilename").GetString());
        Assert.Equal(filesAfterFirst, StoredFiles()); // the old file is gone from disk, not just from the table
    }

    public static IEnumerable<object[]> InvalidUploads()
    {
        yield return ["virus.exe", "application/octet-stream", new byte[] { 0x4D, 0x5A, 1, 2, 3 }, "PDF o DOCX"];
        yield return ["falso.pdf", "application/pdf", "esto no es un pdf, solo texto plano"u8.ToArray(), "PDF válido"];
        yield return ["vacio.pdf", "application/pdf", Array.Empty<byte>(), "vacío"];
        yield return ["cv.docx", "application/pdf", CvFiles.Docx("x"), "no coincide"];
        yield return ["zip.docx", DocxMime, CvFiles.ZipWithoutWordDocument(), "Word"];
        yield return ["gigante.pdf", "application/pdf", new byte[ResumeFileValidator.MaxBytes + 1], "5 MB"];
    }

    [Theory]
    [MemberData(nameof(InvalidUploads))]
    public async Task Invalid_files_are_rejected_with_a_clear_message(string name, string mime, byte[] bytes, string messagePart)
    {
        var (client, _) = await factory.NewUserAsync();

        var response = await client.PostAsync("/api/v1/resumes", Form(bytes, name, mime));

        Assert.True(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.RequestEntityTooLarge, $"got {response.StatusCode}");
        Assert.Contains(messagePart, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/v1/resumes")).GetArrayLength());
    }

    [Fact]
    public async Task A_pdf_without_text_asks_for_the_original_file()
    {
        var (client, _) = await factory.NewUserAsync();

        var response = await client.PostAsync("/api/v1/resumes", Form(CvFiles.BlankPdf(), "escaneo.pdf", "application/pdf"));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("escaneo", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Cvs_are_private_to_their_owner()
    {
        var (owner, _) = await factory.NewUserAsync();
        var created = await owner.PostAsync("/api/v1/resumes", Form(CvFiles.Docx(ResumeParserTests.SampleCv), "cv.docx", DocxMime));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resume").GetProperty("id").GetGuid();

        var (stranger, _) = await factory.NewUserAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/v1/resumes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/v1/resumes/{id}")).StatusCode);
        Assert.Equal(0, (await stranger.GetFromJsonAsync<JsonElement>("/api/v1/resumes")).GetArrayLength());
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync("/api/v1/resumes")).StatusCode);
    }

    [Fact]
    public async Task Deleting_the_account_also_erases_the_stored_cv_files()
    {
        var (client, auth) = await factory.NewUserAsync();
        var before = StoredFiles();
        await client.PostAsync("/api/v1/resumes", Form(CvFiles.Docx(ResumeParserTests.SampleCv), "cv.docx", DocxMime));
        Assert.Equal(before + 1, StoredFiles());

        var deleted = await client.PostAsJsonAsync("/api/v1/account/delete", new { password = "Segura12345" });

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(before, StoredFiles());
        Assert.NotNull(auth);
    }

    [Fact]
    public async Task Onboarding_is_pending_for_new_users_and_completes_with_the_reviewed_profile()
    {
        var (client, _) = await factory.NewUserAsync();
        var before = await client.GetFromJsonAsync<JsonElement>("/api/v1/profile");
        Assert.False(before.GetProperty("onboardingCompleted").GetBoolean());

        var saved = await client.PutAsJsonAsync("/api/v1/profile", new { fullName = "Lucía", experienceMonths = 24, completeOnboarding = true });

        Assert.True((await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("onboardingCompleted").GetBoolean());
        // Saving again without the flag never un-completes it.
        var again = await client.PutAsJsonAsync("/api/v1/profile", new { fullName = "Lucía", experienceMonths = 25 });
        Assert.True((await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("onboardingCompleted").GetBoolean());

        var demo = await factory.DemoClientAsync();
        Assert.True((await demo.GetFromJsonAsync<JsonElement>("/api/v1/profile")).GetProperty("onboardingCompleted").GetBoolean());
    }

    [Fact]
    public async Task Clearing_history_forgets_reactions_but_keeps_the_feed()
    {
        var (client, _) = await factory.NewUserAsync();
        await client.PutAsJsonAsync("/api/v1/profile", new { fullName = "Lucía", experienceMonths = 30, skills = new[] { new { key = "", name = "Excel", level = "basic" } } });
        await client.PutAsJsonAsync("/api/v1/preferences", new { minSalary = 1500 });
        var feed = await client.GetFromJsonAsync<JsonElement>("/api/v1/matches?pageSize=5");
        var jobId = feed.GetProperty("items")[0].GetProperty("job").GetProperty("id").GetGuid();
        await client.PostAsync($"/api/v1/matches/{jobId}/interested", null);
        Assert.Equal(1, (await client.GetFromJsonAsync<JsonElement>("/api/v1/applications")).GetArrayLength());

        var cleared = await client.PostAsync("/api/v1/account/clear-history", null);

        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>("/api/v1/applications")).GetArrayLength());
        var saved = await client.GetFromJsonAsync<JsonElement>("/api/v1/matches?tab=saved");
        Assert.Equal(0, saved.GetProperty("total").GetInt32());
        Assert.True((await client.GetFromJsonAsync<JsonElement>("/api/v1/matches?pageSize=5")).GetProperty("total").GetInt32() > 0);
    }
}

public class ResumeFileValidatorTests
{
    [Theory]
    [InlineData("../../etc/passwd.pdf", ".pdf", "passwd.pdf")]
    [InlineData("C:\\Users\\x\\Mi CV?.docx", ".docx", "Mi CV.docx")]
    [InlineData("   .pdf", ".pdf", "cv.pdf")]
    public void Display_names_are_sanitised(string input, string extension, string expected) =>
        Assert.Equal(expected, ResumeFileValidator.SafeDisplayName(input, extension));
}
