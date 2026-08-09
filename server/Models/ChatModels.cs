namespace RagKnowledgeService.Models;

public class ChatRequest
{
    public required string Message { get; init; }
    public int TopK { get; init; } = 3;
}

public class ChatMessageDto
{
    public required string Id { get; init; }
    public required string Role { get; init; }
    public required string Content { get; init; }
    public List<Citation>? Citations { get; init; }
    public double? Confidence { get; init; }
    public bool NoConfidentAnswer { get; init; }
    public required DateTime CreatedAt { get; init; }
}

public class ChatResponse
{
    public required string ConversationId { get; init; }
    public required ChatMessageDto UserMessage { get; init; }
    public required ChatMessageDto AssistantMessage { get; init; }
}

public class ConversationSummary
{
    public required string Id { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required int MessageCount { get; init; }
    public string? LastMessagePreview { get; init; }
}
