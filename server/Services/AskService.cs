using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

public interface IAskService
{
    AskResponse Ask(string question, int topK);
}

// Orchestrates retrieval -> confidence gate -> generation. Kept as its own
// class so retrieval and generation stay independently testable/swappable.
public class AskService : IAskService
{
    // Cosine similarity below this means "nothing in the corpus is really
    // relevant" - tuned empirically against the sample docs, not a magic constant.
    private const double ConfidenceThreshold = 0.18;

    private readonly IRetrievalService _retrievalService;
    private readonly ILlmService _llmService;

    public AskService(IRetrievalService retrievalService, ILlmService llmService)
    {
        _retrievalService = retrievalService;
        _llmService = llmService;
    }

    public AskResponse Ask(string question, int topK)
    {
        var results = _retrievalService.Retrieve(question, topK);
        var topScore = results.Count > 0 ? results[0].Score : 0.0;
        var noConfidentAnswer = results.Count == 0 || topScore < ConfidenceThreshold;

        if (noConfidentAnswer)
        {
            return new AskResponse
            {
                Question = question,
                Answer = "I don't have a confident answer to that based on the knowledge base.",
                Citations = new List<Citation>(),
                Confidence = Math.Round(topScore, 4),
                NoConfidentAnswer = true
            };
        }

        var contextChunks = results.Select(r => r.Chunk).ToList();
        var answer = _llmService.GenerateAnswer(question, contextChunks);

        var citations = results.Select(r => new Citation
        {
            DocId = r.Chunk.DocId,
            DocTitle = r.Chunk.DocTitle,
            Snippet = Truncate(r.Chunk.Text, 220),
            Score = Math.Round(r.Score, 4)
        }).ToList();

        return new AskResponse
        {
            Question = question,
            Answer = answer,
            Citations = citations,
            Confidence = Math.Round(topScore, 4),
            NoConfidentAnswer = false
        };
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "...";
}
