using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RagKnowledgeService.Data;
using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

// Direct chunk-level CRUD, for correcting or trimming a single chunk without
// re-ingesting its whole parent document. A hand-edited chunk's StartChar/EndChar
// are reset to -1: they'd otherwise point at stale offsets into the original
// document content, which is worse than admitting we no longer know.
public class ChunkService : IChunkService
{
    private readonly AppDbContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISearchIndexService _searchIndex;

    public ChunkService(AppDbContext db, IEmbeddingService embeddingService, ISearchIndexService searchIndex)
    {
        _db = db;
        _embeddingService = embeddingService;
        _searchIndex = searchIndex;
    }

    public async Task<List<ChunkSummary>> ListAsync(string? documentId)
    {
        var query = _db.Chunks.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(documentId))
        {
            query = query.Where(c => c.DocumentId == documentId);
        }

        return await query
            .OrderBy(c => c.DocumentId).ThenBy(c => c.ChunkIndex)
            .Select(c => ToSummary(c))
            .ToListAsync();
    }

    public async Task<ChunkSummary?> GetAsync(string id)
    {
        var chunk = await _db.Chunks.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        return chunk is null ? null : ToSummary(chunk);
    }

    public async Task<ChunkSummary?> UpdateTextAsync(string id, string text)
    {
        var chunk = await _db.Chunks.FirstOrDefaultAsync(c => c.Id == id);
        if (chunk is null) return null;

        chunk.Text = text;
        chunk.EmbeddingJson = JsonSerializer.Serialize(_embeddingService.Embed(text));
        chunk.StartChar = -1;
        chunk.EndChar = -1;
        chunk.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _searchIndex.RefreshAsync();

        return ToSummary(chunk);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var chunk = await _db.Chunks.FirstOrDefaultAsync(c => c.Id == id);
        if (chunk is null) return false;

        _db.Chunks.Remove(chunk);
        await _db.SaveChangesAsync();
        await _searchIndex.RefreshAsync();
        return true;
    }

    private static ChunkSummary ToSummary(Data.Entities.ChunkEntity c) => new()
    {
        Id = c.Id,
        DocumentId = c.DocumentId,
        ChunkIndex = c.ChunkIndex,
        Heading = c.Heading,
        Text = c.Text,
        StartChar = c.StartChar,
        EndChar = c.EndChar,
        UpdatedAt = c.UpdatedAt
    };
}
