namespace RagKnowledgeService.Options;

public class QdrantOptions
{
    public const string SectionName = "Qdrant";

    public required string Host { get; init; }
    public required int GrpcPort { get; init; }
    public required string CollectionName { get; init; }
}
