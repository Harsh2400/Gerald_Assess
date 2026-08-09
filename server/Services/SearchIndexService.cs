using Microsoft.Extensions.DependencyInjection;
using RagKnowledgeService.Models;
using RagKnowledgeService.Repositories;

namespace RagKnowledgeService.Services;

// Singleton, but its data comes from a scoped repository - so each refresh
// opens a short-lived DI scope (the standard pattern for a singleton that
// periodically needs a scoped dependency) rather than holding its own
// DbContext factory and bypassing the repository layer.
public class SearchIndexService : ISearchIndexService
{
    private readonly IServiceScopeFactory _scopeFactory;

    private volatile List<Chunk> _chunks = new();
    private volatile Dictionary<string, Chunk> _chunksById = new();
    private volatile Bm25Index _bm25Index = new(new List<Chunk>());
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public SearchIndexService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task RefreshAsync()
    {
        await _refreshLock.WaitAsync();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var chunkRepository = scope.ServiceProvider.GetRequiredService<IChunkRepository>();
            var chunkEntities = await chunkRepository.GetAllWithDocumentAsync();

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
                    EndChar = c.EndChar
                })
                .ToList();

            _chunks = chunks;
            _chunksById = chunks.ToDictionary(c => c.Id);
            _bm25Index = new Bm25Index(chunks);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public IReadOnlyList<Chunk> GetAllChunks() => _chunks;

    public Chunk? GetChunkById(string id) => _chunksById.GetValueOrDefault(id);

    public Bm25Index GetBm25Index() => _bm25Index;
}
