using Microsoft.AspNetCore.Mvc;
using RagKnowledgeService.Models;
using RagKnowledgeService.Services;

namespace RagKnowledgeService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;
    private readonly IDocumentFileExtractor _fileExtractor;

    public DocumentsController(IDocumentService documentService, IDocumentFileExtractor fileExtractor)
    {
        _documentService = documentService;
        _fileExtractor = fileExtractor;
    }

    // GET /api/documents
    [HttpGet]
    public async Task<ActionResult<List<DocumentSummary>>> List() =>
        Ok(await _documentService.ListAsync());

    // GET /api/documents/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<DocumentDetail>> Get(string id)
    {
        var doc = await _documentService.GetAsync(id);
        return doc is null ? NotFound() : Ok(doc);
    }

    // POST /api/documents { "title": "...", "content": "..." }
    // Chunks, embeds, and indexes the document immediately.
    [HttpPost]
    public async Task<ActionResult<DocumentDetail>> Create([FromBody] CreateDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "title and content are required." });
        }

        var doc = await _documentService.CreateAsync(request.Title.Trim(), request.Content);
        return CreatedAtAction(nameof(Get), new { id = doc.Id }, doc);
    }

    // POST /api/documents/upload (multipart/form-data, field name "file").
    // Extracted content enters the same chunk/embed/index pipeline as manual content.
    [HttpPost("upload")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<DocumentDetail>> Upload(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Choose a non-empty file to upload." });

        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { error = "Files must be 10 MB or smaller." });

        try
        {
            var content = await _fileExtractor.ExtractAsync(file, cancellationToken);
            if (string.IsNullOrWhiteSpace(content))
                return BadRequest(new { error = "No readable text was found in this file." });

            var title = Path.GetFileNameWithoutExtension(file.FileName).Trim();
            if (string.IsNullOrWhiteSpace(title)) title = "Uploaded document";
            var sourceType = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
            var doc = await _documentService.CreateAsync(title, content, sourceType);
            return CreatedAtAction(nameof(Get), new { id = doc.Id }, doc);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return BadRequest(new { error = "The file could not be read. It may be damaged or protected." });
        }
    }

    // PUT /api/documents/{id} { "title": "...", "content": "..." }
    // Replaces all of this document's chunks and re-embeds them.
    [HttpPut("{id}")]
    public async Task<ActionResult<DocumentDetail>> Update(string id, [FromBody] UpdateDocumentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "title and content are required." });
        }

        var doc = await _documentService.UpdateAsync(id, request.Title.Trim(), request.Content);
        return doc is null ? NotFound() : Ok(doc);
    }

    // DELETE /api/documents/{id} - cascades to all of its chunks.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await _documentService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
