namespace RagKnowledgeService.Data.Entities;

public class DocumentEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public required string Title { get; set; }
    public required string Content { get; set; }
    public string SourceType { get; set; } = "manual"; // "manual" | "folder-seed"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<ChunkEntity> Chunks { get; set; } = new();
}
