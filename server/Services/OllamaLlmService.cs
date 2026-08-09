using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.Options;
using RagKnowledgeService.Models;
using RagKnowledgeService.Options;

namespace RagKnowledgeService.Services;

// Real generation via a locally-running Ollama server (gemma3). Only called
// once RagQueryService has already gated on retrieval confidence, so this
// always has at least one genuinely relevant chunk to ground on - the prompt
// still explicitly forbids answering outside the provided context, since a
// model given weak context can still hallucinate around it.
public class OllamaLlmService : ILlmService
{
    private const string SystemPrompt =
        "You are a support assistant that answers ONLY using the numbered source excerpts " +
        "provided in the user message. Rules: " +
        "1) Answer only from the excerpts - never use outside knowledge. " +
        "2) Cite the excerpt number(s) you used inline, like [1] or [1][2]. " +
        "3) If the excerpts don't actually answer the question, say so plainly instead of guessing. " +
        "4) Be concise - two or three sentences unless the question needs more.";

    private record ChatMessage(string Role, string Content);
    private record ChatRequest(string Model, List<ChatMessage> Messages, bool Stream);
    private record ChatResponseMessage(string Role, string Content);
    private record ChatResponse(ChatResponseMessage Message);

    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaLlmService> _logger;

    public OllamaLlmService(HttpClient httpClient, IOptions<OllamaOptions> options, ILogger<OllamaLlmService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string> GenerateAnswerAsync(string question, IReadOnlyList<Chunk> contextChunks, CancellationToken ct = default)
    {
        if (contextChunks.Count == 0)
        {
            return "I don't have enough information in the knowledge base to answer that.";
        }

        var userPrompt = BuildUserPrompt(question, contextChunks);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/chat",
                new ChatRequest(
                    _options.ChatModel,
                    new List<ChatMessage>
                    {
                        new("system", SystemPrompt),
                        new("user", userPrompt)
                    },
                    Stream: false),
                ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);
            var answer = body?.Message.Content?.Trim();

            if (string.IsNullOrWhiteSpace(answer))
            {
                throw new ModelServiceUnavailableException("Ollama returned an empty chat response.");
            }

            return answer;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to reach Ollama at {BaseUrl} for chat model {Model}. Is `ollama serve` running?", _httpClient.BaseAddress, _options.ChatModel);
            throw new ModelServiceUnavailableException(
                $"Could not reach Ollama for chat generation (model '{_options.ChatModel}'). Ensure `ollama serve` is running and the model is pulled.", ex);
        }
    }

    private static string BuildUserPrompt(string question, IReadOnlyList<Chunk> contextChunks)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < contextChunks.Count; i++)
        {
            var c = contextChunks[i];
            sb.AppendLine($"[{i + 1}] ({c.DocTitle} — {c.Heading})");
            sb.AppendLine(c.Text);
            sb.AppendLine();
        }
        sb.AppendLine($"Question: {question}");
        return sb.ToString();
    }
}
