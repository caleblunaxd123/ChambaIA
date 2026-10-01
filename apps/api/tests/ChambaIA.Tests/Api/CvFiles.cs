using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig.Writer;

namespace ChambaIA.Tests.Api;

/// <summary>Builds real PDF/DOCX files in memory so the upload tests exercise the actual extractors.</summary>
public static class CvFiles
{
    public static byte[] Docx(string text)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document(new Body(
                text.Split('\n').Select(line => new Paragraph(new Run(new Text(line.TrimEnd('\r')) { Space = SpaceProcessingModeValues.Preserve })))));
            main.Document.Save();
        }
        return stream.ToArray();
    }

    /// <summary>One text row per line (Helvetica; accents, bullets and dashes are mapped to ASCII).</summary>
    public static byte[] Pdf(string text)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);
        var lines = text.Split('\n').Select(l => Ascii(l.TrimEnd('\r'))).ToList();

        for (var i = 0; i < lines.Count; i += 55)
        {
            var page = builder.AddPage(595, 842);
            var y = 800;
            foreach (var line in lines.Skip(i).Take(55))
            {
                if (line.Length > 0) page.AddText(line, 10, new UglyToad.PdfPig.Core.PdfPoint(40, y), font);
                y -= 14;
            }
        }
        return builder.Build();
    }

    public static byte[] BlankPdf()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(595, 842);
        return builder.Build();
    }

    public static byte[] ZipWithoutWordDocument()
    {
        using var stream = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("hello.txt").Open());
            writer.Write("not a word document");
        }
        return stream.ToArray();
    }

    // The PDF writer's built-in fonts cannot draw accents; the parser normalises accents away, so the test stays meaningful.
    private static string Ascii(string s) => new string(s
        .Replace('•', '-').Replace('–', '-').Replace('·', '|').Replace('º', 'o')
        .Normalize(System.Text.NormalizationForm.FormD)
        .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
        .ToArray());
}
