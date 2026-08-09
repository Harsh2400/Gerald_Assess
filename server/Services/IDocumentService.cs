using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

public interface IDocumentService
{
    Task<List<DocumentSummary>> ListAsync();
    Task<DocumentDetail?> GetAsync(string id);
    Task<DocumentDetail> CreateAsync(string title, string content, string sourceType = "manual");
    Task<DocumentDetail?> UpdateAsync(string id, string title, string content);
    Task<bool> DeleteAsync(string id);
    Task<int> SeedFromFolderIfEmptyAsync(string folderPath);
}
