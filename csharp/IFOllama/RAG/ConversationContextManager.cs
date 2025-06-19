using System.Text.Json.Serialization;
using System.Text.Json;
using IFOllama.RAG;

namespace IFOllama
{
    public partial class ConversationContextManager : IConversationContextManager
    {
        private readonly string _conversationFolder = Path.Combine(Directory.GetCurrentDirectory(), "Conversations");
        private readonly IDictionary<string, List<string>> _conversationHistories = new Dictionary<string, List<string>>();
        private readonly ILogger<ConversationContextManager> _logger;

        private static readonly JsonSerializerOptions IndentedJson = new()
        {
            WriteIndented = true
        };

        public ConversationContextManager(ILogger<ConversationContextManager> logger)
        {
            _logger = logger;
        }

        public void Initialize()
        {
            if (!Directory.Exists(_conversationFolder))
            {
                Directory.CreateDirectory(_conversationFolder);
                _logger.LogInformation($"Created conversation folder at {_conversationFolder}");
            }
        }

        public void AppendMessage(string conversationId, string role, string message)
        {
            var existingMessages = GetConversation(conversationId) ?? new List<string>();

            // Write individual response to disk immediately
            AppendIndividualMessage(conversationId, role, message);

            existingMessages.Add($"{role}: {message}");
            _conversationHistories[conversationId] = existingMessages;

            _logger.LogInformation($"Appended message to conversation {conversationId}: {role}: {message}");
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
                writer.Write(JsonSerializer.Serialize(responseData, IndentedJson));
            }

            _conversationHistories[conversationId].Add($"{role}: {message}");
            _logger.LogInformation($"Saved individual message for conversation {conversationId} at {responsePath}");
        }

        public string GetContext(string conversationId)
        {
            if (!_conversationHistories.ContainsKey(conversationId))
            {
                _conversationHistories[conversationId] = new List<string>();
                _logger.LogInformation($"Created new conversation history for {conversationId}");
            }

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

            _logger.LogInformation($"Saved response for conversation {conversationId}: {response}");
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
                writer.Write(JsonSerializer.Serialize(conversationData, IndentedJson));
            }

            _logger.LogInformation($"Saved conversation {conversationId} to {jsonPath}");
        }

        public List<string>? GetConversation(string conversationId)
        {
            if (_conversationHistories.TryGetValue(conversationId, out List<string>? value))
                return value;

            _logger.LogWarning($"No conversation found for {conversationId}");
            return null;
        }

        public List<string> LoadConversation(string conversationId)
        {
            string jsonPath = Path.Combine(_conversationFolder, $"{conversationId}.json");
            if (File.Exists(jsonPath))
            {
                using StreamReader reader = File.OpenText(jsonPath);
                var conversationData = JsonSerializer.Deserialize<ConversationData>(reader.ReadToEnd());
                _logger.LogInformation($"Loaded conversation {conversationId} from {jsonPath}");
                return conversationData?.Messages ?? new List<string>();
            }

            _logger.LogWarning($"No conversation file found for {conversationId} at {jsonPath}");
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
                    using StreamReader reader = File.OpenText(jsonPath);
                    var conversationData = JsonSerializer.Deserialize<ConversationData>(reader.ReadToEnd());
                    if (conversationData?.Messages != null)
                    {
                        allConversations.Add(conversationData.Messages);
                        _logger.LogInformation($"Loaded conversation from {jsonPath}");
                    }
                }
            }

            return allConversations;
        }

        public void DeleteConversation(string conversationId)
        {
            string folderPath = Path.Combine(_conversationFolder, conversationId);

            if (Directory.Exists(folderPath))
            {
                Directory.Delete(folderPath, true);
                _logger.LogInformation("Deleted conversation {conversationId} at {folderPath}", conversationId, folderPath);
            }
            else
            {
                _logger.LogWarning("Attempted to delete non-existent conversation {conversationId}", conversationId);
            }
        }

        public void GradeResponse(string conversationId, double grade)
        {
            _logger.LogInformation($"Graded response for conversation {conversationId} with grade {grade}", conversationId, grade);
        }
    }
}
