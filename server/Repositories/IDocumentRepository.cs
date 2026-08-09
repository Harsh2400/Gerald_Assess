using RagKnowledgeService.Data.Entities;

namespace RagKnowledgeService.Repositories;

// Data-access boundary for documents. Services own business logic (chunking,
// embedding orchestration, DTO mapping); this owns nothing but reading and
// writing DocumentEntity rows.
public interface IDocumentRepository
{
    Task<List<DocumentEntity>> GetAllAsync(CancellationToken ct = default);
    Task<DocumentEntity?> GetByIdAsync(string id, bool includeChunks = false, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    void Add(DocumentEntity document);
    void Remove(DocumentEntity document);
    Task SaveChangesAsync(CancellationToken ct = default);
}
