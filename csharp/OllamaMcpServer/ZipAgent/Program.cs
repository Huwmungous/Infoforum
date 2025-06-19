using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;

namespace ZipAgent
{
    public class Program
    {
        static readonly string[] DefaultExtensions = LoadExtensions("settings.txt");

        static async Task Main(string[] args)
        {
            string zipPath = args.Length > 0 ? args[0] : "OllamaMcpServer.zip";
            string extractPath = Path.Combine(Path.GetTempPath(), "ollama_context");

            Console.WriteLine($"Unzipping {zipPath} to {extractPath}...");
            UnzipTo(zipPath, extractPath);

            var codeFiles = Directory.GetFiles(extractPath, "*.*", SearchOption.AllDirectories)
                .Where(f => DefaultExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            var contextBuilder = new StringBuilder();
            foreach (var file in codeFiles)
            {
                contextBuilder.AppendLine($"// File: {Path.GetFileName(file)}");
                var code = File.ReadAllText(file);
                contextBuilder.AppendLine(code);
                contextBuilder.AppendLine("\n\n");
            }

            string userQuestion = "What does the startup configuration do?"; // replace or take as input
            string systemPrompt = "You are a senior developer helping analyze C# and Angular code.";

            string fullPrompt = $"{systemPrompt}\n\nContext:\n{contextBuilder}\n\nQuestion: {userQuestion}";

            string ollamaResponse = await AskOllama(fullPrompt);
            Console.WriteLine("\n=== OLLAMA RESPONSE ===\n");
            Console.WriteLine(ollamaResponse);
        }

        static void UnzipTo(string zipPath, string extractPath)
        {
            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);
            ZipFile.ExtractToDirectory(zipPath, extractPath);
        }

        static string[] LoadExtensions(string path)
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Settings file '{path}' not found. Using default extensions.");
                return [".cs", ".ts", ".html", ".scss", ".csproj", ".sln"];
            }

            return [.. File.ReadAllLines(path)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line) && line.StartsWith('.'))
                .Distinct()];
        }

        static async Task<string> AskOllama(string prompt, string model = "deepseek-coder:33b")
        {
            using var client = new HttpClient();
            var request = new
            {
                model,
                prompt,
                stream = false
            };

            var response = await client.PostAsJsonAsync("http://localhost:11434/api/generate", request);
            if (!response.IsSuccessStatusCode)
            {
                Console.Error.WriteLine($"Error: {response.StatusCode} {await response.Content.ReadAsStringAsync()}");
                return "";
            }

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(contentStream);
            return doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
        }
    }
}
