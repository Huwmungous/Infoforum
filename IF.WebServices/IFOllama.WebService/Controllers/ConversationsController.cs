using IFOllama.Classes.Models;
using IFOllama.WebService.Data;
using IFOllama.WebService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IFOllama.WebService.Controllers;

[ApiController]
[Route("api/conversations")]
[Authorize(Policy = "MustBeIntelligenceUser")]
public class ConversationsController(
    IConversationStore store,
    FileStorageService fileStorage,
    OllamaService ollamaService,
    ILogger<ConversationsController> logger) : ControllerBase
{
    /// <summary>
    /// Gets all conversations for a user.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<ConversationListItem>>> GetConversations([FromQuery] string userId)
    {
        var items = await store.ListAsync(userId);
        return Ok(items);
    }

    /// <summary>
    /// Creates a new conversation.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ConversationListItem>> CreateConversation([FromBody] string title, [FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { error = "title is required" });

        var item = await store.CreateAsync(title, userId);
        return Ok(item);
    }

    /// <summary>
    /// Gets all messages for a conversation.
    /// </summary>
    [HttpGet("{id}/messages")]
    public async Task<ActionResult<List<Message>>> GetMessages(string id, [FromQuery] string userId)
    {
        if (!await store.OwnsConversationAsync(id, userId))
            return Forbid();

        var msgs = await store.ReadMessagesAsync(id, userId);
        return Ok(msgs);
    }

    /// <summary>
    /// Appends a message to a conversation.
    /// </summary>
    [HttpPost("{id}/messages")]
    public async Task<IActionResult> AppendMessage(string id, [FromBody] Message msg, [FromQuery] string userId)
    {
        if (msg is null)
            return BadRequest(new { error = "message is required" });

        await store.AppendMessageAsync(id, msg, userId);
        return Ok(new { ok = true });
    }

    /// <summary>
    /// Appends a message with file attachments to a conversation.
    /// </summary>
    [HttpPost("{id}/messages/with-files")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> AppendMessageWithFiles(
        string id,
        [FromForm] string role,
        [FromForm] string content,
        [FromForm] List<IFormFile>? files,
        [FromQuery] string? userId)
    {
        if (string.IsNullOrWhiteSpace(role))
            return BadRequest(new { error = "role is required" });

        var message = new Message
        {
            Role = role,
            Content = content ?? string.Empty,
            Attachments = []
        };

        logger.LogInformation(
            "Received request {Method} {Path} with Content-Type={ContentType}, Content-Length={ContentLength}",
            HttpContext.Request.Method,
            HttpContext.Request.Path,
            HttpContext.Request.ContentType,
            HttpContext.Request.ContentLength);

        if (files != null && files.Count > 0)
        {
            logger.LogInformation("Processing {Count} file(s) for conversation {ConversationId}", files.Count, id);

            foreach (var file in files)
            {
                try
                {
                    var attachment = await fileStorage.SaveFileAsync(file, id);
                    message.Attachments.Add(attachment);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to save file {FileName}", file.FileName);
                    return BadRequest(new { error = $"Failed to save file {file.FileName}: {ex.Message}" });
                }
            }
        }

        await store.AppendMessageAsync(id, message, userId ?? "anonymous");

        return Ok(new
        {
            ok = true,
            attachmentCount = message.Attachments.Count,
            attachments = message.Attachments.Select(a => new
            {
                a.Id,
                a.FileName,
                a.FileType,
                a.ContentType,
                a.SizeBytes,
                a.StoragePath
            })
        });
    }

    /// <summary>
    /// Gets a file attachment from a conversation.
    /// </summary>
    [HttpGet("{id}/files/{fileId}")]
    public async Task<IActionResult> GetFile(string id, string fileId, [FromQuery] string userId)
    {
        if (!await store.OwnsConversationAsync(id, userId))
            return Forbid();

        var messages = await store.ReadMessagesAsync(id, userId);
        var attachment = messages
            .SelectMany(m => m.Attachments ?? [])
            .FirstOrDefault(a => a.Id == fileId);

        if (attachment == null)
            return NotFound(new { error = "File not found" });

        try
        {
            var fileBytes = await fileStorage.ReadFileAsync(attachment.StoragePath);
            return File(fileBytes, attachment.ContentType, attachment.FileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { error = "File not found on disk" });
        }
    }

    /// <summary>
    /// Updates the title of a conversation.
    /// </summary>
    [HttpPatch("{id}/title")]
    public async Task<IActionResult> UpdateTitle(string id, [FromBody] string title, [FromQuery] string userId)
    {
        if (!await store.OwnsConversationAsync(id, userId))
            return Forbid();

        if (string.IsNullOrWhiteSpace(title))
            return BadRequest(new { error = "title is required" });

        await store.UpdateTitleAsync(id, title.Trim(), userId);
        return Ok(new { title = title.Trim() });
    }

    /// <summary>
    /// Generates an AI summary title for a conversation based on the user's first message.
    /// </summary>
    [HttpPost("{id}/generate-title")]
    public async Task<IActionResult> GenerateTitle(string id, [FromBody] string userMessage, [FromQuery] string userId)
    {
        if (!await store.OwnsConversationAsync(id, userId))
            return Forbid();

        if (string.IsNullOrWhiteSpace(userMessage))
            return BadRequest(new { error = "userMessage is required" });

        try
        {
            var title = await ollamaService.GenerateTitleAsync(userMessage);
            await store.UpdateTitleAsync(id, title, userId);
            return Ok(new { title });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate title for conversation {Id}", id);
            return StatusCode(500, new { error = "Failed to generate title" });
        }
    }

    /// <summary>
    /// Deletes a conversation and all associated files.
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveConversation(string id, [FromQuery] string userId)
    {
        if (!await store.OwnsConversationAsync(id, userId))
            return Forbid();

        await fileStorage.DeleteConversationFilesAsync(id);
        await store.RemoveAsync(id, userId);
        return Ok(new { ok = true });
    }
}
