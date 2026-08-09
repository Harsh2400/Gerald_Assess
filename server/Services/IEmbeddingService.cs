namespace RagKnowledgeService.Services;

// Swap this for a real provider (OpenAI text-embedding-3-small, Azure OpenAI, etc.)
// by adding another implementation and changing one DI registration in Program.cs.
public interface IEmbeddingService
{
    int Dimensions { get; }
    float[] Embed(string text);
}
