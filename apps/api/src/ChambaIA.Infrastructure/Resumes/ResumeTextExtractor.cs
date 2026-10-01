using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;

namespace ChambaIA.Infrastructure.Resumes;

public interface IResumeTextExtractor
{
    /// <summary>Plain text, one visual line per line. Null when the file cannot be read at all.</summary>
    string? Extract(ResumeFileKind kind, byte[] content);
}

/// <summary>Deterministic, local text extraction (PDF and DOCX). No network, no AI.</summary>
public sealed class ResumeTextExtractor : IResumeTextExtractor
{
    private const int MaxPages = 15;
    private const int MaxChars = 60_000;

    public string? Extract(ResumeFileKind kind, byte[] content)
    {
        try
        {
            var text = kind == ResumeFileKind.Pdf ? FromPdf(content) : FromDocx(content);
            return text.Length > MaxChars ? text[..MaxChars] : text;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Corrupt or hostile files must never become a 500.
            return null;
        }
    }

    private static string FromPdf(byte[] content)
    {
        using var pdf = PdfDocument.Open(content);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages().Take(MaxPages))
        {
            // Group words by their baseline so a visual row becomes one line, then order left to right.
            var rows = page.GetWords()
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom / 4.0))
                .OrderByDescending(g => g.Key);
            foreach (var row in rows)
                sb.AppendLine(string.Join(' ', row.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text)));
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string FromDocx(byte[] content)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(content), false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return "";

        var sb = new StringBuilder();
        foreach (var paragraph in body.Descendants<Paragraph>())
            sb.AppendLine(string.Concat(paragraph.Descendants<Text>().Select(t => t.Text)));
        return sb.ToString();
    }
}
