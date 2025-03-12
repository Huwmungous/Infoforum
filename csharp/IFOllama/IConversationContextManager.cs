
namespace IFOllama
{
    public interface IConversationContextManager
    {
        void AppendMessage(string conversationId, string role, string message);
        void DeleteConversation(string conversationId);
        string GetContext(string conversationId);
        List<string>? GetConversation(string conversationId);
        void GradeResponse(string conversationId, double grade);
        void Initialize();
        List<List<string>> LoadAllConversations();
        List<string> LoadConversation(string conversationId);
        void SaveResponse(string conversationId, string response);
    }
}