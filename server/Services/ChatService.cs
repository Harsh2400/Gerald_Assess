using System.Text.Json;
using RagKnowledgeService.Data.Entities;
using RagKnowledgeService.Models;
using RagKnowledgeService.Repositories;

namespace RagKnowledgeService.Services;

// Wraps IRagQueryService with conversation persistence: every user message and
// grounded assistant reply (citations, confidence included) is stored, so a
// conversation survives a page refresh and the KB usage has an audit trail.
// This does not do multi-turn context fusion (each question is still answered
// independently) - see README for what that would take.
public class ChatService : IChatService
{
    private readonly IConversationRepository _conversationRepository;
    private readonly IRagQueryService _ragQueryService;

    public ChatService(IConversationRepository conversationRepository, IRagQueryService ragQueryService)
    {
        _conversationRepository = conversationRepository;
        _ragQueryService = ragQueryService;
    }

    public async Task<List<ConversationSummary>> ListConversationsAsync()
    {
        var conversations = await _conversationRepository.GetAllWithMessagesAsync();
        return conversations.Select(c => new ConversationSummary
        {
            Id = c.Id,
            CreatedAt = c.CreatedAt,
            MessageCount = c.Messages.Count,
            LastMessagePreview = c.Messages
                .OrderByDescending(m => m.Sequence)
                .Select(m => m.Content)
                .FirstOrDefault()
        }).ToList();
    }

    public async Task<ChatResponse?> SendMessageAsync(string? conversationId, string message, int topK)
    {
        ConversationEntity? conversation = null;
        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            conversation = await _conversationRepository.GetByIdAsync(conversationId);
            if (conversation is null) return null;
        }

        if (conversation is null)
        {
            conversation = new ConversationEntity();
            _conversationRepository.AddConversation(conversation);
        }

        var nextSequence = await _conversationRepository.GetNextSequenceAsync(conversation.Id);

        var userMessage = new ChatMessageEntity
        {
            ConversationId = conversation.Id,
            Role = "user",
            Content = message,
            Sequence = nextSequence
        };
        _conversationRepository.AddMessage(userMessage);

        var answer = await _ragQueryService.AnswerAsync(message, topK);

        var assistantMessage = new ChatMessageEntity
        {
            ConversationId = conversation.Id,
            Role = "assistant",
            Content = answer.Answer,
            CitationsJson = JsonSerializer.Serialize(answer.Citations),
            Confidence = answer.Confidence,
            NoConfidentAnswer = answer.NoConfidentAnswer,
            Sequence = nextSequence + 1
        };
        _conversationRepository.AddMessage(assistantMessage);

        await _conversationRepository.SaveChangesAsync();

        return new ChatResponse
        {
            ConversationId = conversation.Id,
            UserMessage = ToDto(userMessage),
            AssistantMessage = ToDto(assistantMessage)
        };
    }

    public async Task<List<ChatMessageDto>?> GetHistoryAsync(string conversationId)
    {
        var exists = await _conversationRepository.ExistsAsync(conversationId);
        if (!exists) return null;

        var messages = await _conversationRepository.GetMessagesAsync(conversationId);
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
