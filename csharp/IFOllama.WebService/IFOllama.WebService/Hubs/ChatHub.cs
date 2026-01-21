using IFOllama.Classes.Models;
using IFOllama.WebService.Data;
using IFOllama.WebService.Models;
using IFOllama.WebService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace IFOllama.WebService.Hubs;

[Authorize(Policy = "MustBeIntelligenceUser")]
public class ChatHub(
    IConfiguration config,
    ILogger<ChatHub> logger,
    IConversationStore conversationStore,
    FileStorageService fileStorage,
    McpRouterService mcpRouter) : Hub
{
    private readonly string _ollamaBaseUrl = config["Ollama:BaseUrl"] ?? "http://localhost:11434";
    private readonly string _defaultModel = config["Ollama:Model"] ?? "qwen2.5:32b";

    /// <summary>
    /// Streams chat responses to the client using SignalR.
    /// </summary>
    public async IAsyncEnumerable<string> StreamChat(
        string modelName,
        string[] history,
        string conversationId,
        string userId,
        List<string>? enabledTools = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        logger.LogInformation("StreamChat called for conversation {ConversationId} with {ToolCount} enabled tools",
            conversationId, enabledTools?.Count ?? 0);

        var model = string.IsNullOrWhiteSpace(modelName) ? _defaultModel : modelName;
        
        // Build messages for the model
        var messages = await BuildMessagesForModelAsync(history, enabledTools);

        // Get tools if any are enabled
        List<OllamaTool>? ollamaTools = null;
        if (enabledTools?.Count > 0)
        {
            try
            {
                var allTools = await mcpRouter.GetAllToolsAsync();
                ollamaTools = allTools
                    .Where(t => enabledTools.Contains(t.ServerName))
                    .Select(t => new OllamaTool
                    {
                        Type = "function",
                        Function = new OllamaFunction
                        {
                            Name = $"{t.ServerName}__{t.Name}",
                            Description = t.Description,
                            Parameters = t.InputSchema
                        }
                    })
                    .ToList();
                    
                logger.LogInformation("Loaded {Count} tools for streaming", ollamaTools.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load tools, continuing without tool support");
            }
        }

        var request = new OllamaChatRequest
        {
            Model = model,
            Messages = messages,
            Tools = ollamaTools,
            Stream = true
        };

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromMinutes(10);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_ollamaBaseUrl}/api/chat")
        {
            Content = JsonContent.Create(request)
        };

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to connect to Ollama");
            yield return $"[Error: Failed to connect to model service: {ex.Message}]";
            yield break;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var fullResponse = new StringBuilder();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line)) continue;

            string? token = null;
            var isDone = false;
            List<OllamaToolCall>? toolCalls = null;

            try
            {
                using var json = JsonDocument.Parse(line);

                if (json.RootElement.TryGetProperty("message", out var message))
                {
                    if (message.TryGetProperty("content", out var content))
                    {
                        token = content.GetString();
                    }
                    
                    if (message.TryGetProperty("tool_calls", out var toolCallsElement))
                    {
                        toolCalls = JsonSerializer.Deserialize<List<OllamaToolCall>>(toolCallsElement.GetRawText());
                    }
                }

                if (json.RootElement.TryGetProperty("done", out var done))
                {
                    isDone = done.GetBoolean();
                }
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to parse JSON line: {Line}", line);
                continue;
            }

            if (!string.IsNullOrEmpty(token))
            {
                fullResponse.Append(token);
                yield return token;
            }

            // Handle tool calls
            if (toolCalls != null && toolCalls.Count > 0)
            {
                yield return "\n\n[Executing tools...]\n\n";

                foreach (var toolCall in toolCalls)
                {
                    try
                    {
                        var parts = toolCall.Function.Name.Split("__");
                        if (parts.Length == 2)
                        {
                            var serverName = parts[0];
                            var toolName = parts[1];

                            logger.LogInformation("Executing tool: {Server}/{Tool}", serverName, toolName);
                            
                            var toolResult = await mcpRouter.CallToolAsync(serverName, toolName, toolCall.Function.Arguments);
                            
                            yield return $"\n[Tool {toolName} result: {(toolResult.Length > 100 ? toolResult[..100] + "..." : toolResult)}]\n";
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error executing tool {Tool}", toolCall.Function.Name);
                        yield return $"\n[Tool error: {ex.Message}]\n";
                    }
                }
            }

            if (isDone)
            {
                break;
            }
        }

        // Save the assistant's response to the conversation
        if (!string.IsNullOrEmpty(conversationId) && fullResponse.Length > 0)
        {
            try
            {
                await conversationStore.AppendMessageAsync(
                    conversationId,
                    new Message("assistant", fullResponse.ToString()),
                    userId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to save assistant message to conversation");
            }
        }
    }

    /// <summary>
    /// Simple non-streaming chat for quick responses.
    /// </summary>
    public async Task<string> Chat(
        string modelName,
        string[] history,
        string conversationId,
        string userId,
        List<string>? enabledTools = null)
    {
        var result = new StringBuilder();
        
        await foreach (var token in StreamChat(modelName, history, conversationId, userId, enabledTools))
        {
            result.Append(token);
        }

        return result.ToString();
    }

    private async Task<List<OllamaMessage>> BuildMessagesForModelAsync(
        string[]? history,
        List<string>? enabledTools = null)
    {
        var messages = new List<OllamaMessage>();

        // Add system prompt for tools if enabled
        if (enabledTools?.Count > 0)
        {
            var systemPrompt = BuildToolSystemPrompt(enabledTools);
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                messages.Add(new OllamaMessage { Role = "system", Content = systemPrompt });
            }
        }

        // Parse history strings into messages
        if (history != null)
        {
            foreach (var line in history)
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex > 0)
                {
                    var role = line[..colonIndex].ToLowerInvariant();
                    var content = line[(colonIndex + 1)..];
                    
                    // Normalise role
                    role = role switch
                    {
                        "user" or "assistant" or "system" or "tool" => role,
                        _ => "user"
                    };

                    messages.Add(new OllamaMessage { Role = role, Content = content });
                }
            }
        }

        return await Task.FromResult(messages);
    }

    private static string BuildToolSystemPrompt(List<string> enabledTools)
    {
        if (enabledTools.Count == 0) return string.Empty;

        var prompt = new StringBuilder();
        prompt.AppendLine("You have access to tools for analysing code and databases.");
        prompt.AppendLine("When users ask about 'the application', 'the project', or 'the codebase', use the available tools.");
        prompt.AppendLine();
        prompt.AppendLine("Available tool categories:");
        
        foreach (var tool in enabledTools)
        {
            prompt.AppendLine($"- {tool}");
        }

        return prompt.ToString();
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
