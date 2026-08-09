namespace RagKnowledgeService.Services;

public interface IHybridRetrievalService
{
    List<RankedResult> Retrieve(string query, int topK);
}
