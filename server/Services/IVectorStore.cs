namespace RagKnowledgeService.Services;

public record VectorSearchResult(string ChunkId, double Score);

// The semantic half of retrieval. SQLite stays the source of truth for chunk
// text/metadata (DocumentService/ChunkService own it); this only ever stores
// (chunkId -> embedding) plus enough payload to support cascade deletes by
// document. Swap point for a different vector store: this is the only file
// that would need to change.
public interface IVectorStore
{
    Task EnsureCollectionAsync(CancellationToken ct = default);
    Task UpsertAsync(string chunkId, string documentId, float[] embedding, CancellationToken ct = default);
    Task DeleteAsync(string chunkId, CancellationToken ct = default);
    Task DeleteByDocumentIdAsync(string documentId, CancellationToken ct = default);
    Task<List<VectorSearchResult>> SearchAsync(float[] queryEmbedding, int topK, CancellationToken ct = default);
}
