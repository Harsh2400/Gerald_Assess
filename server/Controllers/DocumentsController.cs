using Microsoft.AspNetCore.Mvc;
using RagKnowledgeService.Models;
using RagKnowledgeService.Services;

namespace RagKnowledgeService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentsController(IDocumentService documentService)
    {
        _documentService = documentService;
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
