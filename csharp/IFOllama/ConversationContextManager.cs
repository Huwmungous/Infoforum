namespace IFOllama
{
    public class ConversationContextManager
    {
        private readonly IDictionary<string, List<string>> _conversationHistories = new Dictionary<string, List<string>>();

        public void AppendMessage(string conversationId, string role, string message)
        {
            if (!_conversationHistories.ContainsKey(conversationId))
                _conversationHistories[conversationId] = new List<string>();

            _conversationHistories[conversationId].Add($"{role}: {message}");
        }

        public string GetContext(string conversationId)
        {
            if (!_conversationHistories.ContainsKey(conversationId))
                return string.Empty;
            // Optionally, perform summarization/truncation here
            return string.Join("\n", _conversationHistories[conversationId]);
        }
    }

}
