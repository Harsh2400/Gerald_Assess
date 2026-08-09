using Microsoft.AspNetCore.Mvc;
using RagKnowledgeService.Models;
using RagKnowledgeService.Services;

namespace RagKnowledgeService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AskController : ControllerBase
{
    private readonly IAskService _askService;

    public AskController(IAskService askService)
    {
        _askService = askService;
    }

    // POST /api/ask { "question": "...", "topK": 3 }
    [HttpPost]
    public ActionResult<AskResponse> Ask([FromBody] AskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            return BadRequest(new { error = "question is required." });
        }

        var topK = request.TopK is > 0 and <= 10 ? request.TopK : 3;
        var response = _askService.Ask(request.Question.Trim(), topK);
        return Ok(response);
    }
}
