using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IFOllama.Models;

namespace IFOllama.Services
{
    // Test model: replies with "You previously said: <last user message>"
    public class FakeEchoModel : IChatModel
    {
        public Task<string> GetReplyAsync(IEnumerable<ChatMessage> messages)
        {
            var lastUser = messages.LastOrDefault(m => m.Role == "user")?.Content ?? "(nothing)";
            return Task.FromResult($"You previously said: {lastUser}");
        }
    }
}
