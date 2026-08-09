namespace RagKnowledgeService.Data.Entities;

public class ChunkEntity
{
    // Dashed GUID string (not "N" format) - this doubles as the Qdrant point ID,
    // which requires standard UUID formatting.
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public required string DocumentId { get; set; }
    public DocumentEntity? Document { get; set; }

    public int ChunkIndex { get; set; }
    public required string Heading { get; set; }
    public required string Text { get; set; }

    // Offsets into the parent document's Content, for exact-pinpoint citations.
    // Set to -1 when a chunk has been hand-edited and offsets are no longer authoritative.
    public int StartChar { get; set; }
    public int EndChar { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
