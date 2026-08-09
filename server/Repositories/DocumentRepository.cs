using Microsoft.EntityFrameworkCore;
using RagKnowledgeService.Data;
using RagKnowledgeService.Data.Entities;

namespace RagKnowledgeService.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly AppDbContext _db;

    public DocumentRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<DocumentEntity>> GetAllAsync(CancellationToken ct = default) =>
        await _db.Documents.AsNoTracking()
            .Include(d => d.Chunks)
            .OrderByDescending(d => d.UpdatedAt)
            .ToListAsync(ct);

    public async Task<DocumentEntity?> GetByIdAsync(string id, bool includeChunks = false, CancellationToken ct = default)
    {
        IQueryable<DocumentEntity> query = includeChunks
            ? _db.Documents.Include(d => d.Chunks)
            : _db.Documents;

        return await query.FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public Task<bool> AnyAsync(CancellationToken ct = default) => _db.Documents.AnyAsync(ct);

    public void Add(DocumentEntity document) => _db.Documents.Add(document);

    public void Remove(DocumentEntity document) => _db.Documents.Remove(document); // cascades to chunks

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
