using System.Text.Json;
using System.Text.Json.Serialization;
using IFOllama.RAG;
using Microsoft.Extensions.Logging;

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
            [JsonPropertyName("message")]
            public string Message { get; set; } = string.Empty;

            [JsonPropertyName("timestamp")]
            public DateTime Timestamp { get; set; }

            public ResponseData() { }

            public ResponseData(string message, DateTime timestamp)
            {
                Message = message;
                Timestamp = timestamp;
            }

            public void Serialize(Utf8JsonWriter writer)
            {
                writer.WriteStartObject();
                writer.WriteString("message", Message);
                writer.WriteString("timestamp", Timestamp.ToString("o")); // ISO 8601 format
                writer.WriteEndObject();
            }
        }

        private readonly string _folder = Path.Combine(Directory.GetCurrentDirectory(), "Conversations");
        private readonly ILogger<ConversationContextManager> _logger;

        public ConversationContextManager(ILogger<ConversationContextManager> logger)
        {
            _logger = logger;
            Directory.CreateDirectory(_folder);
        }

        public void Initialize()
        {
            if (!Directory.Exists(_folder))
            {
                Directory.CreateDirectory(_folder);
                _logger.LogInformation($"Created conversation folder at {_folder}");
            }
        }

        public string? GetContext(string conversationId)
        {
            var contextPath = Path.Combine(_folder, conversationId, "context");
            if (!Directory.Exists(contextPath)) return null;

            var files = Directory.EnumerateFiles(contextPath, "*.*", SearchOption.AllDirectories)
                                 .Where(f => f.EndsWith(".cs") || f.EndsWith(".ts") || f.EndsWith(".html") || f.EndsWith(".json"))
                                 .Take(10); // Keep manageable

            var snippets = files.Select(path => $"File: {Path.GetFileName(path)}\n{File.ReadAllText(path)}");
            return string.Join("\n---\n", snippets);
        }

        public void AppendMessage(string conversationId, string role, string message)
        {
            var path = Path.Combine(_folder, $"{conversationId}.json");
            var history = new List<Dictionary<string, string>>();
            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    history = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(existing)
                            ?? new List<Dictionary<string, string>>();
                }
            }
            history.Add(new Dictionary<string, string> { ["role"] = role, ["message"] = message });
            File.WriteAllText(path, JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true }));
        }

        public List<Dictionary<string, string>> GetConversation(string conversationId)
        {
            var path = Path.Combine(_folder, $"{conversationId}.json");
            if (!File.Exists(path)) return new List<Dictionary<string, string>>();
            var content = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<Dictionary<string, string>>>(content)
                ?? new List<Dictionary<string, string>>();
        }

        public List<string> ListConversations()
        {
            return Directory.EnumerateFiles(_folder, "*.json")
                            .Select(Path.GetFileNameWithoutExtension)
                            .ToList();
        }

        public void DeleteConversation(string conversationId)
        {
            var path = Path.Combine(_folder, $"{conversationId}.json");
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation($"Deleted conversation {conversationId}");
            }
        }
    }
}
