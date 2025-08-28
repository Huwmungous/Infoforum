using IFOllama.RAG;
using IFOllama.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace IFOllama.Controllers;

[ApiController]
[Route("api/diag")]
public class DiagController : ControllerBase
{
    private readonly IConversationContextManager _ctx;
    private readonly IChatModel _model;

    public DiagController(IConversationContextManager ctx, IChatModel model)
    {
        _ctx = ctx;
        _model = model;
    }

    [HttpGet("whoami")]
    public IActionResult WhoAmI([FromQuery] string? conversationId = null)
    {
        // Make both branches the same type (List<Dictionary<string,string>>)
        List<Dictionary<string, string>> list = string.IsNullOrWhiteSpace(conversationId)
            ? new List<Dictionary<string, string>>()
            : _ctx.GetConversation(conversationId);

        return Ok(new
        {
            ModelType = _model.GetType().FullName,
            ConversationsCount = _ctx.ListConversations().Count,
            HistoryCount = list.Count
        });
    }
}
