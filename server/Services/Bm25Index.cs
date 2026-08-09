using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

// Classic Okapi BM25 over the current chunk set, rebuilt whenever the corpus
// changes (SearchIndexService owns the rebuild). Keyword-exact by design - this
// is what catches product names, error codes, and other terms a semantic
// embedding can blur together, which is why hybrid search runs both.
public class Bm25Index
{
    private const double K1 = 1.5;
    private const double B = 0.75;

    private readonly Dictionary<string, Dictionary<string, int>> _invertedIndex = new(); // term -> chunkId -> freq
    private readonly Dictionary<string, int> _chunkLength = new(); // chunkId -> token count
    private readonly double _avgChunkLength;
    private readonly int _totalChunks;

    public Bm25Index(IReadOnlyList<Chunk> chunks)
    {
        _totalChunks = chunks.Count;
        var totalLength = 0;

        foreach (var chunk in chunks)
        {
            var tokens = Tokenizer.Tokenize(chunk.Text);
            _chunkLength[chunk.Id] = tokens.Count;
            totalLength += tokens.Count;

            foreach (var group in tokens.GroupBy(t => t))
            {
                if (!_invertedIndex.TryGetValue(group.Key, out var postings))
                {
                    postings = new Dictionary<string, int>();
                    _invertedIndex[group.Key] = postings;
                }
                postings[chunk.Id] = group.Count();
            }
        }

        _avgChunkLength = _totalChunks == 0 ? 0 : (double)totalLength / _totalChunks;
    }

    public Dictionary<string, double> ScoreAll(string query)
    {
        var scores = new Dictionary<string, double>();
        if (_totalChunks == 0 || _avgChunkLength == 0) return scores;

        foreach (var term in Tokenizer.Tokenize(query).Distinct())
        {
            if (!_invertedIndex.TryGetValue(term, out var postings)) continue;

            var idf = Math.Log(((_totalChunks - postings.Count + 0.5) / (postings.Count + 0.5)) + 1);

            foreach (var (chunkId, freq) in postings)
            {
                var dl = _chunkLength[chunkId];
                var denom = freq + K1 * (1 - B + B * dl / _avgChunkLength);
                var termScore = idf * (freq * (K1 + 1)) / denom;
                scores[chunkId] = scores.GetValueOrDefault(chunkId) + termScore;
            }
        }

        return scores;
    }
}
