
using System.Text.Json;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IFOllama.RAG
{
    public class ConversationContextManager : IConversationContextManager
    {
        private readonly ILogger<ConversationContextManager> _logger;
        private readonly string _folder;
        private readonly JsonSerializerOptions _jsonSerializerOptions; // Cached instance  

        public ConversationContextManager(ILogger<ConversationContextManager> logger)
        {
            _logger = logger;
            _folder = Path.Combine(Directory.GetCurrentDirectory(), "Conversations");
            _jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true }; // Initialize cached instance  
        }

        public void Initialize()
        {
            if (!Directory.Exists(_folder))
            {
                Directory.CreateDirectory(_folder);
                _logger.LogInformation("Created conversation folder at {_folder}", _folder);
            }
        }

        public void AppendMessage(string conversationId, string role, string message)
        {
            var path = Path.Combine(_folder, $"{conversationId}.json");
            var history = GetConversation(conversationId);
            history.Add(new Dictionary<string, string> { ["role"] = role, ["message"] = message });

            File.WriteAllText(path, JsonSerializer.Serialize(history, _jsonSerializerOptions)); // Use cached instance  
        }

        public List<Dictionary<string, string>> GetConversation(string conversationId)
        {
            var path = Path.Combine(_folder, $"{conversationId}.json");
            if (!File.Exists(path))
                return [];

            var content = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<Dictionary<string, string>>>(content) ?? [];
        }

        public List<string> ListConversations()
        {
            return [.. Directory.EnumerateFiles(_folder, "*.json")
                            .Select(Path.GetFileNameWithoutExtension)
                            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!)];
        }

        public void DeleteConversation(string conversationId)
        {
            var path = Path.Combine(_folder, $"{conversationId}.json");
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation("Deleted conversation {ConversationId}", conversationId);
            }
        }

        public string? GetContext(string conversationId)
        {
            var contextPath = Path.Combine(_folder, conversationId, "context");
            if (!Directory.Exists(contextPath)) return null;

            var files = Directory.EnumerateFiles(contextPath, "*.*", SearchOption.AllDirectories)
                                 .Where(f => f.EndsWith(".cs") || f.EndsWith(".ts") || f.EndsWith(".html") || f.EndsWith(".json"))
                                 .Take(10);

            var snippets = files.Select(path => $"File: {Path.GetFileName(path)}\n{File.ReadAllText(path)}");
            return string.Join("\n---\n", snippets);
        }
    }
}
