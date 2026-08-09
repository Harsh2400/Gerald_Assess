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
        // Chunk.Text is "{DocTitle} — {Heading}\n{body}" (see DocumentService.BuildChunks).
        // Score sentences from the body only - otherwise title/heading keywords
        // (e.g. "Offline" in a heading) leak into the first sentence's score and
        // can outrank a later sentence that's actually more relevant.
        var body = best.Text.Contains('\n') ? best.Text[(best.Text.IndexOf('\n') + 1)..] : best.Text;
        var sentence = PickMostRelevantSentence(question, body);
        return $"{sentence} (from \"{best.DocTitle}\")";
    }

    private static string PickMostRelevantSentence(string question, string text)
    {
        var questionWords = Tokenizer.Tokenize(question).ToHashSet();
        var sentences = Regex.Split(text, @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        if (sentences.Count == 0) return text.Trim();

        return sentences
            .OrderByDescending(s => Tokenizer.Tokenize(s).Count(questionWords.Contains))
            .First()
            .Trim();
    }
}
