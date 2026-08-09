using Microsoft.AspNetCore.Mvc;
using RagKnowledgeService.Services;

namespace RagKnowledgeService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocsController : ControllerBase
{
    private readonly IKnowledgeStore _store;

    public DocsController(IKnowledgeStore store)
    {
        _store = store;
    }

    // GET /api/docs - lists what's currently indexed, mainly for the demo UI.
    [HttpGet]
    public ActionResult<object> Get()
    {
        var chunks = _store.GetAllChunks();
        return Ok(new
        {
            documentTitles = _store.GetIngestedDocTitles(),
            chunkCount = chunks.Count
        });
    }
}
