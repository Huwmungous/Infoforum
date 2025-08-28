using IFOllama.RAG;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace IFOllama.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ConversationsController : ControllerBase
    {
        private readonly IConversationContextManager _ctx;

        public ConversationsController(IConversationContextManager ctx)
        {
            _ctx = ctx;
        }

        [HttpGet]
        public ActionResult<IEnumerable<string>> List() => _ctx.ListConversations();

        public record AppendDto(string ConversationId, string Role, string Message);

        [HttpPost("append")]
        public IActionResult Append([FromBody] AppendDto dto)
        {
            _ctx.AppendMessage(dto.ConversationId, dto.Role, dto.Message);
            return Ok();
        }

        [HttpGet("{id}/history")]
        public ActionResult<IEnumerable<Dictionary<string, string>>> GetHistory(string id)
            => _ctx.GetConversation(id);

        [HttpGet("{id}/context")]
        public ActionResult<string?> GetContext(string id)
            => _ctx.GetContext(id);

        [HttpDelete("{id}")]
        public IActionResult Delete(string id)
        {
            _ctx.DeleteConversation(id);
            return NoContent();
        }
    }
}
