using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

// The in-memory read model retrieval queries against: chunk metadata (for BM25
// and for resolving citations) rebuilt from SQLite after every write. The
// vectors themselves live in Qdrant (IVectorStore), not here - this only
// carries what BM25 and citation-building need.
public interface ISearchIndexService
{
    Task RefreshAsync();
    IReadOnlyList<Chunk> GetAllChunks();
    Chunk? GetChunkById(string id);
    Bm25Index GetBm25Index();
}
