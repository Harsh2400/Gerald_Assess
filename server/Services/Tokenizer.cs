using System.Text.RegularExpressions;

namespace RagKnowledgeService.Services;

public static class Tokenizer
{
    private static readonly HashSet<string> StopWords = new(new[]
    {
        "the", "a", "an", "and", "or", "of", "to", "in", "on", "for", "is", "are",
        "was", "were", "be", "been", "with", "as", "at", "by", "from", "this",
        "that", "it", "its", "can", "will", "your", "you", "their", "has", "have"
    });

    public static List<string> Tokenize(string text) =>
        Regex.Matches(text.ToLowerInvariant(), "[a-z0-9]+")
            .Select(m => m.Value)
            .Where(w => w.Length > 2 && !StopWords.Contains(w))
            .ToList();
}
