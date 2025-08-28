using System.Collections.Generic;

namespace IFOllama.RAG
{
    public interface IConversationContextManager
    {
        void Initialize();

        void AppendMessage(string conversationId, string role, string message);

        List<Dictionary<string, string>> GetConversation(string conversationId);

        List<string> ListConversations();

        void DeleteConversation(string conversationId);

        /// <summary>
        /// Returns a merged “context”: conversation history (history.json) plus up to 10 code files
        /// from Conversations/{id}/context/**. Returns null if nothing exists.
        /// </summary>
        string? GetContext(string conversationId);
    }
}
