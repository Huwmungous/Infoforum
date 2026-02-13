using IFOllama.Classes.Models;
using IFOllama.WebService.Models;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace IFOllama.WebService.Services;

public class OllamaService(
    HttpClient httpClient,
    McpRouterService mcpRouter,
    FileStorageService fileStorage,
    ILogger<OllamaService> logger,
    IConfiguration config)
{
    private readonly string _ollamaBaseUrl = config["Ollama:BaseUrl"] ?? throw new InvalidOperationException("Ollama:BaseUrl not configured");
    private readonly string _defaultModel = config["Ollama:Model"] ?? "qwen2.5:32b";

    public async Task<string> GenerateTitleAsync(string userMessage, string? model = null)
    {
        model ??= _defaultModel;

        var messages = new List<OllamaMessage>
        {
            new()
            {
                Role = "system",
                Content = "You are a title generator. Given a user's message, generate a short, descriptive title (maximum 8 words) that summarises the topic. Respond with ONLY the title, no quotes, no punctuation at the end, no explanation."
            },
            new()
            {
                Role = "user",
                Content = userMessage
            }
        };

        var request = new OllamaChatRequest
        {
            Model = model,
            Messages = messages,
            Stream = false
        };

        logger.LogInformation("Generating title for conversation using model {Model}", model);

        var response = await httpClient.PostAsJsonAsync($"{_ollamaBaseUrl}/api/chat", request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            logger.LogError("Ollama title generation error: {Error}", error);
            throw new InvalidOperationException($"Ollama API error: {error}");
        }

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
        var title = result?.Message?.Content?.Trim() ?? userMessage[..Math.Min(50, userMessage.Length)];

        // Strip any <think>...</think> blocks from the title
        var thinkPattern = new System.Text.RegularExpressions.Regex(@"<think>[\s\S]*?</think>");
        title = thinkPattern.Replace(title, "").Trim();

        // Clean up: remove surrounding quotes if present
        if ((title.StartsWith('"') && title.EndsWith('"')) ||
            (title.StartsWith('\'') && title.EndsWith('\'')))
        {
            title = title[1..^1];
        }

        // Ensure it's not too long
        if (title.Length > 80)
        {
            title = title[..77] + "...";
        }

        logger.LogInformation("Generated title: {Title}", title);
        return title;
    }

    public async Task<string> ChatWithToolsAsync(
        string userMessage,
        List<OllamaMessage> history,
        string? model = null,
        List<FileAttachment>? attachments = null)
    {
        model ??= _defaultModel;

        var tools = await mcpRouter.GetAllToolsAsync();
        logger.LogInformation("Loaded {Count} tools for Ollama", tools.Count);

        var ollamaTools = tools.Select(t => new OllamaTool
        {
            Type = "function",
            Function = new OllamaFunction
            {
                Name = $"{t.ServerName}__{t.Name}",
                Description = t.Description,
                Parameters = t.InputSchema
            }
        }).ToList();

        var messages = new List<OllamaMessage>(history);

        var userOllamaMessage = new OllamaMessage
        {
            Role = "user",
            Content = userMessage
        };

        if (attachments != null && attachments.Count > 0)
        {
            var (images, combinedText) = await ProcessAttachmentsAsync(attachments);

            if (!string.IsNullOrWhiteSpace(combinedText))
            {
                userOllamaMessage.Content += "\n\n" + combinedText;
            }

            if (images.Count > 0)
            {
                userOllamaMessage.Images = images;
            }

            logger.LogInformation("Added {Count} image(s) to user message", userOllamaMessage.Images?.Count ?? 0);
        }

        messages.Add(userOllamaMessage);

        const int maxIterations = 10;
        var iteration = 0;

        while (iteration < maxIterations)
        {
            iteration++;
            logger.LogInformation("Ollama iteration {Iteration}", iteration);

            var request = new OllamaChatRequest
            {
                Model = model,
                Messages = messages,
                Tools = ollamaTools,
                Stream = false
            };

            var response = await httpClient.PostAsJsonAsync(
                $"{_ollamaBaseUrl}/api/chat",
                request);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                logger.LogError("Ollama API error: {Error}", error);
                throw new InvalidOperationException($"Ollama API error: {error}");
            }

            var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();

            if (result?.Message == null)
            {
                throw new InvalidOperationException("No message in Ollama response");
            }

            messages.Add(result.Message);

            if (result.Message.ToolCalls == null || result.Message.ToolCalls.Count == 0)
            {
                logger.LogInformation("Ollama completed with final response");
                return result.Message.Content ?? string.Empty;
            }

            logger.LogInformation("Executing {Count} tool calls", result.Message.ToolCalls.Count);

            foreach (var toolCall in result.Message.ToolCalls)
            {
                try
                {
                    logger.LogInformation("Executing tool call: {Tool}", toolCall.Function.Name);

                    var parts = toolCall.Function.Name.Split("__");
                    if (parts.Length != 2)
                    {
                        throw new InvalidOperationException($"Invalid tool name format: {toolCall.Function.Name}");
                    }

                    var serverName = parts[0];
                    var toolName = parts[1];

                    var toolResult = await mcpRouter.CallToolAsync(
                        serverName,
                        toolName,
                        toolCall.Function.Arguments);

                    logger.LogInformation("Tool {Tool} returned result: {Result}",
                        toolCall.Function.Name,
                        toolResult.Length > 200 ? toolResult[..200] + "..." : toolResult);

                    messages.Add(new OllamaMessage
                    {
                        Role = "tool",
                        Content = toolResult
                    });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error executing tool {Tool}", toolCall.Function.Name);

                    messages.Add(new OllamaMessage
                    {
                        Role = "tool",
                        Content = JsonSerializer.Serialize(new
                        {
                            error = ex.Message,
                            tool = toolCall.Function.Name
                        })
                    });
                }
            }
        }

        throw new InvalidOperationException($"Max iterations ({maxIterations}) reached in tool calling loop");
    }

    public async Task<(List<string> Images, string CombinedText)> ProcessAttachmentsAsync(
       List<FileAttachment> attachments)
    {
        var images = new List<string>();
        var combinedText = new StringBuilder();

        foreach (var attachment in attachments)
        {
            logger.LogInformation(
                "Processing attachment: {FileName}, FileType={FileType}, ContentType={ContentType}, Path={Path}",
                attachment.FileName, attachment.FileType, attachment.ContentType, attachment.StoragePath);

            try
            {
                switch (attachment.FileType)
                {
                    case FileContentType.Image:
                        {
                            var base64Image = fileStorage.GetBase64Content(attachment.StoragePath);
                            images.Add(base64Image);

                            logger.LogInformation("Processed image: {FileName}", attachment.FileName);
                            break;
                        }

                    case FileContentType.Text:
                    case FileContentType.Document:
                        {
                            var textContent = await fileStorage.ReadTextFileAsync(attachment.StoragePath);

                            combinedText.AppendLine($"""
                        --- Begin attachment: {attachment.FileName} ---
                        {textContent}
                        --- End attachment: {attachment.FileName} ---
                        """);

                            logger.LogInformation(
                                "Processed text file: {FileName} ({Length} chars)",
                                attachment.FileName,
                                textContent.Length);

                            break;
                        }

                    case FileContentType.Zip:
                        {
                            using var zip = ZipFile.OpenRead(attachment.StoragePath);
                            var processedEntries = 0;
                            var skippedEntries = 0;

                            foreach (var entry in zip.Entries)
                            {
                                if (string.IsNullOrEmpty(entry.Name)) continue; // skip directories

                                // Skip binary/compiled/large files
                                var entryExt = Path.GetExtension(entry.Name).ToLowerInvariant();
                                if (!IsTextFileExtension(entryExt))
                                {
                                    skippedEntries++;
                                    continue;
                                }

                                // Skip known non-source directories
                                var fullName = entry.FullName.Replace('\\', '/');
                                if (fullName.Contains("/bin/") || fullName.Contains("/obj/") ||
                                    fullName.Contains("/node_modules/") || fullName.Contains("/dist/") ||
                                    fullName.Contains("/.git/") || fullName.Contains("/packages/"))
                                {
                                    skippedEntries++;
                                    continue;
                                }

                                // Skip very large individual files (>100KB)
                                if (entry.Length > 100 * 1024)
                                {
                                    skippedEntries++;
                                    logger.LogInformation("Skipping large zip entry: {Entry} ({Size}KB)",
                                        entry.FullName, entry.Length / 1024);
                                    continue;
                                }

                                try
                                {
                                    using var reader = new StreamReader(entry.Open(), Encoding.UTF8, true);
                                    var content = await reader.ReadToEndAsync();

                                    combinedText.AppendLine($"--- Begin: {entry.FullName} ---");
                                    combinedText.AppendLine(content);
                                    combinedText.AppendLine($"--- End: {entry.FullName} ---");
                                    combinedText.AppendLine();
                                    processedEntries++;
                                }
                                catch
                                {
                                    skippedEntries++;
                                }
                            }

                            logger.LogInformation(
                                "Processed ZIP {FileName}: {Processed} text entries, {Skipped} skipped",
                                attachment.FileName, processedEntries, skippedEntries);
                            break;
                        }

                    case FileContentType.Pdf:
                        logger.LogWarning(
                            "PDF processing not yet implemented for: {FileName}",
                            attachment.FileName);
                        break;

                    default:
                        logger.LogWarning(
                            "Unsupported file type for: {FileName}",
                            attachment.FileName);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process attachment: {FileName}", attachment.FileName);
            }
        }

        logger.LogInformation(
            "ProcessAttachmentsAsync complete: {ImageCount} images, {TextLength} chars of text from {Total} attachments",
            images.Count, combinedText.Length, attachments.Count);

        return (images, combinedText.ToString());
    }

    private static readonly HashSet<string> TextFileExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".cs", ".ts", ".tsx", ".js", ".jsx", ".json", ".xml", ".csv",
        ".log", ".py", ".java", ".cpp", ".c", ".h", ".hpp", ".css", ".scss", ".html",
        ".htm", ".sql", ".sh", ".bash", ".yml", ".yaml", ".toml", ".ini", ".cfg",
        ".config", ".csproj", ".sln", ".props", ".targets", ".razor", ".vue", ".svelte",
        ".rb", ".go", ".rs", ".swift", ".kt", ".gradle", ".ps1", ".psm1", ".psd1",
        ".dockerfile", ".env", ".gitignore", ".editorconfig", ".eslintrc", ".prettierrc",
        ".tf", ".proto", ".graphql", ".r", ".m", ".mm", ".pl", ".lua", ".ex", ".exs",
        ".erl", ".hs", ".fs", ".fsx", ".clj", ".scala", ".pas", ".dfm", ".dpr"
    };

    private static bool IsTextFileExtension(string extension) =>
        TextFileExtensions.Contains(extension) || string.IsNullOrEmpty(extension);
}
