using System.Collections.Concurrent;
using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

// Registered as a singleton: one shared index for the process lifetime,
// populated once at startup by the ingestion service.
public class InMemoryKnowledgeStore : IKnowledgeStore
{
    private readonly ConcurrentBag<Chunk> _chunks = new();

    public void AddChunk(Chunk chunk) => _chunks.Add(chunk);

    public IReadOnlyList<Chunk> GetAllChunks() => _chunks.ToList();

    public IReadOnlyList<string> GetIngestedDocTitles() =>
        _chunks.Select(c => c.DocTitle).Distinct().OrderBy(t => t).ToList();
}
