using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

public interface IChunkService
{
    Task<List<ChunkSummary>> ListAsync(string? documentId);
    Task<ChunkSummary?> GetAsync(string id);
    Task<ChunkSummary?> UpdateTextAsync(string id, string text);
    Task<bool> DeleteAsync(string id);
}
