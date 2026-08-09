namespace RagKnowledgeService.Services;

// Swap point for a real cross-encoder reranker (Cohere Rerank, a BGE/ms-marco
// cross-encoder, etc.) - those take (query, candidate text) pairs and return a
// single relevance score per pair, same contract as this stub.
public interface IRerankerService
{
    List<RankedResult> Rerank(string query, List<RerankCandidate> candidates);
}
