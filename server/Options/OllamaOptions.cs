namespace RagKnowledgeService.Options;

public class OllamaOptions
{
    public const string SectionName = "Ollama";

    public required string BaseUrl { get; init; }
    public required string EmbeddingModel { get; init; }
    public required int EmbeddingDimensions { get; init; }
    public required string ChatModel { get; init; }
}
