using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

public interface IRagQueryService
{
    Task<AskResponse> AnswerAsync(string question, int topK, CancellationToken ct = default);
}
