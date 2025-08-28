using IFOllama.Models;
using IFOllama.RAG;
using IFOllama.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace IFOllama.Controllers
{
    [ApiController]
    [Route("api/chat")]
    public class ChatController : ControllerBase
    {
        private readonly IConversationContextManager _ctx;
        private readonly IChatModel _model;

        public ChatController(IConversationContextManager ctx, IChatModel model)
        {
            _ctx = ctx;
            _model = model;
        }

        public record ChatRequest(string ConversationId, string Message, string? SystemPrompt = null);

        [HttpPost]
        public async Task<ActionResult<string>> Post([FromBody] ChatRequest req)
        {
            if(string.IsNullOrWhiteSpace(req.ConversationId))
                return BadRequest("ConversationId is required.");
            if(string.IsNullOrWhiteSpace(req.Message))
                return BadRequest("Message is required.");

            // 1) Load prior history from disk
            var raw = _ctx.GetConversation(req.ConversationId); // List<Dictionary<string,string>>
            var history = ToChatMessages(raw);

            // 2) Optional system prompt at the start (only once, if provided)
            if(!string.IsNullOrWhiteSpace(req.SystemPrompt) && !history.Any(m => m.Role == "system"))
            {
                history.Insert(0, new ChatMessage { Role = "system", Content = req.SystemPrompt! });
            }

            // 3) Add current user message (also persist it immediately)
            var userMsg = new ChatMessage { Role = "user", Content = req.Message };
            history.Add(userMsg);
            _ctx.AppendMessage(req.ConversationId, "user", req.Message);

            // 4) Ask the model with the FULL history (now includes the new user turn)
            var assistantReply = await _model.GetReplyAsync(history);

            // 5) Persist assistant reply
            _ctx.AppendMessage(req.ConversationId, "assistant", assistantReply);

            // 6) Return reply
            return Ok(assistantReply);
        }

        private static List<ChatMessage> ToChatMessages(List<Dictionary<string, string>> raw)
        {
            var list = new List<ChatMessage>(raw.Count);
            foreach(var d in raw)
            {
                d.TryGetValue("role", out var role);
                d.TryGetValue("message", out var content);
                if(!string.IsNullOrWhiteSpace(role) && content is not null)
                    list.Add(new ChatMessage { Role = role!, Content = content });
            }
            return list;
        }
    }
}
