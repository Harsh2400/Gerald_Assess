using Microsoft.EntityFrameworkCore;
using RagKnowledgeService.Data;
using RagKnowledgeService.Data.Entities;

namespace RagKnowledgeService.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly AppDbContext _db;

    public ConversationRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ConversationEntity>> GetAllWithMessagesAsync(CancellationToken ct = default) =>
        await _db.Conversations.AsNoTracking()
            .Include(c => c.Messages)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

    public Task<ConversationEntity?> GetByIdAsync(string id, CancellationToken ct = default) =>
        _db.Conversations.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<bool> ExistsAsync(string id, CancellationToken ct = default) =>
        _db.Conversations.AnyAsync(c => c.Id == id, ct);

    public async Task<List<ChatMessageEntity>> GetMessagesAsync(string conversationId, CancellationToken ct = default) =>
        await _db.ChatMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.Sequence)
            .ToListAsync(ct);

    public async Task<int> GetNextSequenceAsync(string conversationId, CancellationToken ct = default)
    {
        var max = await _db.ChatMessages
            .Where(m => m.ConversationId == conversationId)
            .Select(m => (int?)m.Sequence)
            .MaxAsync(ct);
        return (max ?? -1) + 1;
    }

    public void AddConversation(ConversationEntity conversation) => _db.Conversations.Add(conversation);

    public void AddMessage(ChatMessageEntity message) => _db.ChatMessages.Add(message);

    public Task SaveChangesAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
