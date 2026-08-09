using RagKnowledgeService.Data.Entities;

namespace RagKnowledgeService.Repositories;

public interface IChunkRepository
{
    Task<List<ChunkEntity>> ListAsync(string? documentId, CancellationToken ct = default);
    Task<ChunkEntity?> GetByIdAsync(string id, CancellationToken ct = default);

    // Used by SearchIndexService to rebuild the BM25 + citation-metadata read
    // model; needs the parent document's title, hence the Include.
    Task<List<ChunkEntity>> GetAllWithDocumentAsync(CancellationToken ct = default);

    void RemoveRange(IEnumerable<ChunkEntity> chunks);
    void Remove(ChunkEntity chunk);
    Task SaveChangesAsync(CancellationToken ct = default);
}
