using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using RagKnowledgeService.Options;

namespace RagKnowledgeService.Services;

// Real embeddings via a locally-running Ollama server (nomic-embed-text:v1.5,
// 768 dims). No API key, no external network call - the model runs on the same
// machine. This is the swap point the earlier hashing stub was built to allow.
public class OllamaEmbeddingService : IEmbeddingService
{
    private record EmbedRequest(string Model, string Input);
    private record EmbedResponse(float[][] Embeddings);

    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    public int Dimensions => _options.EmbeddingDimensions;

    public OllamaEmbeddingService(HttpClient httpClient, IOptions<OllamaOptions> options, ILogger<OllamaEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/embed",
                new EmbedRequest(_options.EmbeddingModel, text),
                ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken: ct);
            if (body?.Embeddings is not { Length: > 0 })
            {
                throw new ModelServiceUnavailableException("Ollama returned no embedding vector.");
            }

            return body.Embeddings[0];
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to reach Ollama at {BaseUrl} for embedding model {Model}. Is `ollama serve` running?", _httpClient.BaseAddress, _options.EmbeddingModel);
            throw new ModelServiceUnavailableException(
                $"Could not reach Ollama for embeddings (model '{_options.EmbeddingModel}'). Ensure `ollama serve` is running and the model is pulled.", ex);
        }
    }
}
