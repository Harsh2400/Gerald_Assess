using RagKnowledgeService.Models;

namespace RagKnowledgeService.Services;

public interface IIngestionService
{
    Task IngestFolderAsync(string folderPath);
}

// Reads every .md/.txt file in the docs folder, splits each into section-level
// chunks, embeds each chunk, and stores the result. Runs once at startup.
public class IngestionService : IIngestionService
{
    private readonly IKnowledgeStore _store;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<IngestionService> _logger;

    public IngestionService(
        IKnowledgeStore store,
        IEmbeddingService embeddingService,
        ILogger<IngestionService> logger)
    {
        _store = store;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    public Task IngestFolderAsync(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            _logger.LogWarning("Docs folder {FolderPath} does not exist; nothing to ingest.", folderPath);
            return Task.CompletedTask;
        }

        var files = Directory.GetFiles(folderPath)
            .Where(f => f.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f);

        var chunkCount = 0;
        var docCount = 0;

        foreach (var filePath in files)
        {
            var docId = Path.GetFileNameWithoutExtension(filePath);
            var text = File.ReadAllText(filePath);
            var parsed = MarkdownChunker.Parse(text);
            docCount++;

            foreach (var (heading, body) in parsed.Sections)
            {
                foreach (var piece in MarkdownChunker.SplitLongSection(body))
                {
                    var chunkText = $"{parsed.Title} — {heading}\n{piece}";
                    var embedding = _embeddingService.Embed(chunkText);

                    _store.AddChunk(new Chunk
                    {
                        Id = $"{docId}#{chunkCount}",
                        DocId = docId,
                        DocTitle = parsed.Title,
                        Text = chunkText,
                        Embedding = embedding
                    });
                    chunkCount++;
                }
            }
        }

        _logger.LogInformation(
            "Ingested {DocCount} documents into {ChunkCount} chunks from {FolderPath}.",
            docCount, chunkCount, folderPath);

        return Task.CompletedTask;
    }
}
