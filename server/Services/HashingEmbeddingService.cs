namespace RagKnowledgeService.Services;

// Deterministic stand-in for a real embedding model: a normalized hashed
// bag-of-words vector (the "hashing trick"). No API key or network call needed.
// Words are hashed into fixed buckets, so texts sharing vocabulary land close
// together under cosine similarity - good enough to demo real retrieval behavior.
public class HashingEmbeddingService : IEmbeddingService
{
    public int Dimensions => 2048;

    public float[] Embed(string text)
    {
        var vector = new float[Dimensions];

        foreach (var token in Tokenizer.Tokenize(text))
        {
            var hash = Fnv1aHash(token);
            var index = (int)(hash % (uint)Dimensions);
            var sign = (hash & 1) == 0 ? 1f : -1f; // spreads collisions, cancels bucket bias
            vector[index] += sign;
        }

        Normalize(vector);
        return vector;
    }

    private static uint Fnv1aHash(string token)
    {
        const uint fnvPrime = 16777619;
        var hash = 2166136261;
        foreach (var c in token)
        {
            hash ^= c;
            hash *= fnvPrime;
        }
        return hash;
    }

    private static void Normalize(float[] vector)
    {
        var magnitude = MathF.Sqrt(vector.Sum(v => v * v));
        if (magnitude < 1e-8f) return;
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= magnitude;
        }
    }
}
