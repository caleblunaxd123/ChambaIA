using System.Text.Json;
using System.Text.Json.Serialization;
using ChambaIA.Domain.Entities;
using ChambaIA.Domain.Enums;
using ChambaIA.Domain.Resumes;
using ChambaIA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ChambaIA.Infrastructure.Resumes;

public enum UploadFailure { None, Invalid, Unreadable, LimitReached }

public sealed record ResumeUploadResult(Resume? Resume, ParsedResume? Parsed, UploadFailure Failure, string? Message)
{
    public static ResumeUploadResult Fail(UploadFailure failure, string message) => new(null, null, failure, message);
}

/// <summary>The CV is analysed exactly once, at upload. Afterwards only the stored structure is used; the file is never re-sent anywhere.</summary>
public sealed class ResumeService(AppDbContext db, IResumeTextExtractor extractor, IResumeStorage storage, TimeProvider clock)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static int MaxResumes(SubscriptionPlan plan) => plan == SubscriptionPlan.ProPlus ? 5 : 1;

    public async Task<ResumeUploadResult> UploadAsync(Guid userId, SubscriptionPlan plan, string? filename, string? contentType, byte[] content, CancellationToken ct)
    {
        var (file, error) = ResumeFileValidator.Validate(filename, contentType, content);
        if (file is null) return ResumeUploadResult.Fail(UploadFailure.Invalid, error!.Message);

        var existing = await db.Resumes.Where(r => r.UserId == userId).OrderBy(r => r.CreatedAt).ToListAsync(ct);
        var limit = MaxResumes(plan);
        if (existing.Count >= limit && limit > 1)
            return ResumeUploadResult.Fail(UploadFailure.LimitReached, $"Tu plan permite hasta {limit} CV. Elimina uno para subir otro.");

        var text = extractor.Extract(file.Kind, content);
        if (text is null || text.Trim().Length < ResumeParser.MinTextLength)
            return ResumeUploadResult.Fail(UploadFailure.Unreadable,
                "No pudimos leer texto en tu CV. Si es una foto o un escaneo, sube la versión en PDF o Word original.");

        var parsed = ResumeParser.Parse(text, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        var key = await storage.SaveAsync(content, file.Extension, ct);

        // Plans with a single CV replace the previous one: the old file and its extracted text are deleted.
        var replaced = existing.Take(Math.Max(0, existing.Count - limit + 1)).ToList();
        db.Resumes.RemoveRange(replaced);

        var now = clock.GetUtcNow();
        var resume = new Resume
        {
            UserId = userId,
            FileUrl = key,
            OriginalFilename = file.SafeFilename,
            MimeType = file.MimeType,
            SizeBytes = content.Length,
            ParsedText = text,
            StructuredData = JsonSerializer.Serialize(parsed, Json),
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Resumes.Add(resume);
        await db.SaveChangesAsync(ct);

        foreach (var old in replaced) await storage.DeleteAsync(old.FileUrl);
        return new ResumeUploadResult(resume, parsed, UploadFailure.None, null);
    }

    public static ParsedResume? ReadStructure(Resume resume) =>
        string.IsNullOrEmpty(resume.StructuredData) ? null : JsonSerializer.Deserialize<ParsedResume>(resume.StructuredData, Json);

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var resume = await db.Resumes.SingleOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (resume is null) return false;

        db.Resumes.Remove(resume);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(resume.FileUrl);
        return true;
    }

    /// <summary>Right-to-erasure helper: removes every stored CV (rows and files) of a user.</summary>
    public async Task DeleteAllAsync(Guid userId, CancellationToken ct)
    {
        var all = await db.Resumes.Where(r => r.UserId == userId).ToListAsync(ct);
        db.Resumes.RemoveRange(all);
        await db.SaveChangesAsync(ct);
        foreach (var r in all) await storage.DeleteAsync(r.FileUrl);
    }
}
