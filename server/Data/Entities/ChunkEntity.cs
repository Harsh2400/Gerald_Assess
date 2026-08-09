namespace RagKnowledgeService.Data.Entities;

public class ChunkEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public required string DocumentId { get; set; }
    public DocumentEntity? Document { get; set; }

    public int ChunkIndex { get; set; }
    public required string Heading { get; set; }
    public required string Text { get; set; }

    // Offsets into the parent document's Content, for exact-pinpoint citations.
    // Set to -1 when a chunk has been hand-edited and offsets are no longer authoritative.
    public int StartChar { get; set; }
    public int EndChar { get; set; }

    // JSON-encoded float[] - stored as text for readability/debuggability in SQLite.
    public required string EmbeddingJson { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
