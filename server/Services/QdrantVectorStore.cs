using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using RagKnowledgeService.Options;

namespace RagKnowledgeService.Services;

public class QdrantVectorStore : IVectorStore
{
    private readonly QdrantClient _client;
    private readonly OllamaOptions _ollamaOptions;
    private readonly QdrantOptions _qdrantOptions;
    private readonly ILogger<QdrantVectorStore> _logger;

    public QdrantVectorStore(
        QdrantClient client,
        IOptions<OllamaOptions> ollamaOptions,
        IOptions<QdrantOptions> qdrantOptions,
        ILogger<QdrantVectorStore> logger)
    {
        _client = client;
        _ollamaOptions = ollamaOptions.Value;
        _qdrantOptions = qdrantOptions.Value;
        _logger = logger;
    }

    public async Task EnsureCollectionAsync(CancellationToken ct = default)
    {
        var exists = await _client.CollectionExistsAsync(_qdrantOptions.CollectionName, ct);
        if (exists) return;

        await _client.CreateCollectionAsync(
            _qdrantOptions.CollectionName,
            new VectorParams { Size = (ulong)_ollamaOptions.EmbeddingDimensions, Distance = Distance.Cosine },
            cancellationToken: ct);

        _logger.LogInformation(
            "Created Qdrant collection '{Collection}' ({Dims} dims, cosine).",
            _qdrantOptions.CollectionName, _ollamaOptions.EmbeddingDimensions);
    }

    public async Task UpsertAsync(string chunkId, string documentId, float[] embedding, CancellationToken ct = default)
    {
        var point = new PointStruct
        {
            Id = new PointId { Uuid = chunkId },
            Vectors = embedding
        };
        point.Payload["documentId"] = documentId;

        await _client.UpsertAsync(_qdrantOptions.CollectionName, new List<PointStruct> { point }, cancellationToken: ct);
    }

    public async Task DeleteAsync(string chunkId, CancellationToken ct = default)
    {
        await _client.DeleteAsync(_qdrantOptions.CollectionName, new PointId { Uuid = chunkId }, cancellationToken: ct);
    }

    public async Task DeleteByDocumentIdAsync(string documentId, CancellationToken ct = default)
    {
        await _client.DeleteAsync(
            _qdrantOptions.CollectionName,
            filter: Qdrant.Client.Grpc.Conditions.MatchKeyword("documentId", documentId),
            cancellationToken: ct);
    }

    public async Task<List<VectorSearchResult>> SearchAsync(float[] queryEmbedding, int topK, CancellationToken ct = default)
    {
        var results = await _client.QueryAsync(
            _qdrantOptions.CollectionName,
            query: queryEmbedding,
            limit: (ulong)topK,
            cancellationToken: ct);

        return results
            .Select(r => new VectorSearchResult(r.Id.Uuid, r.Score))
            .ToList();
    }
}
