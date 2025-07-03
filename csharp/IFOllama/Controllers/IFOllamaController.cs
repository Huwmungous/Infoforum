using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        private class ChatChunk
        {
            [JsonPropertyName("response")]
            public string? Response { get; set; }

            [JsonPropertyName("done")]
            public bool Done { get; set; }
        }

        [HttpGet("stream")]
        public async Task StreamTest()
        {
            var response = Response;
            response.ContentType = "application/x-ndjson";
            response.Headers["Cache-Control"] = "no-cache";
            response.Headers["X-Accel-Buffering"] = "no";

            var utf8NoBom = new UTF8Encoding(false);
            await using var writer = new StreamWriter(response.BodyWriter.AsStream(), utf8NoBom, leaveOpen: true)
            {
                AutoFlush = true
            };

            var chunks = new[]
            {
                new { Response = "Hello", Done = false },
                new { Response = " World", Done = false },
                new { Response = "", Done = true }
            };

            foreach (var chunk in chunks)
            {
                var json = JsonSerializer.Serialize(chunk);
                await writer.WriteLineAsync(json);
                await writer.FlushAsync();
                await response.Body.FlushAsync();

                if (chunk.Done)
                    break;

                await Task.Delay(500);
            }
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
                if (dest == "image")
                {
                    return await HandleImagePrompt(conversationId, request.Prompt);
                }
                else
                {
                    await HandleCodeOrChatPrompt(conversationId, request.Prompt, context, dest);
                    return new EmptyResult();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing prompt");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        private async Task HandleCodeOrChatPrompt(string conversationId, string prompt, string conversationContext, string dest)
        {
            var finalPrompt = $"{conversationContext}\nUser: {prompt}\nAssistant:";

            var jsonRequest = new StringContent(
                JsonSerializer.Serialize(new { Model = SelectModel(dest), Prompt = finalPrompt }),
                Encoding.UTF8, "application/json");

            var upstreamResponse = await _httpClient.PostAsync(SelectAPIUrl(), jsonRequest);
            if (!upstreamResponse.IsSuccessStatusCode)
            {
                Response.StatusCode = (int)upstreamResponse.StatusCode;
                return;
            }

            Response.ContentType = "application/x-ndjson";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            var utf8NoBom = new UTF8Encoding(false);

            await using var responseStream = await upstreamResponse.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(responseStream);

            await using var writer = new StreamWriter(Response.BodyWriter.AsStream(), utf8NoBom, leaveOpen: true)
            {
                AutoFlush = true
            };

            _contextManager.AppendMessage(conversationId, "User", prompt);

            var fullResponse = new StringBuilder();

            string? line;
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    var chunk = JsonSerializer.Deserialize<ChatChunk>(line, jsonOptions);
                    if (chunk != null)
                    {
                        fullResponse.Append(chunk.Response ?? "");

                        var ndjson = JsonSerializer.Serialize(chunk);
                        await writer.WriteLineAsync(ndjson);
                        await writer.FlushAsync();
                        await Response.Body.FlushAsync();

                        if (chunk.Done)
                        {
                            break;
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Skipping malformed JSON line: {Line}", line);
                }
            }

            _contextManager.AppendMessage(conversationId, "Assistant", fullResponse.ToString());
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
