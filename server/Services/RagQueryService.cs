using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

// Orchestrates hybrid retrieval -> confidence gate -> generation. Used
// statelessly by AskController and, with conversation persistence layered on
// top, by ChatController - both hit the same underlying pipeline.
public class RagQueryService : IRagQueryService
{
    // Rerank score below this means "nothing in the corpus is really
    // relevant" - tuned empirically against the sample docs, not a magic constant.
    private const double ConfidenceThreshold = 0.35;

    private readonly IHybridRetrievalService _retrievalService;
    private readonly ILlmService _llmService;

    public RagQueryService(IHybridRetrievalService retrievalService, ILlmService llmService)
    {
        _retrievalService = retrievalService;
        _llmService = llmService;
    }

    public AskResponse Answer(string question, int topK)
    {
        var results = _retrievalService.Retrieve(question, topK);
        var topScore = results.Count > 0 ? results[0].RerankScore : 0.0;
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
            ChunkId = r.Chunk.Id,
            Heading = r.Chunk.Heading,
            Snippet = Truncate(r.Chunk.Text, 260),
            StartChar = r.Chunk.StartChar,
            EndChar = r.Chunk.EndChar,
            Bm25Score = Math.Round(r.Bm25Score, 4),
            SemanticScore = Math.Round(r.SemanticScore, 4),
            RerankScore = Math.Round(r.RerankScore, 4)
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
