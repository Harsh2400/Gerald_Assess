using Microsoft.AspNetCore.Mvc;
using RagKnowledgeService.Models;
using RagKnowledgeService.Services;

namespace RagKnowledgeService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AskController : ControllerBase
{
    private readonly IRagQueryService _ragQueryService;

    public AskController(IRagQueryService ragQueryService)
    {
        _ragQueryService = ragQueryService;
    }

    // POST /api/ask { "question": "...", "topK": 3 } - stateless one-shot Q&A,
    // no conversation persisted. Use /api/chat for a persisted conversation.
    [HttpPost]
    public ActionResult<AskResponse> Ask([FromBody] AskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { error = "question is required." });
        }

        var topK = request.TopK is > 0 and <= 10 ? request.TopK : 3;
        var response = _ragQueryService.Answer(request.Question.Trim(), topK);
        return Ok(response);
    }
}
