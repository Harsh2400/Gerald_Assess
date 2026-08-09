namespace RagKnowledgeService.Models;

public class DocumentSummary
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string SourceType { get; init; }
    public required int ChunkCount { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class ChunkSummary
{
    public required string Id { get; init; }
    public required string DocumentId { get; init; }
    public required int ChunkIndex { get; init; }
    public required string Heading { get; init; }
    public required string Text { get; init; }
    public required int StartChar { get; init; }
    public required int EndChar { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

public class DocumentDetail : DocumentSummary
{
    public required string Content { get; init; }
    public required List<ChunkSummary> Chunks { get; init; }
}

public class CreateDocumentRequest
{
    public required string Title { get; init; }
    public required string Content { get; init; }
}

public class UpdateDocumentRequest
{
    public required string Title { get; init; }
    public required string Content { get; init; }
}

public class UpdateChunkRequest
{
    public required string Text { get; init; }
}
