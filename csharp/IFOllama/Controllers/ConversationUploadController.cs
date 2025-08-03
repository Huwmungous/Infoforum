
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.IO.Compression;

namespace IFOllama.Controllers
{
    [ApiController]
    [Route("conversations")]
    public class ConversationUploadController(
        ILogger<ConversationUploadController> logger) : ControllerBase
    {
        private readonly ILogger<ConversationUploadController> _logger = logger;

        [HttpPost("{conversationId}/upload-context")]
        [Authorize(Policy = "MustBeIntelligenceUser")]
        public async Task<IActionResult> UploadContext(
            string conversationId,
            IFormFile zip,
            [FromQuery] bool keep = false,
            CancellationToken cancellationToken = default)
        {
            if (zip == null || zip.Length == 0)
                return BadRequest("No zip file provided.");

            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "Conversations", conversationId);
            Directory.CreateDirectory(basePath);
            var zipPath = Path.Combine(basePath, "context.zip");
            var extractPath = Path.Combine(basePath, "context");

            await using (var stream = new FileStream(zipPath, FileMode.Create))
            {
                await zip.CopyToAsync(stream, cancellationToken);
            }

            if (Directory.Exists(extractPath))
                Directory.Delete(extractPath, true);

            ZipFile.ExtractToDirectory(zipPath, extractPath);
            _logger.LogInformation("Uploaded and extracted context zip for conversation {ConversationId}", conversationId);

            var keepFlagPath = Path.Combine(basePath, "keep.json");
            await System.IO.File.WriteAllTextAsync(keepFlagPath, keep.ToString().ToLower(), cancellationToken);

            return Ok(new { message = "Context uploaded", conversationId, keep });
        }
    }
}
