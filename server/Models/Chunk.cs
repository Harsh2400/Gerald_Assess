namespace RagKnowledgeService.Models;

// A single indexed unit of text: one document is split into several of these.
public class Chunk
{
    public required string Id { get; init; }
    public required string DocId { get; init; }
    public required string DocTitle { get; init; }
    public required string Text { get; init; }
    public required float[] Embedding { get; init; }
}
