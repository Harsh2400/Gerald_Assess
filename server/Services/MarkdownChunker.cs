using System.Text;

namespace RagKnowledgeService.Services;

public record ParsedDocument(string Title, List<(string Heading, string Body)> Sections);

// Splits on markdown "## " headings so each chunk is one coherent section
// (e.g. "Pricing - Enterprise Plan") rather than an arbitrary fixed-size window.
// Sections longer than MaxWordsPerChunk are further split with a sliding word
// window so no single chunk grows unbounded.
public static class MarkdownChunker
{
    private const int MaxWordsPerChunk = 120;
    private const int OverlapWords = 20;

    public static ParsedDocument Parse(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        string? title = null;
        var sections = new List<(string Heading, string Body)>();
        var currentHeading = "Overview";
        var currentBody = new StringBuilder();

        void FlushSection()
        {
            var text = currentBody.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                sections.Add((currentHeading, text));
            }
            currentBody.Clear();
        }

        foreach (var line in lines)
        {
            if (line.StartsWith("# ") && title is null)
            {
                title = line[2..].Trim();
                continue;
            }
            if (line.StartsWith("## "))
            {
                FlushSection();
                currentHeading = line[3..].Trim();
                continue;
            }
            currentBody.AppendLine(line);
        }
        FlushSection();

        return new ParsedDocument(title ?? "Untitled", sections);
    }

    public static List<string> SplitLongSection(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= MaxWordsPerChunk)
        {
            return new List<string> { text };
        }

        var chunks = new List<string>();
        var step = MaxWordsPerChunk - OverlapWords;
        for (var start = 0; start < words.Length; start += step)
        {
            var slice = words.Skip(start).Take(MaxWordsPerChunk);
            chunks.Add(string.Join(' ', slice));
            if (start + MaxWordsPerChunk >= words.Length) break;
        }
        return chunks;
    }
}
