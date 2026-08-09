namespace RagKnowledgeService.Services;

public interface IHybridRetrievalService
{
    Task<List<RankedResult>> RetrieveAsync(string query, int topK, CancellationToken ct = default);
}
