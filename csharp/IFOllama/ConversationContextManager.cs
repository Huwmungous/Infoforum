using Newtonsoft.Json;

namespace IFOllama
{
    public partial class ConversationContextManager : IConversationContextManager
    {

        private readonly string _conversationFolder = Path.Combine(Directory.GetCurrentDirectory(), "Conversations");

        public void Initialize()
        {
            if (!Directory.Exists(_conversationFolder))
                Directory.CreateDirectory(_conversationFolder);
        }

        private readonly IDictionary<string, List<string>> _conversationHistories = new Dictionary<string, List<string>>();

        public void AppendMessage(string conversationId, string role, string message)
        {
            var existingMessages = GetConversation(conversationId) ?? new List<string>();

            // Write individual response to disk immediately
            AppendIndividualMessage(conversationId, role, message);

            existingMessages.Add($"{role}: {message}");

            _conversationHistories[conversationId] = existingMessages;
        }

        private void AppendIndividualMessage(string conversationId, string role, string message)
        {
            string folderPath = Path.Combine(_conversationFolder, conversationId);
            Directory.CreateDirectory(folderPath);

            var responseId = Guid.NewGuid().ToString();
            var responsePath = Path.Combine(folderPath, $"{responseId}.json");

            using (StreamWriter writer = File.CreateText(responsePath))
            {
                var responseData = new ResponseData { Message = message, Timestamp = DateTime.Now };
                JsonConvert.SerializeObject(responseData, Formatting.Indented);
            }

            _conversationHistories[conversationId].Add($"{role}: {message}");
        }

        public string GetContext(string conversationId)
        {
            if (!_conversationHistories.ContainsKey(conversationId))
            {
                // Create a new conversation history for this conversationId
                _conversationHistories[conversationId] = new List<string>();
            }
            // Optionally, perform summarization/truncation here
            return string.Join("\n", _conversationHistories[conversationId]);
        }


        public void SaveResponse(string conversationId, string response)
        {
            var existingMessages = GetConversation(conversationId) ?? new List<string>();
            existingMessages.Add($"User: {response}");

            // Write conversation data to disk
            SaveConversation(conversationId, existingMessages);

            // Save individual response to disk
            AppendIndividualMessage(conversationId, "User", response);
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

            foreach (var message in messages)
            {
                string responseId = Guid.NewGuid().ToString();
                var responsePath = Path.Combine(folderPath, $"{responseId}.json");
                using StreamWriter writer = File.CreateText(responsePath);
                var responseData = new ResponseData { Message = message, Timestamp = DateTime.Now };
                JsonConvert.SerializeObject(responseData, Formatting.Indented);
            }

            _conversationHistories[conversationId] = messages;
        }

        public List<string>? GetConversation(string conversationId)
        {
            if (_conversationHistories.TryGetValue(conversationId, out List<string>? value))
                return value;
            else
                return null;
        }

        public List<string> LoadConversation(string conversationId)
        {
            string jsonPath = Path.Combine(_conversationFolder, $"{conversationId}.json");
            if (File.Exists(jsonPath))
            {
                using StreamReader reader = File.OpenText(jsonPath);
                var conversationData = JsonConvert.DeserializeObject<ConversationData>(reader.ReadToEnd());
                return conversationData?.Messages ?? [];
            }
            else
                return [];
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
                    using StreamReader reader = File.OpenText(jsonPath);
                    var conversationData = JsonConvert.DeserializeObject<ConversationData>(reader.ReadToEnd());
                    if (conversationData?.Messages != null)
                    {
                        allConversations.Add(conversationData.Messages);
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