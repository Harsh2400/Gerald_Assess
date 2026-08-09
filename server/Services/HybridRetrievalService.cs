namespace RagKnowledgeService.Services;

// Standard hybrid-search pipeline: run BM25 (keyword, in-process over SQLite-
// backed chunk text) and semantic (Qdrant ANN search over Ollama embeddings)
// independently, fuse their rankings with Reciprocal Rank Fusion, then rerank
// the fused candidate set. RRF is what Elasticsearch/Azure AI Search/Weaviate
// use to combine hybrid results - it works on ranks, not raw scores, which
// sidesteps the fact that BM25 and cosine similarity live on completely
// different scales and can't be averaged directly.
public class HybridRetrievalService : IHybridRetrievalService
{
    private const int CandidatePoolSize = 20;
    private const double RrfK = 60;

    private readonly ISearchIndexService _searchIndex;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly IRerankerService _reranker;

    public HybridRetrievalService(
        ISearchIndexService searchIndex,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        IRerankerService reranker)
    {
        _searchIndex = searchIndex;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _reranker = reranker;
    }

    public async Task<List<RankedResult>> RetrieveAsync(string query, int topK, CancellationToken ct = default)
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

        // --- Semantic search (Qdrant ANN over Ollama embeddings) ---
        var queryEmbedding = await _embeddingService.EmbedAsync(query, ct);
        var semanticHits = await _vectorStore.SearchAsync(queryEmbedding, CandidatePoolSize, ct);
        var semanticRanked = semanticHits
            .Select((hit, rank) => (hit.ChunkId, hit.Score, Rank: rank))
            .ToList();

        // --- Reciprocal Rank Fusion ---
        var bm25RawById = bm25Ranked.ToDictionary(x => x.ChunkId, x => x.Score);
        var semanticRawById = semanticRanked.ToDictionary(x => x.ChunkId, x => x.Score);

        var fusedScores = new Dictionary<string, double>();
        foreach (var (chunkId, _, rank) in bm25Ranked)
        {
            fusedScores[chunkId] = fusedScores.GetValueOrDefault(chunkId) + 1.0 / (RrfK + rank + 1);
        }
        foreach (var (chunkId, _, rank) in semanticRanked)
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
            .Select(id => (Id: id, Chunk: _searchIndex.GetChunkById(id)))
            .Where(x => x.Chunk is not null)
            .Select(x => new RerankCandidate(
                x.Chunk!,
                Math.Min(1.0, fusedScores[x.Id] / maxPossibleFused),
                bm25RawById.GetValueOrDefault(x.Id),
                semanticRawById.GetValueOrDefault(x.Id)))
            .ToList();

        // --- Rerank the fused candidate set ---
        return _reranker.Rerank(query, candidates).Take(topK).ToList();
    }
}
