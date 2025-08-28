using System.Collections.Generic;
using System.Threading.Tasks;
using IFOllama.Models;

namespace IFOllama.Services
{
    public interface IChatModel
    {
        Task<string> GetReplyAsync(IEnumerable<ChatMessage> messages);
    }
}
