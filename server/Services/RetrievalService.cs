using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

public record ScoredChunk(Chunk Chunk, double Score);

public interface IRetrievalService
{
    List<ScoredChunk> Retrieve(string query, int topK);
}

// Brute-force cosine similarity over every chunk in the store. Fine up to a
// few tens of thousands of chunks; see README "Scaling" for what replaces
// this (ANN index / dedicated vector store) beyond that.
public class RetrievalService : IRetrievalService
{
    private readonly IKnowledgeStore _store;
    private readonly IEmbeddingService _embeddingService;

    public RetrievalService(IKnowledgeStore store, IEmbeddingService embeddingService)
    {
        _store = store;
        _embeddingService = embeddingService;
    }

    public List<ScoredChunk> Retrieve(string query, int topK)
    {
        var queryEmbedding = _embeddingService.Embed(query);

        return _store.GetAllChunks()
            .Select(chunk => new ScoredChunk(chunk, CosineSimilarity(queryEmbedding, chunk.Embedding)))
            .OrderByDescending(sc => sc.Score)
            .Take(topK)
            .ToList();
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        if (magA < 1e-8 || magB < 1e-8) return 0;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}
