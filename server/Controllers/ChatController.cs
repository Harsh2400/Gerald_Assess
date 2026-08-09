using Microsoft.AspNetCore.Mvc;
using RagKnowledgeService.Models;
using RagKnowledgeService.Services;

namespace RagKnowledgeService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    // GET /api/chat - list conversations, newest first.
    [HttpGet]
    public async Task<ActionResult<List<ConversationSummary>>> ListConversations() =>
        Ok(await _chatService.ListConversationsAsync());

    // GET /api/chat/{conversationId} - full message history for one conversation.
    [HttpGet("{conversationId}")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetHistory(string conversationId)
    {
        var history = await _chatService.GetHistoryAsync(conversationId);
        return history is null ? NotFound() : Ok(history);
    }

    // POST /api/chat { "message": "...", "topK": 3 } - starts a new conversation.
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Send([FromBody] ChatRequest request) =>
        await SendInternal(null, request);

    // POST /api/chat/{conversationId} { "message": "...", "topK": 3 } - continues one.
    [HttpPost("{conversationId}")]
    public async Task<ActionResult<ChatResponse>> SendToConversation(string conversationId, [FromBody] ChatRequest request) =>
        await SendInternal(conversationId, request);

    private async Task<ActionResult<ChatResponse>> SendInternal(string? conversationId, ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "message is required." });
        }

        var topK = request.TopK is > 0 and <= 10 ? request.TopK : 3;
        var response = await _chatService.SendMessageAsync(conversationId, request.Message.Trim(), topK);
        return response is null ? NotFound(new { error = "conversation not found." }) : Ok(response);
    }
}
