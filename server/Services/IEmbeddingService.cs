namespace RagKnowledgeService.Services;

public interface IEmbeddingService
{
    int Dimensions { get; }
    Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
}
