using System.Collections.Generic;

namespace IFOllama
{
    public interface IConversationContextManager
    {
        void AppendMessage(string conversationId, string role, string message);

        List<Dictionary<string, string>> GetConversation(string conversationId);

        List<string> ListConversations();

        void DeleteConversation(string conversationId);

        void Initialize();

        string? GetContext(string conversationId);
    }
}
