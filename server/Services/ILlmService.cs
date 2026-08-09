using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

public interface ILlmService
{
    Task<string> GenerateAnswerAsync(string question, IReadOnlyList<Chunk> contextChunks, CancellationToken ct = default);
}
