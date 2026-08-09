using Microsoft.AspNetCore.Mvc;
using RagKnowledgeService.Models;
using RagKnowledgeService.Services;

namespace RagKnowledgeService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChunksController : ControllerBase
{
    private readonly IChunkService _chunkService;

    public ChunksController(IChunkService chunkService)
    {
        _chunkService = chunkService;
    }

    // GET /api/chunks?documentId=... - omit documentId to list every indexed chunk.
    [HttpGet]
    public async Task<ActionResult<List<ChunkSummary>>> List([FromQuery] string? documentId) =>
        Ok(await _chunkService.ListAsync(documentId));

    // GET /api/chunks/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<ChunkSummary>> Get(string id)
    {
        var chunk = await _chunkService.GetAsync(id);
        return chunk is null ? NotFound() : Ok(chunk);
    }

    // PUT /api/chunks/{id} { "text": "..." } - re-embeds this chunk only.
    // Offsets into the parent document are reset (-1/-1): they're no longer
    // trustworthy once the chunk's text diverges from the source document.
    [HttpPut("{id}")]
    public async Task<ActionResult<ChunkSummary>> UpdateText(string id, [FromBody] UpdateChunkRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest(new { error = "text is required." });
        }

        var chunk = await _chunkService.UpdateTextAsync(id, request.Text);
        return chunk is null ? NotFound() : Ok(chunk);
    }

    // DELETE /api/chunks/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var deleted = await _chunkService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
