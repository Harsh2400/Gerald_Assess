using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RagKnowledgeService.Data;
using RagKnowledgeService.Data.Entities;
using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

// Owns document/chunk persistence and the ingestion pipeline (chunk -> embed ->
// store). Every write refreshes ISearchIndexService so retrieval sees it
// immediately - fine at this corpus size; see README for what replaces the
// full-rebuild-on-write approach at larger scale.
public class DocumentService : IDocumentService
{
    private readonly AppDbContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISearchIndexService _searchIndex;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        AppDbContext db,
        IEmbeddingService embeddingService,
        ISearchIndexService searchIndex,
        ILogger<DocumentService> logger)
    {
        _db = db;
        _embeddingService = embeddingService;
        _searchIndex = searchIndex;
        _logger = logger;
    }

    public async Task<List<DocumentSummary>> ListAsync()
    {
        return await _db.Documents.AsNoTracking()
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => ToSummary(d, d.Chunks.Count))
            .ToListAsync();
    }

    public async Task<DocumentDetail?> GetAsync(string id)
    {
        var doc = await _db.Documents.AsNoTracking()
            .Include(d => d.Chunks)
            .FirstOrDefaultAsync(d => d.Id == id);
        return doc is null ? null : ToDetail(doc);
    }

    public async Task<DocumentDetail> CreateAsync(string title, string content, string sourceType = "manual")
    {
        var normalized = MarkdownChunker.Normalize(content);
        var doc = new DocumentEntity { Title = title, Content = normalized, SourceType = sourceType };
        doc.Chunks = BuildChunks(doc.Id, normalized);

        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();
        await _searchIndex.RefreshAsync();

        return ToDetail(doc);
    }

    public async Task<DocumentDetail?> UpdateAsync(string id, string title, string content)
    {
        var doc = await _db.Documents.Include(d => d.Chunks).FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return null;

        var normalized = MarkdownChunker.Normalize(content);
        doc.Title = title;
        doc.Content = normalized;
        doc.UpdatedAt = DateTime.UtcNow;

        _db.Chunks.RemoveRange(doc.Chunks);
        doc.Chunks = BuildChunks(doc.Id, normalized);

        await _db.SaveChangesAsync();
        await _searchIndex.RefreshAsync();

        return ToDetail(doc);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id);
        if (doc is null) return false;

        _db.Documents.Remove(doc); // cascades to chunks
        await _db.SaveChangesAsync();
        await _searchIndex.RefreshAsync();
        return true;
    }

    public async Task<int> SeedFromFolderIfEmptyAsync(string folderPath)
    {
        if (await _db.Documents.AnyAsync())
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
            .OrderBy(f => f);

        var count = 0;
        foreach (var filePath in files)
        {
            var raw = await File.ReadAllTextAsync(filePath);
            var normalized = MarkdownChunker.Normalize(raw);
            var parsed = MarkdownChunker.Parse(normalized);
            var title = parsed.Title != "Untitled" ? parsed.Title : Path.GetFileNameWithoutExtension(filePath);

            var doc = new DocumentEntity { Title = title, Content = normalized, SourceType = "folder-seed" };
            doc.Chunks = BuildChunks(doc.Id, normalized);
            _db.Documents.Add(doc);
            count++;
        }

        await _db.SaveChangesAsync();
        await _searchIndex.RefreshAsync();

        _logger.LogInformation("Seeded {DocCount} documents from {FolderPath}.", count, folderPath);
        return count;
    }

    private List<ChunkEntity> BuildChunks(string documentId, string normalizedContent)
    {
        var parsed = MarkdownChunker.Parse(normalizedContent);
        var entities = new List<ChunkEntity>();
        var index = 0;

        foreach (var section in parsed.Sections)
        {
            foreach (var piece in MarkdownChunker.SplitLongSection(section.Body))
            {
                var chunkText = $"{parsed.Title} — {section.Heading}\n{piece.Text}";
                var embedding = _embeddingService.Embed(chunkText);

                entities.Add(new ChunkEntity
                {
                    DocumentId = documentId,
                    ChunkIndex = index,
                    Heading = section.Heading,
                    Text = chunkText,
                    StartChar = section.StartChar + piece.StartChar,
                    EndChar = section.StartChar + piece.EndChar,
                    EmbeddingJson = JsonSerializer.Serialize(embedding)
                });
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
