using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

public interface IChatService
{
    Task<List<ConversationSummary>> ListConversationsAsync();
    Task<ChatResponse?> SendMessageAsync(string? conversationId, string message, int topK);
    Task<List<ChatMessageDto>?> GetHistoryAsync(string conversationId);
}
