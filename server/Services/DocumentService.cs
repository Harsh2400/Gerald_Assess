using RagKnowledgeService.Data.Entities;
using RagKnowledgeService.Models;
using RagKnowledgeService.Repositories;

namespace RagKnowledgeService.Services;

// Owns the document ingestion pipeline (chunk -> embed -> store) and maps
// entities to API DTOs. Data access itself lives in IDocumentRepository;
// SQLite (via the repository) is the source of truth for text/metadata,
// Qdrant holds the vectors keyed by chunk ID. Every write refreshes
// ISearchIndexService (BM25 + in-memory chunk metadata) so retrieval sees it
// immediately - fine at this corpus size; see README for what replaces the
// full-rebuild-on-write approach at larger scale.
public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IChunkRepository _chunkRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStore _vectorStore;
    private readonly ISearchIndexService _searchIndex;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        IDocumentRepository documentRepository,
        IChunkRepository chunkRepository,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore,
        ISearchIndexService searchIndex,
        ILogger<DocumentService> logger)
    {
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
        _searchIndex = searchIndex;
        _logger = logger;
    }

    public async Task<List<DocumentSummary>> ListAsync()
    {
        var docs = await _documentRepository.GetAllAsync();
        return docs.Select(d => ToSummary(d, d.Chunks.Count)).ToList();
    }

    public async Task<DocumentDetail?> GetAsync(string id)
    {
        var doc = await _documentRepository.GetByIdAsync(id, includeChunks: true);
        return doc is null ? null : ToDetail(doc);
    }

    public async Task<DocumentDetail> CreateAsync(string title, string content, string sourceType = "manual")
    {
        var normalized = MarkdownChunker.Normalize(content);
        var doc = new DocumentEntity { Title = title, Content = normalized, SourceType = sourceType };
        doc.Chunks = await BuildChunksAsync(doc.Id, normalized);

        _documentRepository.Add(doc);
        await _documentRepository.SaveChangesAsync();
        await _searchIndex.RefreshAsync();

        return ToDetail(doc);
    }

    public async Task<DocumentDetail?> UpdateAsync(string id, string title, string content)
    {
        var doc = await _documentRepository.GetByIdAsync(id, includeChunks: true);
        if (doc is null) return null;

        var normalized = MarkdownChunker.Normalize(content);
        doc.Title = title;
        doc.Content = normalized;
        doc.UpdatedAt = DateTime.UtcNow;

        _chunkRepository.RemoveRange(doc.Chunks.ToList());
        doc.Chunks.Clear();
        await _vectorStore.DeleteByDocumentIdAsync(doc.Id);
        doc.Chunks = await BuildChunksAsync(doc.Id, normalized);

        await _documentRepository.SaveChangesAsync();
        await _searchIndex.RefreshAsync();

        return ToDetail(doc);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var doc = await _documentRepository.GetByIdAsync(id);
        if (doc is null) return false;

        _documentRepository.Remove(doc); // cascades to chunks in SQLite
        await _documentRepository.SaveChangesAsync();
        await _vectorStore.DeleteByDocumentIdAsync(id);
        await _searchIndex.RefreshAsync();
        return true;
    }

    public async Task<int> SeedFromFolderIfEmptyAsync(string folderPath)
    {
        if (await _documentRepository.AnyAsync())
        {
            await _searchIndex.RefreshAsync();
            return 0;
        }

        if (!Directory.Exists(folderPath))
        {
            _logger.LogWarning("Seed folder {FolderPath} does not exist; nothing to seed.", folderPath);
            return 0;
        }

        var files = Directory.GetFiles(folderPath)
            .Where(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();

        var count = 0;
        foreach (var filePath in files)
        {
            var raw = await File.ReadAllTextAsync(filePath);
            var normalized = MarkdownChunker.Normalize(raw);
            var parsed = MarkdownChunker.Parse(normalized);
            var title = parsed.Title != "Untitled" ? parsed.Title : Path.GetFileNameWithoutExtension(filePath);

            var doc = new DocumentEntity { Title = title, Content = normalized, SourceType = "folder-seed" };
            doc.Chunks = await BuildChunksAsync(doc.Id, normalized);
            _documentRepository.Add(doc);
            count++;

            _logger.LogInformation("Seeded document {Index}/{Total}: {Title} ({ChunkCount} chunks).",
                count, files.Count, title, doc.Chunks.Count);
        }

        await _documentRepository.SaveChangesAsync();
        await _searchIndex.RefreshAsync();

        _logger.LogInformation("Seeded {DocCount} documents from {FolderPath}.", count, folderPath);
        return count;
    }

    private async Task<List<ChunkEntity>> BuildChunksAsync(string documentId, string normalizedContent)
    {
        var parsed = MarkdownChunker.Parse(normalizedContent);
        var entities = new List<ChunkEntity>();
        var index = 0;

        foreach (var section in parsed.Sections)
        {
            foreach (var piece in MarkdownChunker.SplitLongSection(section.Body))
            {
                var chunkText = $"{parsed.Title} — {section.Heading}\n{piece.Text}";
                var embedding = await _embeddingService.EmbedAsync(chunkText);

                var chunk = new ChunkEntity
                {
                    DocumentId = documentId,
                    ChunkIndex = index,
                    Heading = section.Heading,
                    Text = chunkText,
                    StartChar = section.StartChar + piece.StartChar,
                    EndChar = section.StartChar + piece.EndChar
                };

                await _vectorStore.UpsertAsync(chunk.Id, documentId, embedding);
                entities.Add(chunk);
                index++;
            }
        }

        return entities;
    }

    private static DocumentSummary ToSummary(DocumentEntity doc, int chunkCount) => new()
    {
        Id = doc.Id,
        Title = doc.Title,
        SourceType = doc.SourceType,
        ChunkCount = chunkCount,
        CreatedAt = doc.CreatedAt,
        UpdatedAt = doc.UpdatedAt
    };

    private static DocumentDetail ToDetail(DocumentEntity doc) => new()
    {
        Id = doc.Id,
        Title = doc.Title,
        SourceType = doc.SourceType,
        ChunkCount = doc.Chunks.Count,
        CreatedAt = doc.CreatedAt,
        UpdatedAt = doc.UpdatedAt,
        Content = doc.Content,
        Chunks = doc.Chunks
            .OrderBy(c => c.ChunkIndex)
            .Select(c => new ChunkSummary
            {
                Id = c.Id,
                DocumentId = c.DocumentId,
                ChunkIndex = c.ChunkIndex,
                Heading = c.Heading,
                Text = c.Text,
                StartChar = c.StartChar,
                EndChar = c.EndChar,
                UpdatedAt = c.UpdatedAt
            }).ToList()
    };
}
