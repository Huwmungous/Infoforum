using System.Collections.Generic;
using System.Threading.Tasks;
using IFOllama.Models;

namespace IFOllama.Services
{
    public class OllamaChatModel : IChatModel
    {
        // Inject your HTTP client / SDK here
        public Task<string> GetReplyAsync(IEnumerable<ChatMessage> messages)
        {
            // TODO: call your Ollama endpoint with the full `messages` list in order.
            // Pseudocode:
            // var req = new { model = "deepseek-coder", messages = messages.Select(m => new { role = m.Role, content = m.Content }) };
            // var resp = await _http.PostAsJsonAsync("/api/chat", req);
            // var text = await resp.Content.ReadFromJsonAsync<YourResp>();
            // return text.Message;
            return Task.FromResult("TODO: wire Ollama here.");
        }
    }
}
