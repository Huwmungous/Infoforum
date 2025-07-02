using System.Text;
using System.Text.Json;
using IFOllama.RAG;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IFOllama.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class IFOllamaController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly IConversationContextManager _contextManager;
        private readonly CodeContextService? _codeContextService;
        private readonly ILogger<IFOllamaController> _logger;

        public IFOllamaController(
            IConfiguration configuration,
            HttpClient httpClient,
            IConversationContextManager contextManager,
            ILogger<IFOllamaController> logger,
            CodeContextService? codeContextService = null)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _contextManager = contextManager;
            _logger = logger;
            _codeContextService = codeContextService;

            if (_codeContextService == null)
                _logger.LogWarning("CodeContextService is unavailable. Enhanced code context features will be disabled.");
        }

        [HttpGet("test")]
        public IActionResult Test() => Ok("IFOllama is alive.");

        [HttpPost]
        [Authorize(Policy = "MustBeIntelligenceUser")]
        public async Task<IActionResult> SendPrompt(
            [FromQuery] string conversationId,
            [FromBody] PromptDto request,
            string dest = "code")
        {
            try
            {
                var context = _contextManager.GetContext(conversationId);
                return dest == "image"
                    ? await HandleImagePrompt(conversationId, request.Prompt)
                    : await HandleCodeOrChatPrompt(conversationId, request.Prompt, context, dest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing prompt");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        private async Task<IActionResult> HandleCodeOrChatPrompt(string conversationId, string prompt, string conversationContext, string dest)
        {
            var finalPrompt = $"{conversationContext}\nUser: {prompt}\nAssistant:";

            var jsonRequest = new StringContent(
                JsonSerializer.Serialize(new { Model = SelectModel(dest), Prompt = finalPrompt }),
                Encoding.UTF8, "application/json");

            var upstreamResponse = await _httpClient.PostAsync(SelectAPIUrl(), jsonRequest);
            if (!upstreamResponse.IsSuccessStatusCode)
                return StatusCode((int)upstreamResponse.StatusCode, "Upstream request failed.");

            Response.ContentType = "application/x-ndjson";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["Transfer-Encoding"] = "chunked";
            Response.Headers["X-Accel-Buffering"] = "no";

            await using var responseStream = await upstreamResponse.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(responseStream);

            await using var writer = new StreamWriter(Response.BodyWriter.AsStream(), Encoding.UTF8, bufferSize: 8192, leaveOpen: true)
            {
                AutoFlush = true
            };

            _contextManager.AppendMessage(conversationId, "User", prompt);

            var fullResponse = new StringBuilder();

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var chunk = JsonSerializer.Deserialize(line, AppJsonContext.Default.ChatChunk);
                    if (chunk?.Response is { Length: > 0 })
                    {
                        fullResponse.Append(chunk.Response);
                        var ndjson = JsonSerializer.Serialize(chunk);
                        await writer.WriteLineAsync(ndjson);
                    }

                    if (chunk?.Done == true)
                        break;
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Skipping malformed JSON line: {Line}", line);
                }
            }

            _contextManager.AppendMessage(conversationId, "Assistant", fullResponse.ToString());

            return new EmptyResult(); // We've already written the response
        }

        private async Task<IActionResult> HandleImagePrompt(string conversationId, string prompt)
        {
            _contextManager.AppendMessage(conversationId, "User", prompt);

            var request = new StringContent(
                JsonSerializer.Serialize(new { Model = SelectModel("image"), Prompt = prompt, ConversationId = conversationId }),
                Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(SelectAPIUrl(), request);
            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Image generation failed");

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var base64 = Convert.ToBase64String(bytes);

            return Ok(new { image = base64 });
        }

        [HttpGet("query")]
        [Authorize(Policy = "MustBeIntelligenceUser")]
        public async Task<IActionResult> QueryCodebase([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest(new { error = "Query parameter is required." });

            try
            {
                var url = $"{_configuration["QueryApiUrl"]}?query={Uri.EscapeDataString(query)}";
                var result = await _httpClient.GetStringAsync(url);
                var lines = result.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                return Ok(new { query, results = lines });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        private string SelectAPIUrl() =>
            _configuration["ApiUrl"] ?? throw new InvalidOperationException("ApiUrl is missing.");

        private string SelectModel(string dest) => dest switch
        {
            "code" => _configuration["CodeModel"] ?? throw new InvalidOperationException("CodeModel not configured."),
            "chat" => _configuration["ChatModel"] ?? throw new InvalidOperationException("ChatModel not configured."),
            "image" => _configuration["ImageModel"] ?? throw new InvalidOperationException("ImageModel not configured."),
            _ => throw new InvalidOperationException("Invalid model destination.")
        };
    }
}
