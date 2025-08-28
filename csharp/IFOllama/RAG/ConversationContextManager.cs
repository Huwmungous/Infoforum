using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace IFOllama.RAG
{
    public class ConversationContextManager : IConversationContextManager
    {
        private readonly ILogger<ConversationContextManager> _logger;
        private readonly string _baseFolder;
        private readonly int _maxContextFiles;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly object _ioLock = new(); // simple process-wide guard

        public ConversationContextManager(
            ILogger<ConversationContextManager> logger,
            IOptions<ConversationStorageOptions> options,
            IWebHostEnvironment env
        )
        {
            _logger = logger;

            var cfg = options.Value ?? new ConversationStorageOptions();

            // Resolve BasePath against ContentRoot if it's not rooted
            _baseFolder = Path.IsPathRooted(cfg.BasePath)
                ? cfg.BasePath
                : Path.GetFullPath(Path.Combine(env.ContentRootPath, cfg.BasePath));

            _maxContextFiles = Math.Max(1, cfg.MaxContextFiles);

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true
            };
        }

        public void Initialize()
        {
            lock(_ioLock)
            {
                Directory.CreateDirectory(_baseFolder);
            }
            _logger.LogInformation("Using conversation base folder at {Folder}", _baseFolder);
        }

        public void AppendMessage(string conversationId, string role, string message)
        {
            if(string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("conversationId is required", nameof(conversationId));

            var convDir = GetConversationDir(conversationId);
            var historyPath = Path.Combine(convDir, "history.json");

            lock(_ioLock)
            {
                Directory.CreateDirectory(convDir);

                var history = File.Exists(historyPath)
                    ? DeserializeHistory(historyPath)
                    : new List<Dictionary<string, string>>();

                history.Add(new Dictionary<string, string>
                {
                    ["role"] = role,
                    ["message"] = message
                });

                File.WriteAllText(historyPath, JsonSerializer.Serialize(history, _jsonOptions));
            }

            _logger.LogDebug("Appended message to {HistoryPath}", historyPath);
        }

        public List<Dictionary<string, string>> GetConversation(string conversationId)
        {
            if(string.IsNullOrWhiteSpace(conversationId))
                return new();

            var convDir = GetConversationDir(conversationId);
            var historyPath = Path.Combine(convDir, "history.json");

            lock(_ioLock)
            {
                return File.Exists(historyPath)
                    ? DeserializeHistory(historyPath)
                    : new();
            }
        }

        public List<string> ListConversations()
        {
            lock(_ioLock)
            {
                if(!Directory.Exists(_baseFolder)) return new();

                return Directory.EnumerateDirectories(_baseFolder)
                                .Select(Path.GetFileName)
                                .Where(n => !string.IsNullOrWhiteSpace(n))
                                .OrderBy(n => n)
                                .ToList()!;
            }
        }

        public void DeleteConversation(string conversationId)
        {
            if(string.IsNullOrWhiteSpace(conversationId))
                return;

            var convDir = GetConversationDir(conversationId);

            lock(_ioLock)
            {
                if(Directory.Exists(convDir))
                {
                    Directory.Delete(convDir, recursive: true);
                    _logger.LogInformation("Deleted conversation {ConversationId} at {Path}", conversationId, convDir);
                }
            }
        }

        public string? GetContext(string conversationId)
        {
            if(string.IsNullOrWhiteSpace(conversationId))
                return null;

            var convDir = GetConversationDir(conversationId);
            var historyPath = Path.Combine(convDir, "history.json");
            var codeDir = Path.Combine(convDir, "context");

            var parts = new List<string>();

            lock(_ioLock)
            {
                if(File.Exists(historyPath))
                {
                    parts.Add($"File: history.json\n{File.ReadAllText(historyPath)}");
                }

                if(Directory.Exists(codeDir))
                {
                    var files = Directory.EnumerateFiles(codeDir, "*.*", SearchOption.AllDirectories)
                        .Where(f =>
                            f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".ts", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                            f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        .Take(_maxContextFiles);

                    foreach(var file in files)
                    {
                        parts.Add($"File: {Path.GetFileName(file)}\n{File.ReadAllText(file)}");
                    }
                }
            }

            return parts.Count == 0 ? (string?)null : string.Join("\n---\n", parts);
        }

        private string GetConversationDir(string conversationId)
            => Path.Combine(_baseFolder, Sanitize(conversationId));

        private static string Sanitize(string name)
        {
            // basic filesystem safety; keep it simple
            foreach(var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        private List<Dictionary<string, string>> DeserializeHistory(string historyPath)
        {
            var content = File.ReadAllText(historyPath);
            return JsonSerializer.Deserialize<List<Dictionary<string, string>>>(content) ?? new();
        }
    }
}
