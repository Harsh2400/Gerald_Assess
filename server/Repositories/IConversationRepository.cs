using RagKnowledgeService.Data.Entities;

namespace RagKnowledgeService.Repositories;

public interface IConversationRepository
{
    Task<List<ConversationEntity>> GetAllWithMessagesAsync(CancellationToken ct = default);
    Task<ConversationEntity?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<bool> ExistsAsync(string id, CancellationToken ct = default);
    Task<List<ChatMessageEntity>> GetMessagesAsync(string conversationId, CancellationToken ct = default);
    Task<int> GetNextSequenceAsync(string conversationId, CancellationToken ct = default);
    void AddConversation(ConversationEntity conversation);
    void AddMessage(ChatMessageEntity message);
    Task SaveChangesAsync(CancellationToken ct = default);
}
