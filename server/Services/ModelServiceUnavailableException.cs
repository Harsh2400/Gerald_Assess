namespace RagKnowledgeService.Services;

// Thrown when a local model backend (Ollama embeddings/chat, Qdrant) can't be
// reached or returns something unusable. Mapped to HTTP 502 by the global
// exception-handling middleware in Program.cs instead of a try/catch in every
// controller that happens to touch a model call.
public class ModelServiceUnavailableException : Exception
{
    public ModelServiceUnavailableException(string message, Exception? inner = null)
        : base(message, inner) { }
}
