using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

// Swap this for pgvector / Azure AI Search / Cosmos DB vector search once the
// corpus outgrows a single process. See README "Scaling" section.
public interface IKnowledgeStore
{
    void AddChunk(Chunk chunk);
    IReadOnlyList<Chunk> GetAllChunks();
    IReadOnlyList<string> GetIngestedDocTitles();
}
