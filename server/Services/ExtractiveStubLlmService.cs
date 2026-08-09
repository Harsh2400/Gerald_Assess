using System.Text.RegularExpressions;
using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

// Deterministic stand-in for a real LLM call: picks the sentence in the
// top-ranked chunk that shares the most words with the question, instead of
// generating free text. Answers stay strictly grounded in retrieved text by
// construction, which is the property a real LLM call needs to be prompted for.
public class ExtractiveStubLlmService : ILlmService
{
    public string GenerateAnswer(string question, IReadOnlyList<Chunk> contextChunks)
    {
        if (contextChunks.Count == 0)
        {
            return "I don't have enough information in the knowledge base to answer that.";
        }

        var best = contextChunks[0];
        var sentence = PickMostRelevantSentence(question, best.Text);
        return $"{sentence} (from \"{best.DocTitle}\")";
    }

    private static string PickMostRelevantSentence(string question, string text)
    {
        var questionWords = Tokenize(question).ToHashSet();
        var sentences = Regex.Split(text, @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (sentences.Count == 0) return text.Trim();

        return sentences
            .OrderByDescending(s => Tokenize(s).Count(questionWords.Contains))
            .First()
            .Trim();
    }

    private static IEnumerable<string> Tokenize(string text) =>
        Regex.Matches(text.ToLowerInvariant(), "[a-z0-9]+").Select(m => m.Value);
}
