using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RagKnowledgeService.Data;
using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

public class SearchIndexService : ISearchIndexService
{
    private readonly IDbContextFactory<AppDbContext> _dbContextFactory;

    private volatile List<Chunk> _chunks = new();
    private volatile Bm25Index _bm25Index = new(new List<Chunk>());
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public SearchIndexService(IDbContextFactory<AppDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task RefreshAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var chunkEntities = await db.Chunks.AsNoTracking()
                .Include(c => c.Document)
                .ToListAsync();

            var chunks = chunkEntities
                .Where(c => c.Document != null)
                .Select(c => new Chunk
                {
                    Id = c.Id,
                    DocId = c.DocumentId,
                    DocTitle = c.Document!.Title,
                    ChunkIndex = c.ChunkIndex,
                    Heading = c.Heading,
                    Text = c.Text,
                    StartChar = c.StartChar,
                    EndChar = c.EndChar,
                    Embedding = JsonSerializer.Deserialize<float[]>(c.EmbeddingJson) ?? Array.Empty<float>()
                })
                .ToList();

            _chunks = chunks;
            _bm25Index = new Bm25Index(chunks);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public IReadOnlyList<Chunk> GetAllChunks() => _chunks;

    public Bm25Index GetBm25Index() => _bm25Index;
}
