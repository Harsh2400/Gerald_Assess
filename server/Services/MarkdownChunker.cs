using System.Text;
using System.Text.RegularExpressions;

namespace RagKnowledgeService.Services;

public record ParsedSection(string Heading, string Body, int StartChar, int EndChar);
public record ParsedDocument(string Title, List<ParsedSection> Sections);
public record ChunkPiece(string Text, int StartChar, int EndChar);

// Splits on markdown "## " headings so each chunk is one coherent section
// (e.g. "Pricing - Enterprise Plan") rather than an arbitrary fixed-size window.
// Sections longer than MaxWordsPerChunk are further split with a sliding word
// window so no single chunk grows unbounded. Every piece carries StartChar/EndChar
// offsets into the (newline-normalized) document content, so citations can point
// at an exact location instead of just "somewhere in this doc".
public static class MarkdownChunker
{
    private const int MaxWordsPerChunk = 120;
    private const int OverlapWords = 20;

    // Callers should store DocumentEntity.Content as this normalized form so
    // offsets computed here stay valid when the document is re-read later.
    public static string Normalize(string markdown) => markdown.Replace("\r\n", "\n");

    public static ParsedDocument Parse(string normalizedMarkdown)
    {
        var lines = normalizedMarkdown.Split('\n');
        string? title = null;
        var sections = new List<ParsedSection>();
        var currentHeading = "Overview";
        var currentBody = new StringBuilder();
        var cursor = 0;
        var sectionStart = 0;

        void FlushSection(int endChar)
        {
            var text = currentBody.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(text))
            {
                // Re-locate the trimmed text within [sectionStart, endChar) so
                // offsets point at the trimmed body, not the raw accumulated buffer.
                // Clamp to the string length: the cursor assumes every consumed line
                // was followed by '\n', which is false for the final line.
                var clampedEnd = Math.Min(endChar, normalizedMarkdown.Length);
                var clampedStart = Math.Min(sectionStart, clampedEnd);
                var raw = normalizedMarkdown.Substring(clampedStart, Math.Max(0, clampedEnd - clampedStart));
                var relativeStart = raw.IndexOf(text, StringComparison.Ordinal);
                var absStart = relativeStart >= 0 ? clampedStart + relativeStart : clampedStart;
                sections.Add(new ParsedSection(currentHeading, text, absStart, absStart + text.Length));
            }
            currentBody.Clear();
        }

        foreach (var line in lines)
        {
            if (line.StartsWith("# ") && title is null)
            {
                title = line[2..].Trim();
                cursor += line.Length + 1;
                sectionStart = cursor;
                continue;
            }
            if (line.StartsWith("## "))
            {
                FlushSection(cursor);
                currentHeading = line[3..].Trim();
                cursor += line.Length + 1;
                sectionStart = cursor;
                continue;
            }
            currentBody.AppendLine(line);
            cursor += line.Length + 1;
        }
        FlushSection(cursor);

        return new ParsedDocument(title ?? "Untitled", sections);
    }

    public static List<ChunkPiece> SplitLongSection(string body)
    {
        var words = Regex.Matches(body, @"\S+")
            .Select(m => (Text: m.Value, Index: m.Index))
            .ToList();

        if (words.Count <= MaxWordsPerChunk)
        {
            return new List<ChunkPiece> { new(body, 0, body.Length) };
        }

        var pieces = new List<ChunkPiece>();
        var step = MaxWordsPerChunk - OverlapWords;
        for (var start = 0; start < words.Count; start += step)
        {
            var windowWords = words.Skip(start).Take(MaxWordsPerChunk).ToList();
            var startChar = windowWords[0].Index;
            var last = windowWords[^1];
            var endChar = last.Index + last.Text.Length;
            pieces.Add(new ChunkPiece(body[startChar..endChar], startChar, endChar));
            if (start + MaxWordsPerChunk >= words.Count) break;
        }
        return pieces;
    }
}
