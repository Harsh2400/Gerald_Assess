using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

// The in-memory read model that retrieval queries against: a flat chunk list
// plus a BM25 index built over it. Persistence (SQLite) is the source of
// truth; this is rebuilt from it after every write (add/update/delete a
// document or chunk) and once at startup. Swap point for a real vector store:
// this whole service is what gets replaced by a pgvector/Azure AI Search
// client at scale (see README).
public interface ISearchIndexService
{
    Task RefreshAsync();
    IReadOnlyList<Chunk> GetAllChunks();
    Bm25Index GetBm25Index();
}
