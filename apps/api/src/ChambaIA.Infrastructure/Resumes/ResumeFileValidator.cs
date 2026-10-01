using System.IO.Compression;

namespace ChambaIA.Infrastructure.Resumes;

public enum ResumeFileKind { Pdf, Docx }

public sealed record ValidatedResumeFile(ResumeFileKind Kind, string SafeFilename, string Extension, string MimeType);

/// <summary>
/// First line of defence for uploads. A file is accepted only when extension, declared MIME type *and* the real
/// content signature all agree. The user-supplied name is never used on disk, only sanitised for display.
/// </summary>
public static class ResumeFileValidator
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private const long MaxUncompressedDocxBytes = 30 * 1024 * 1024;

    private const string PdfMime = "application/pdf";
    private const string DocxMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    public sealed record Failure(string Message);

    public static (ValidatedResumeFile? File, Failure? Error) Validate(string? filename, string? contentType, byte[] content)
    {
        if (content.Length == 0) return (null, new("El archivo está vacío."));
        if (content.Length > MaxBytes) return (null, new("El archivo pesa más de 5 MB. Sube una versión más liviana."));

        var extension = Path.GetExtension(filename ?? "").ToLowerInvariant();
        var kind = extension switch
        {
            ".pdf" => ResumeFileKind.Pdf,
            ".docx" => ResumeFileKind.Docx,
            _ => (ResumeFileKind?)null
        };
        if (kind is null) return (null, new("Solo aceptamos CV en PDF o DOCX."));

        var mime = (contentType ?? "").Split(';')[0].Trim().ToLowerInvariant();
        var expectedMime = kind == ResumeFileKind.Pdf ? PdfMime : DocxMime;
        // Some clients send a generic type; only a *different specific* type is suspicious.
        if (mime is not ("" or "application/octet-stream") && mime != expectedMime)
            return (null, new("El tipo de archivo no coincide con su extensión."));

        if (!HasSignature(kind.Value, content)) return (null, new("El archivo no parece ser un " + (kind == ResumeFileKind.Pdf ? "PDF" : "DOCX") + " válido."));
        if (kind == ResumeFileKind.Docx && !LooksLikeSafeDocx(content)) return (null, new("No pudimos leer ese documento de Word."));

        return (new ValidatedResumeFile(kind.Value, SafeDisplayName(filename, extension), extension, expectedMime), null);
    }

    private static bool HasSignature(ResumeFileKind kind, byte[] c) => kind switch
    {
        // "%PDF-" may be preceded by a few junk bytes in the wild, never by many.
        ResumeFileKind.Pdf => c.AsSpan(0, Math.Min(c.Length, 1024)).IndexOf("%PDF-"u8) >= 0,
        _ => c.Length > 4 && c[0] == 0x50 && c[1] == 0x4B && c[2] == 0x03 && c[3] == 0x04
    };

    /// <summary>Rejects zip bombs and ZIPs that are not a Word document.</summary>
    private static bool LooksLikeSafeDocx(byte[] content)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
            long total = 0;
            var hasDocument = false;
            foreach (var entry in zip.Entries)
            {
                total += entry.Length;
                if (total > MaxUncompressedDocxBytes) return false;
                if (entry.FullName == "word/document.xml") hasDocument = true;
            }
            return hasDocument;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    public static string SafeDisplayName(string? filename, string extension)
    {
        // Some clients send the full local path; on Linux `Path` does not treat '\' as a separator, so cut both by hand.
        var raw = filename ?? "";
        var lastSeparator = raw.LastIndexOfAny(['/', '\\']);
        var name = Path.GetFileNameWithoutExtension(lastSeparator >= 0 ? raw[(lastSeparator + 1)..] : raw);
        var cleaned = new string(name.Where(c => !char.IsControl(c) && c is not ('<' or '>' or '"' or '\\' or '/' or '|' or '?' or '*' or ':')).ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "cv";
        if (cleaned.Length > 80) cleaned = cleaned[..80];
        return cleaned + extension;
    }
}
