using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RagKnowledgeService.Data;
using RagKnowledgeService.Data.Entities;
using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

// Wraps IRagQueryService with conversation persistence: every user message and
// grounded assistant reply (citations, confidence included) is stored, so a
// conversation survives a page refresh and the KB usage has an audit trail.
// This does not do multi-turn context fusion (each question is still answered
// independently) - see README for what that would take.
public class ChatService : IChatService
{
    private readonly AppDbContext _db;
    private readonly IRagQueryService _ragQueryService;

    public ChatService(AppDbContext db, IRagQueryService ragQueryService)
    {
        _db = db;
        _ragQueryService = ragQueryService;
    }

    public async Task<List<ConversationSummary>> ListConversationsAsync()
    {
        return await _db.Conversations.AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ConversationSummary
            {
                Id = c.Id,
                CreatedAt = c.CreatedAt,
                MessageCount = c.Messages.Count,
                LastMessagePreview = c.Messages
                    .OrderByDescending(m => m.Sequence)
                    .Select(m => m.Content)
                    .FirstOrDefault()
            })
            .ToListAsync();
    }

    public async Task<ChatResponse?> SendMessageAsync(string? conversationId, string message, int topK)
    {
        ConversationEntity? conversation = null;
        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            conversation = await _db.Conversations.FirstOrDefaultAsync(c => c.Id == conversationId);
            if (conversation is null) return null;
        }

        if (conversation is null)
        {
            conversation = new ConversationEntity();
            _db.Conversations.Add(conversation);
        }

        var nextSequence = await _db.ChatMessages
            .Where(m => m.ConversationId == conversation.Id)
            .Select(m => (int?)m.Sequence)
            .MaxAsync() ?? -1;

        var userMessage = new ChatMessageEntity
        {
            ConversationId = conversation.Id,
            Role = "user",
            Content = message,
            Sequence = nextSequence + 1
        };
        _db.ChatMessages.Add(userMessage);

        var answer = _ragQueryService.Answer(message, topK);

        var assistantMessage = new ChatMessageEntity
        {
            ConversationId = conversation.Id,
            Role = "assistant",
            Content = answer.Answer,
            CitationsJson = JsonSerializer.Serialize(answer.Citations),
            Confidence = answer.Confidence,
            NoConfidentAnswer = answer.NoConfidentAnswer,
            Sequence = nextSequence + 2
        };
        _db.ChatMessages.Add(assistantMessage);

        await _db.SaveChangesAsync();

        return new ChatResponse
        {
            ConversationId = conversation.Id,
            UserMessage = ToDto(userMessage),
            AssistantMessage = ToDto(assistantMessage)
        };
    }

    public async Task<List<ChatMessageDto>?> GetHistoryAsync(string conversationId)
    {
        var exists = await _db.Conversations.AnyAsync(c => c.Id == conversationId);
        if (!exists) return null;

        var messages = await _db.ChatMessages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.Sequence)
            .ToListAsync();

        return messages.Select(ToDto).ToList();
    }

    private static ChatMessageDto ToDto(ChatMessageEntity m) => new()
    {
        Id = m.Id,
        Role = m.Role,
        Content = m.Content,
        Citations = m.CitationsJson is null
            ? null
            : JsonSerializer.Deserialize<List<Citation>>(m.CitationsJson),
        Confidence = m.Confidence,
        NoConfidentAnswer = m.NoConfidentAnswer,
        CreatedAt = m.CreatedAt
    };
}
