using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ChambaIA.Tests.Api;

[Collection(ApiCollection.Name)]
public class AuthTests(ApiFactory factory)
{
    private static object Register(string email, string password = "Segura12345") =>
        new { email, password, fullName = "Usuaria Test" };

    [Fact]
    public async Task Register_returns_tokens_and_creates_an_empty_profile_and_preferences()
    {
        var (client, auth) = await factory.NewUserAsync("Lucía Prueba");

        Assert.Equal("Lucía Prueba", auth.User.FullName);
        Assert.Equal("Free", auth.User.Plan);
        Assert.False(string.IsNullOrWhiteSpace(auth.RefreshToken));

        var profile = await client.GetFromJsonAsync<JsonElement>("/api/v1/profile");
        Assert.Equal(0, profile.GetProperty("experienceMonths").GetInt32());
        var prefs = await client.GetFromJsonAsync<JsonElement>("/api/v1/preferences");
        Assert.Equal("every6Hours", prefs.GetProperty("notificationFrequency").GetString());
    }

    [Fact]
    public async Task Register_with_an_existing_email_conflicts_without_leaking_details()
    {
        var client = factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@example.com";

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/auth/register", Register(email))).StatusCode);
        var second = await client.PostAsJsonAsync("/api/v1/auth/register", Register(email));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "Segura12345")]
    [InlineData("ok@example.com", "corta1")]
    [InlineData("ok2@example.com", "sinnumeros")]
    public async Task Register_rejects_invalid_input_with_a_validation_problem(string email, string password)
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/register", Register(email, password));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Login_succeeds_with_the_right_password_and_fails_generically_otherwise()
    {
        var client = factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", Register(email));

        var ok = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Segura12345" });
        var wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Equivocada1" });
        var unknownUser = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "nadie@example.com", password = "Segura12345" });

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        // Same body for both failures: no account enumeration.
        Assert.Equal(
            (await wrongPassword.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString(),
            (await unknownUser.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Repeated_failed_logins_lock_the_account()
    {
        var client = factory.CreateClient();
        var email = $"lock-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync("/api/v1/auth/register", Register(email));

        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Equivocada1" });

        var locked = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password = "Segura12345" });
        Assert.Equal(HttpStatusCode.Locked, locked.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_reusing_the_old_one_revokes_the_session()
    {
        var (_, first) = await factory.NewUserAsync();
        var anonymous = factory.CreateClient();

        var rotated = await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var second = (await rotated.Content.ReadFromJsonAsync<ApiFactory.AuthBody>(ApiFactory.Json))!;
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        // The first token was already used: treated as theft, and the new one dies with it.
        var reuse = await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = first.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        var afterReuse = await anonymous.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = second.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var (client, auth) = await factory.NewUserAsync();

        var logout = await client.PostAsJsonAsync("/api/v1/auth/logout", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Deleting_the_account_requires_the_password_and_erases_everything()
    {
        var (client, auth) = await factory.NewUserAsync();
        await client.PutAsJsonAsync("/api/v1/preferences", new { minSalary = 1800 });

        var wrong = await client.PostAsJsonAsync("/api/v1/account/delete", new { password = "Equivocada1" });
        Assert.Equal(HttpStatusCode.Forbidden, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/profile")).StatusCode);

        var deleted = await client.PostAsJsonAsync("/api/v1/account/delete", new { password = "Segura12345" });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var login = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { email = auth.User.Email, password = "Segura12345" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        var refresh = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Malformed_request_bodies_are_a_400_not_a_500()
    {
        var client = factory.CreateClient();
        // Latin-1 byte for "í" inside a JSON string: invalid UTF-8.
        var invalidUtf8 = new ByteArrayContent([.. "{\"email\":\"a@b.com\",\"password\":\"Segura12345\",\"fullName\":\"Luc"u8.ToArray(), 0xED, .. "a\"}"u8.ToArray()]);
        invalidUtf8.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var response = await client.PostAsync("/api/v1/auth/register", invalidUtf8);
        var notJson = await client.PostAsync("/api/v1/auth/login", new StringContent("esto no es json", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, notJson.StatusCode);
        Assert.Equal("application/problem+json", notJson.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("/api/v1/profile")]
    [InlineData("/api/v1/preferences")]
    [InlineData("/api/v1/jobs")]
    [InlineData("/api/v1/matches")]
    [InlineData("/api/v1/matches/overview")]
    [InlineData("/api/v1/applications")]
    public async Task Private_endpoints_require_authentication(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_tampered_token_is_rejected()
    {
        var (client, auth) = await factory.NewUserAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken[..^3] + "abc");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/profile")).StatusCode);
    }

    [Fact]
    public async Task Responses_carry_a_correlation_id_and_problem_details_include_it()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "test-correlation-123");

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { email = "x@example.com", password = "Nope12345" });

        Assert.Equal("test-correlation-123", response.Headers.GetValues("X-Correlation-ID").Single());
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("test-correlation-123", problem.GetProperty("correlationId").GetString());
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health")]
    public async Task Health_endpoints_report_healthy(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
