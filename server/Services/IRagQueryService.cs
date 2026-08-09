using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

public interface IRagQueryService
{
    AskResponse Answer(string question, int topK);
}
