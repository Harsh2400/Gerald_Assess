using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace RagKnowledgeService.Services;

public interface IDocumentFileExtractor
{
    Task<string> ExtractAsync(IFormFile file, CancellationToken cancellationToken = default);
}

public sealed class DocumentFileExtractor : IDocumentFileExtractor
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".txt", ".md", ".pdf", ".docx" };

    public async Task<string> ExtractAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
            throw new InvalidDataException("Unsupported file type. Upload a PDF, DOCX, Markdown, or text file.");

        await using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        return extension.ToLowerInvariant() switch
        {
            ".pdf" => ExtractPdf(buffer),
            ".docx" => ExtractDocx(buffer),
            _ => await ReadTextAsync(buffer, cancellationToken)
        };
    }

    private static async Task<string> ReadTextAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static string ExtractPdf(Stream stream)
    {
        using var document = PdfDocument.Open(stream);
        // page.Text is the raw content-stream order and commonly concatenates
        // every glyph in academic PDFs. ContentOrderTextExtractor restores word
        // spacing and a useful reading order before the text reaches chunking.
        return string.Join("\n\n", document.GetPages()
            .Select(page => ContentOrderTextExtractor.GetText(page))
            .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static string ExtractDocx(Stream stream)
    {
        using var document = WordprocessingDocument.Open(stream, false);
        var paragraphs = document.MainDocumentPart?.Document.Body?
            .Descendants<Paragraph>()
            .Select(paragraph => paragraph.InnerText.Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text)) ?? [];
        return string.Join("\n\n", paragraphs);
    }
}
