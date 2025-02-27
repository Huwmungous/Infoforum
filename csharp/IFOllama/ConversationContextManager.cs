using Newtonsoft.Json;

namespace IFOllama
{
    public class ConversationContextManager : IConversationContextManager
    {
        private class ConversationData
        {
            public List<string>? Messages { get; set; }
            public DateTime LastMessageTimestamp { get; set; }
        }

        private class ResponseData
        {
            [JsonProperty("message")]
            public string Message { get; set; } = string.Empty;

            [JsonProperty("timestamp")]
            public DateTime Timestamp { get; set; }

            public ResponseData() { }

            public ResponseData(string message, DateTime timestamp)
            {
                Message = message;
                Timestamp = timestamp;
            }

            public void Serialize(JsonWriter writer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("Message");
                writer.WriteValue(Message);
                writer.WritePropertyName("Timestamp");
                writer.WriteValue(Timestamp.ToString());
                writer.WriteEndObject();
            }
        }

        private readonly string _conversationFolder = Path.Combine(Directory.GetCurrentDirectory(), "Conversations");

        public void Initialize()
        {
            if (!Directory.Exists(_conversationFolder))
                Directory.CreateDirectory(_conversationFolder);
        }

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

        public void SaveResponse(string conversationId, string response)
        {
            // For now, just append the response to the existing messages
            var existingMessages = GetConversation(conversationId) ?? new List<string>();
            existingMessages.Add($"User: {response}");

            // Save conversation data to disk
            SaveConversation(conversationId, existingMessages);
        }

        private void SaveConversation(string conversationId, List<string> messages)
        {
            string folderPath = Path.Combine(_conversationFolder, conversationId);
            Directory.CreateDirectory(folderPath);

            string jsonPath = Path.Combine(folderPath, "conversation.json");

            var conversationData = new ConversationData
            {
                Messages = messages,
                LastMessageTimestamp = DateTime.Now
            };

            using (StreamWriter writer = File.CreateText(jsonPath))
            {
                JsonConvert.SerializeObject(conversationData, Formatting.Indented);
            }

            // Save all responses to disk
            foreach (var message in messages)
            {
                string responseId = Guid.NewGuid().ToString();
                var responsePath = Path.Combine(folderPath, $"{responseId}.json");
                using (StreamWriter writer = File.CreateText(responsePath))
                {
                    var responseData = new ResponseData { Message = message, Timestamp = DateTime.Now };
                    JsonConvert.SerializeObject(responseData, Formatting.Indented);
                }
            }

            _conversationHistories[conversationId] = messages;
        }

        public List<string>? GetConversation(string conversationId)
        {
            if (_conversationHistories.ContainsKey(conversationId))
                return _conversationHistories[conversationId];
            else
                return null;
        }

        public List<string> LoadConversation(string conversationId)
        {
            string jsonPath = Path.Combine(_conversationFolder, $"{conversationId}.json");
            if (File.Exists(jsonPath))
            {
                using (StreamReader reader = File.OpenText(jsonPath))
                {
                    var conversationData = JsonConvert.DeserializeObject<ConversationData>(reader.ReadToEnd());
                    return conversationData?.Messages ?? new List<string>();
                }
            }
            else
                return new List<string>();
        }

        public List<List<string>> LoadAllConversations()
        {
            var allConversations = new List<List<string>>();

            foreach (var directory in Directory.GetDirectories(_conversationFolder))
            {
                string folderPath = directory;
                string jsonPath = Path.Combine(folderPath, "conversation.json");

                if (File.Exists(jsonPath))
                {
                    using (StreamReader reader = File.OpenText(jsonPath))
                    {
                        var conversationData = JsonConvert.DeserializeObject<ConversationData>(reader.ReadToEnd());
                        if (conversationData?.Messages != null)
                        {
                            allConversations.Add(conversationData.Messages);
                        }
                    }
                }
            }

            return allConversations;
        }

        public void DeleteConversation(string conversationId)
        {
            string folderPath = Path.Combine(_conversationFolder, conversationId);

            if (Directory.Exists(folderPath))
                Directory.Delete(folderPath, true);
        }

        public void GradeResponse(string conversationId, double grade)
        {
            // For now, just log the grade
            Console.WriteLine($"Graded response for conversation {conversationId} with grade {grade}");
        }
    }

}