using Microsoft.EntityFrameworkCore;
using RagKnowledgeService.Data;
using RagKnowledgeService.Data.Entities;

namespace RagKnowledgeService.Repositories;

public class ChunkRepository : IChunkRepository
{
    private readonly AppDbContext _db;

    public ChunkRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ChunkEntity>> ListAsync(string? documentId, CancellationToken ct = default)
    {
        var query = _db.Chunks.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(documentId))
        {
            query = query.Where(c => c.DocumentId == documentId);
        }

        return await query
            .OrderBy(c => c.DocumentId).ThenBy(c => c.ChunkIndex)
            .ToListAsync(ct);
    }

    public Task<ChunkEntity?> GetByIdAsync(string id, CancellationToken ct = default) =>
        _db.Chunks.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<List<ChunkEntity>> GetAllWithDocumentAsync(CancellationToken ct = default) =>
        await _db.Chunks.AsNoTracking()
            .Include(c => c.Document)
            .ToListAsync(ct);

    public void RemoveRange(IEnumerable<ChunkEntity> chunks) => _db.Chunks.RemoveRange(chunks);

    public void Remove(ChunkEntity chunk) => _db.Chunks.Remove(chunk);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
