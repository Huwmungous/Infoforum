
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OllamaMcpServer.Services;

namespace OllamaMcpServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AskController : ControllerBase
{
    private readonly ZipCodeExtractor _extractor;
    private readonly ILogger<AskController> _logger;

    public AskController(ZipCodeExtractor extractor, ILogger<AskController> logger)
    {
        _extractor = extractor;
        _logger = logger;
    }

    [HttpPost("with-zip")]
    public async Task<IActionResult> AskWithZip([FromForm] string prompt, IFormFile? zipfile)
    {
        string context = "";

        if (zipfile is not null)
        {
            await using var stream = zipfile.OpenReadStream();
            var files = await _extractor.ExtractAsync(stream);

            context = string.Join("\n\n", files.Select(f => $"// File: {f.FileName}\n{f.Content}"));
        }

        var combinedPrompt = string.IsNullOrWhiteSpace(context)
            ? prompt
            : $"Use the following code context:\n\n{context}\n\nUser question: {prompt}";

        return Ok(new { fullPrompt = combinedPrompt });
    }
}
