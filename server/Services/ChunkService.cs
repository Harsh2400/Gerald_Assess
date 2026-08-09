using RagKnowledgeService.Models;
using RagKnowledgeService.Repositories;

namespace RagKnowledgeService.Services;

// Direct chunk-level CRUD, for correcting or trimming a single chunk without
// re-ingesting its whole parent document. A hand-edited chunk's StartChar/EndChar
// are reset to -1: they'd otherwise point at stale offsets into the original
// document content, which is worse than admitting we no longer know.
public class ChunkService : IChunkService
{
    private readonly IChunkRepository _chunkRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly ISearchIndexService _searchIndex;

    public ChunkService(
        IChunkRepository chunkRepository,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ISearchIndexService searchIndex)
    {
        _chunkRepository = chunkRepository;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _searchIndex = searchIndex;
    }

    public async Task<List<ChunkSummary>> ListAsync(string? documentId)
    {
        var chunks = await _chunkRepository.ListAsync(documentId);
        return chunks.Select(ToSummary).ToList();
    }

    public async Task<ChunkSummary?> GetAsync(string id)
    {
        var chunk = await _chunkRepository.GetByIdAsync(id);
        return chunk is null ? null : ToSummary(chunk);
    }

    public async Task<ChunkSummary?> UpdateTextAsync(string id, string text)
    {
        var chunk = await _chunkRepository.GetByIdAsync(id);
        if (chunk is null) return null;

        var embedding = await _embeddingService.EmbedAsync(text);

        chunk.Text = text;
        chunk.StartChar = -1;
        chunk.EndChar = -1;
        chunk.UpdatedAt = DateTime.UtcNow;

        await _chunkRepository.SaveChangesAsync();
        await _vectorStore.UpsertAsync(chunk.Id, chunk.DocumentId, embedding);
        await _searchIndex.RefreshAsync();

        return ToSummary(chunk);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var chunk = await _chunkRepository.GetByIdAsync(id);
        if (chunk is null) return false;

        _chunkRepository.Remove(chunk);
        await _chunkRepository.SaveChangesAsync();
        await _vectorStore.DeleteAsync(id);
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
