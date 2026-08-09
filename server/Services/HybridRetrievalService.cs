using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

// Standard hybrid-search pipeline: run BM25 (keyword) and semantic (vector)
// search independently, fuse their rankings with Reciprocal Rank Fusion, then
// rerank the fused candidate set. RRF is what Elasticsearch/Azure AI
// Search/Weaviate use to combine hybrid results - it works on ranks, not raw
// scores, which sidesteps the fact that BM25 and cosine similarity live on
// completely different scales and can't be averaged directly.
public class HybridRetrievalService : IHybridRetrievalService
{
    private const int CandidatePoolSize = 20;
    private const double RrfK = 60;

    private readonly ISearchIndexService _searchIndex;
    private readonly IEmbeddingService _embeddingService;
    private readonly IRerankerService _reranker;

    public HybridRetrievalService(
        ISearchIndexService searchIndex,
        IEmbeddingService embeddingService,
        IRerankerService reranker)
    {
        _searchIndex = searchIndex;
        _embeddingService = embeddingService;
        _reranker = reranker;
    }

    public List<RankedResult> Retrieve(string query, int topK)
    {
        var allChunks = _searchIndex.GetAllChunks();
        if (allChunks.Count == 0) return new List<RankedResult>();

        // --- Keyword search (BM25) ---
        var bm25Scores = _searchIndex.GetBm25Index().ScoreAll(query);
        var bm25Ranked = bm25Scores
            .OrderByDescending(kv => kv.Value)
            .Take(CandidatePoolSize)
            .Select((kv, rank) => (ChunkId: kv.Key, Score: kv.Value, Rank: rank))
            .ToList();

        // --- Semantic search (cosine similarity over embeddings) ---
        var queryEmbedding = _embeddingService.Embed(query);
        var semanticRanked = allChunks
            .Select(c => (Chunk: c, Score: CosineSimilarity(queryEmbedding, c.Embedding)))
            .OrderByDescending(x => x.Score)
            .Take(CandidatePoolSize)
            .Select((x, rank) => (ChunkId: x.Chunk.Id, Chunk: x.Chunk, Score: x.Score, Rank: rank))
            .ToList();

        // --- Reciprocal Rank Fusion ---
        var chunksById = allChunks.ToDictionary(c => c.Id);
        var bm25RawById = bm25Ranked.ToDictionary(x => x.ChunkId, x => x.Score);
        var semanticRawById = semanticRanked.ToDictionary(x => x.ChunkId, x => x.Score);

        var fusedScores = new Dictionary<string, double>();
        foreach (var (chunkId, _, rank) in bm25Ranked)
        {
            fusedScores[chunkId] = fusedScores.GetValueOrDefault(chunkId) + 1.0 / (RrfK + rank + 1);
        }
        foreach (var (chunkId, _, _, rank) in semanticRanked)
        {
            fusedScores[chunkId] = fusedScores.GetValueOrDefault(chunkId) + 1.0 / (RrfK + rank + 1);
        }

        if (fusedScores.Count == 0) return new List<RankedResult>();

        // Normalize against the theoretical max (rank #1 in both BM25 and
        // semantic lists), not the candidate batch's own min/max - the latter
        // would make the best-of-a-bad-lot candidate always score ~1.0, which
        // defeats the whole point of a confidence signal on irrelevant queries.
        var maxPossibleFused = 2.0 / (RrfK + 1);

        var candidates = fusedScores.Keys
            .Where(chunksById.ContainsKey)
            .Select(id => new RerankCandidate(
                chunksById[id],
                Math.Min(1.0, fusedScores[id] / maxPossibleFused),
                bm25RawById.GetValueOrDefault(id),
                semanticRawById.GetValueOrDefault(id)))
            .ToList();

        // --- Rerank the fused candidate set ---
        return _reranker.Rerank(query, candidates).Take(topK).ToList();
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
