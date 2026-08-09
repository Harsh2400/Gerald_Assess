using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

// Swap this for a real provider (OpenAI/Azure OpenAI/Anthropic chat completion)
// by adding another implementation and changing one DI registration in Program.cs.
// The contract stays the same: question + retrieved context in, grounded answer out.
public interface ILlmService
{
    string GenerateAnswer(string question, IReadOnlyList<Chunk> contextChunks);
}
