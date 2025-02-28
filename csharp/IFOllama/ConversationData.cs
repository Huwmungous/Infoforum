namespace IFOllama
{
    public partial class ConversationContextManager
    {
        private class ConversationData
        {
            public List<string>? Messages { get; set; }
            public DateTime LastMessageTimestamp { get; set; }
        }
    }

}