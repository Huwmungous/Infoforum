using IFOllama.Classes.Models;
using IFOllama.WebService.Models;
using IFOllama.WebService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace IFOllama.WebService.Controllers;

[ApiController]
[Route("api/mcp")]
[Authorize(Policy = "MustBeIntelligenceUser")]
public class McpController(
    McpRouterService router,
    OllamaService ollama,
    FileStorageService fileStorage,
    ILogger<McpController> logger) : ControllerBase
{
    /// <summary>
    /// Gets all available MCP tools from all configured servers.
    /// </summary>
    [HttpGet("tools")]
    [ProducesResponseType(typeof(object), 200)]
    public async Task<IActionResult> GetMcpTools()
    {
        var tools = await router.GetAllToolsAsync();
        return Ok(new { tools, count = tools.Count });
    }

    /// <summary>
    /// Gets the configured MCP server endpoints.
    /// </summary>
    [HttpGet("servers")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetMcpServers()
    {
        var servers = router.GetServerEndpoints();
        return Ok(new { servers, count = servers.Count });
    }

    /// <summary>
    /// Calls a specific tool on an MCP server.
    /// </summary>
    [HttpPost("tools/call")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    public async Task<IActionResult> CallMcpTool([FromBody] ToolCallRequest request)
    {
        try
        {
            var result = await router.CallToolAsync(
                request.ServerName,
                request.ToolName,
                request.Arguments);
            return Ok(new { success = true, result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error calling tool {Server}/{Tool}", request.ServerName, request.ToolName);
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Chat with Ollama using available tools.
    /// </summary>
    [HttpPost("chat/ollama")]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    public async Task<IActionResult> ChatWithOllama([FromBody] ChatRequest request)
    {
        try
        {
            logger.LogInformation("chat/ollama called");

            var response = await ollama.ChatWithToolsAsync(
                request.Message,
                request.History ?? [],
                request.Model);

            return Ok(new { success = true, response });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in chat with Ollama");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Chat with Ollama including file attachments.
    /// </summary>
    [HttpPost("chat/ollama/with-files")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [ProducesResponseType(typeof(object), 200)]
    [ProducesResponseType(typeof(object), 400)]
    public async Task<IActionResult> ChatWithOllamaWithFiles(
        [FromForm] string message,
        [FromForm] string? conversationId,
        [FromForm] string? historyJson,
        [FromForm] string? model,
        [FromForm] List<IFormFile>? files)
    {
        try
        {
            var history = new List<OllamaMessage>();
            if (!string.IsNullOrWhiteSpace(historyJson))
            {
                history = JsonSerializer.Deserialize<List<OllamaMessage>>(historyJson) ?? [];
            }

            List<FileAttachment>? attachments = null;
            var finalMessage = message;

            if (files != null && files.Count > 0)
            {
                conversationId ??= Guid.NewGuid().ToString();
                attachments = [];

                logger.LogInformation("Processing {Count} file(s) for chat", files.Count);

                finalMessage += "\n\nThe user uploaded the following files:";

                foreach (var file in files)
                {
                    var attachment = await fileStorage.SaveFileAsync(file, conversationId);

                    finalMessage += $"\n- {attachment.FileName} ({attachment.FileType})";

                    attachments.Add(attachment);

                    // Append text contents to the message
                    if (attachment.FileType == FileContentType.Text || attachment.FileType == FileContentType.Document)
                    {
                        var textContent = await fileStorage.ReadTextFileAsync(attachment.StoragePath);
                        finalMessage += $"\n\n--- Begin attachment: {attachment.FileName} ---\n{textContent}\n--- End attachment: {attachment.FileName} ---";
                    }
                    else if (attachment.FileType == FileContentType.Zip)
                    {
                        try
                        {
                            using var zip = ZipFile.OpenRead(attachment.StoragePath);

                            foreach (var entry in zip.Entries)
                            {
                                if (string.IsNullOrEmpty(entry.Name)) continue;

                                try
                                {
                                    using var reader = new StreamReader(entry.Open(), Encoding.UTF8, true);
                                    var content = await reader.ReadToEndAsync();

                                    if (!string.IsNullOrWhiteSpace(content))
                                    {
                                        finalMessage += $"\n\n--- Begin ZIP entry: {entry.FullName} ---\n{content}\n--- End ZIP entry: {entry.FullName} ---";
                                    }
                                    else
                                    {
                                        finalMessage += $"\n\n--- ZIP entry empty or binary: {entry.FullName} ---";
                                    }
                                }
                                catch
                                {
                                    finalMessage += $"\n\n--- Could not read ZIP entry: {entry.FullName} ---";
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            finalMessage += $"\n\n--- Failed to open ZIP file: {attachment.FileName} ({ex.Message}) ---";
                        }
                    }
                    else
                    {
                        finalMessage += $"\n\n--- Unable to read: {attachment.FileName} ---";
                    }
                }
            }

            var response = await ollama.ChatWithToolsAsync(
                finalMessage,
                history,
                model,
                attachments);

            return Ok(new
            {
                success = true,
                response,
                conversationId,
                attachmentCount = attachments?.Count ?? 0
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in chat with files");
            return BadRequest(new { success = false, error = ex.Message });
        }
    }
}
