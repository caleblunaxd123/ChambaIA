namespace ChambaIA.Domain.Entities;

public class Resume
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid UserId { get; set; }

    /// <summary>Storage key (random file name), never the user-supplied name.</summary>
    public string FileUrl { get; set; } = "";
    public string OriginalFilename { get; set; } = "";
    public string MimeType { get; set; } = "";
    public long SizeBytes { get; set; }

    public string? ParsedText { get; set; }
    /// <summary>JSON produced by the CV parser, validated against the profile schema.</summary>
    public string? StructuredData { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
