using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

public record RerankCandidate(Chunk Chunk, double FusedScore, double Bm25Score, double SemanticScore);

public record RankedResult(Chunk Chunk, double Bm25Score, double SemanticScore, double RerankScore);
