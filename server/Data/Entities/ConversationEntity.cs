namespace RagKnowledgeService.Data.Entities;

public class ConversationEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ChatMessageEntity> Messages { get; set; } = new();
}

public class ChatMessageEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public required string ConversationId { get; set; }
    public ConversationEntity? Conversation { get; set; }

    public required string Role { get; set; } // "user" | "assistant"
    public required string Content { get; set; }

    // JSON-encoded List<Citation>, populated for assistant messages only.
    public string? CitationsJson { get; set; }
    public double? Confidence { get; set; }
    public bool NoConfidentAnswer { get; set; }

    public int Sequence { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
