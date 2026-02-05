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
    private readonly string _ollamaBaseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434";
    private readonly string _defaultModel = config["Ollama:Model"] ?? "qwen2.5:32b";

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

                            foreach (var entry in zip.Entries)
                            {
                                if (string.IsNullOrEmpty(entry.Name)) continue; // skip directories

                                try
                                {
                                    using var reader = new StreamReader(entry.Open(), Encoding.UTF8, true);
                                    var content = await reader.ReadToEndAsync();

                                    combinedText.AppendLine($"""
                                        --- Begin ZIP entry: {entry.FullName} ---
                                        {content}
                                        --- End ZIP entry: {entry.FullName} ---
                                        """);
                                }
                                catch
                                {
                                    combinedText.AppendLine($"--- Could not read ZIP entry: {entry.FullName} ---");
                                }
                            }

                            logger.LogInformation("Processed ZIP file: {FileName}", attachment.FileName);
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

        return (images, combinedText.ToString());
    }
}
