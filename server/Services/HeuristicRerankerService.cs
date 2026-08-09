namespace RagKnowledgeService.Services;

// Deterministic stand-in for a cross-encoder reranker. A real cross-encoder
// jointly encodes (query, passage) and learns what "relevant" means; this
// approximates that signal with three explainable features instead of a
// learned model:
//   - the fused BM25+semantic score (already normalized 0..1 against the
//     theoretical max by HybridRetrievalService - an absolute signal, not
//     relative to this batch, so a batch of uniformly bad candidates still
//     scores low instead of the best-of-them looking artificially confident)
//   - lexical overlap between query and chunk (recall-oriented, since chunks
//     are much longer than queries)
//   - an exact-phrase-match bonus (catches the "right product name" case that
//     both BM25 and a hashed embedding can under-rank)
public class HeuristicRerankerService : IRerankerService
{
    public List<RankedResult> Rerank(string query, List<RerankCandidate> candidates)
    {
        if (candidates.Count == 0) return new List<RankedResult>();

        var queryTokens = Tokenizer.Tokenize(query).ToHashSet();
        var queryLower = query.ToLowerInvariant().Trim();

        var results = candidates.Select(c =>
        {
            var normalizedFused = c.FusedScore;

            var chunkTokens = Tokenizer.Tokenize(c.Chunk.Text).ToHashSet();
            var overlap = queryTokens.Count == 0
                ? 0.0
                : (double)queryTokens.Count(chunkTokens.Contains) / queryTokens.Count;

            var exactPhrase = queryLower.Length > 0 &&
                               c.Chunk.Text.ToLowerInvariant().Contains(queryLower) ? 1.0 : 0.0;

            var rerankScore = 0.5 * normalizedFused + 0.35 * overlap + 0.15 * exactPhrase;

            return new RankedResult(c.Chunk, c.Bm25Score, c.SemanticScore, Math.Clamp(rerankScore, 0, 1));
        })
        .OrderByDescending(r => r.RerankScore)
        .ToList();

        return results;
    }
}
