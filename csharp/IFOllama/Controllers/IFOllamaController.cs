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

            if(_codeContextService == null)
                _logger.LogWarning("CodeContextService is unavailable. Enhanced code context features will be disabled.");
        }

        // ===== Upstream shapes we might receive =====
        private sealed class GenerateChunk // /api/generate style
        {
            [JsonPropertyName("response")] public string? Response { get; set; }
            [JsonPropertyName("done")] public bool Done { get; set; }
        }

        private sealed class ChatMsg
        {
            [JsonPropertyName("role")] public string? Role { get; set; }
            [JsonPropertyName("content")] public string? Content { get; set; }
        }
        private sealed class ChatChunk // /api/chat streaming style
        {
            [JsonPropertyName("message")] public ChatMsg? Message { get; set; }
            [JsonPropertyName("done")] public bool Done { get; set; }
        }

        // ===== Public DTOs =====
        public sealed class PromptDto
        {
            public string Prompt { get; set; } = "";
        }

        [HttpGet("test")]
        public IActionResult Test() => Ok("IFOllama is alive.");

        // Simple NDJSON streamer for sanity checks
        [HttpGet("stream")]
        public async Task StreamTest()
        {
            Response.ContentType = "application/x-ndjson";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            var utf8NoBom = new UTF8Encoding(false);
            await using var writer = new StreamWriter(Response.BodyWriter.AsStream(), utf8NoBom, leaveOpen: true) { AutoFlush = true };

            var chunks = new[]
            {
                new { response = "Hello", done = false },
                new { response = " World", done = false },
                new { response = "", done = true }
            };

            foreach(var c in chunks)
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(c));
                await writer.FlushAsync();
                await Response.Body.FlushAsync();
                if(c.done) break;
                await Task.Delay(250);
            }
        }

        [HttpPost]
        [Authorize(Policy = "MustBeIntelligenceUser")]
        public async Task<IActionResult> SendPrompt(
            [FromQuery] string conversationId,
            [FromBody] PromptDto request,
            string dest = "code")
        {
            if(string.IsNullOrWhiteSpace(conversationId))
                return BadRequest("conversationId is required.");
            if(request is null || string.IsNullOrWhiteSpace(request.Prompt))
                return BadRequest("prompt is required.");

            try
            {
                // image path is separate
                if(string.Equals(dest, "image", StringComparison.OrdinalIgnoreCase))
                    return await HandleImagePrompt(conversationId, request.Prompt);

                // === Build conversation memory ===
                var history = _contextManager.GetConversation(conversationId); // prior turns
                var codeContext = _contextManager.GetContext(conversationId);  // history + code files (Option B)

                // Persist THIS user turn now (so it lands in history.json)
                _contextManager.AppendMessage(conversationId, "user", request.Prompt);

                // === Choose upstream mode by dest ===
                if(string.Equals(dest, "chat", StringComparison.OrdinalIgnoreCase))
                    await StreamViaChat(conversationId, dest, history, request.Prompt, codeContext);
                else // "code" (default) or anything else -> generate mode
                    await StreamViaGenerate(conversationId, dest, history, request.Prompt, codeContext);

                return new EmptyResult();
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Error processing prompt");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }


        // ------------------ CHAT MODE (messages[]) ------------------
        private async Task StreamViaChat(
            string conversationId,
            string dest,
            List<Dictionary<string, string>> history,
            string latestUserPrompt,
            string? codeContext)
        {
            var messages = new List<object>();

            if(!string.IsNullOrWhiteSpace(codeContext))
            {
                messages.Add(new
                {
                    role = "system",
                    content = "Use the following persistent context and prior turns.\n" + codeContext
                });
            }

            foreach(var d in history)
            {
                if(!d.TryGetValue("role", out var role) || string.IsNullOrWhiteSpace(role)) continue;
                if(!d.TryGetValue("message", out var content) || content is null) continue;
                role = role.ToLowerInvariant();
                if(role != "user" && role != "assistant" && role != "system") role = "user";
                messages.Add(new { role, content });
            }

            messages.Add(new { role = "user", content = latestUserPrompt });

            var reqBody = new { model = SelectModel(dest), messages, stream = true };

            using var jsonRequest = new StringContent(JsonSerializer.Serialize(reqBody), Encoding.UTF8, "application/json");
            using var upstreamResponse = await _httpClient.PostAsync(SelectChatUrl(), jsonRequest);

            if(!upstreamResponse.IsSuccessStatusCode)
            {
                Response.StatusCode = (int)upstreamResponse.StatusCode;
                var err = await upstreamResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("Upstream(chat) returned {Status}: {Body}", upstreamResponse.StatusCode, err);
                return;
            }

            // Detect streaming vs single JSON
            var mediaType = upstreamResponse.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "";
            var isNdjson = mediaType.Contains("x-ndjson") || upstreamResponse.Headers.TransferEncodingChunked == true;

            if(isNdjson)
            {
                await StreamAndPersistChatNdjson(conversationId, upstreamResponse);
            }
            else
            {
                await StreamAndPersistChatSingleJson(conversationId, upstreamResponse);
            }
        }

        private async Task StreamAndPersistChatNdjson(string conversationId, HttpResponseMessage upstreamResponse)
        {
            Response.ContentType = "application/x-ndjson";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            var utf8NoBom = new UTF8Encoding(false);
            await using var upstream = await upstreamResponse.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(upstream);
            await using var writer = new StreamWriter(Response.BodyWriter.AsStream(), utf8NoBom, leaveOpen: true) { AutoFlush = true };

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var full = new StringBuilder();

            string? line;
            while((line = await reader.ReadLineAsync()) != null)
            {
                if(string.IsNullOrWhiteSpace(line)) continue;

                string delta = "";
                bool done = false;

                try
                {
                    var chat = JsonSerializer.Deserialize<ChatChunk>(line, jsonOptions);
                    if(chat is not null)
                    {
                        if(chat.Message?.Content is { Length: > 0 } c1) delta = c1;
                        done = chat.Done;
                    }
                }
                catch { /* ignore malformed lines */ }

                // Only send payloads with actual text or the final done line
                if(delta.Length > 0 || done)
                {
                    if(delta.Length > 0) full.Append(delta);
                    var normalized = JsonSerializer.Serialize(new { response = delta, done });
                    await writer.WriteLineAsync(normalized);
                    await writer.FlushAsync();
                    await Response.Body.FlushAsync();
                }

                if(done) break;
            }

            _contextManager.AppendMessage(conversationId, "assistant", full.ToString());
        }

        private async Task StreamAndPersistChatSingleJson(string conversationId, HttpResponseMessage upstreamResponse)
        {
            // Non-streaming: read the final JSON, extract message.content, return one line
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            using var s = await upstreamResponse.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(s);

            string content = "";
            if(doc.RootElement.TryGetProperty("message", out var msgElem) &&
                msgElem.TryGetProperty("content", out var contentElem))
            {
                content = contentElem.GetString() ?? "";
            }
            else if(doc.RootElement.TryGetProperty("response", out var respElem))
            {
                content = respElem.GetString() ?? "";
            }

            Response.ContentType = "application/x-ndjson";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            var utf8NoBom = new UTF8Encoding(false);
            await using var writer = new StreamWriter(Response.BodyWriter.AsStream(), utf8NoBom, leaveOpen: true) { AutoFlush = true };

            var payload = JsonSerializer.Serialize(new { response = content, done = true });
            await writer.WriteLineAsync(payload);
            await writer.FlushAsync();
            await Response.Body.FlushAsync();

            _contextManager.AppendMessage(conversationId, "assistant", content);
        }




        // ------------------ GENERATE MODE (prompt) ------------------
        private async Task StreamViaGenerate(
            string conversationId,
            string dest,
            List<Dictionary<string, string>> history,
            string latestUserPrompt,
            string? codeContext)
        {
            var transcript = BuildTranscript(history);

            var sb = new StringBuilder();
            sb.AppendLine("You are continuing a persistent conversation. Use the transcript below; do not claim you lack memory.");
            sb.AppendLine();

            if(!string.IsNullOrWhiteSpace(codeContext))
            {
                sb.AppendLine("### Context");
                sb.AppendLine(codeContext);
                sb.AppendLine();
            }

            if(!string.IsNullOrEmpty(transcript))
            {
                sb.AppendLine("### Conversation so far");
                sb.AppendLine(transcript);
                sb.AppendLine();
            }

            sb.AppendLine($"User: {latestUserPrompt}");
            sb.Append("Assistant:");

            var reqBody = new { model = SelectModel(dest), prompt = sb.ToString(), stream = true };

            using var jsonRequest = new StringContent(JsonSerializer.Serialize(reqBody), Encoding.UTF8, "application/json");
            using var upstreamResponse = await _httpClient.PostAsync(SelectGenerateUrl(), jsonRequest);
            if(!upstreamResponse.IsSuccessStatusCode)
            {
                Response.StatusCode = (int)upstreamResponse.StatusCode;
                var err = await upstreamResponse.Content.ReadAsStringAsync();
                _logger.LogWarning("Upstream(generate) returned {Status}: {Body}", upstreamResponse.StatusCode, err);
                return;
            }

            await StreamAndPersist(conversationId, upstreamResponse);
        }


        // ------------------ Common streaming & persistence ------------------
        private async Task StreamAndPersist(string conversationId, HttpResponseMessage upstreamResponse)
        {
            Response.ContentType = "application/x-ndjson";
            Response.Headers["Cache-Control"] = "no-cache";
            Response.Headers["X-Accel-Buffering"] = "no";

            var utf8NoBom = new UTF8Encoding(false);
            await using var upstream = await upstreamResponse.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(upstream);
            await using var writer = new StreamWriter(Response.BodyWriter.AsStream(), utf8NoBom, leaveOpen: true) { AutoFlush = true };

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var full = new StringBuilder();

            string? line;
            while((line = await reader.ReadLineAsync()) != null)
            {
                if(string.IsNullOrWhiteSpace(line)) continue;

                string? delta = null;
                bool done = false;

                // try chat-style first
                try
                {
                    var chat = JsonSerializer.Deserialize<ChatChunk>(line, jsonOptions);
                    if(chat?.Message?.Content is { Length: > 0 } c1)
                    {
                        delta = c1;
                        done = chat.Done;
                    }
                    else if(chat is not null)
                    {
                        done = chat.Done;
                    }
                }
                catch { /* ignore and try generate */ }

                // then try generate-style
                if(delta is null)
                {
                    try
                    {
                        var gen = JsonSerializer.Deserialize<GenerateChunk>(line, jsonOptions);
                        if(gen is not null)
                        {
                            if(!string.IsNullOrEmpty(gen.Response))
                                delta = gen.Response;
                            done = gen.Done;
                        }
                    }
                    catch { /* skip */ }
                }

                if(delta is not null)
                    full.Append(delta);

                // Normalize downstream to {response, done} so your frontend keeps working
                var normalized = JsonSerializer.Serialize(new { response = delta ?? "", done });
                await writer.WriteLineAsync(normalized);
                await writer.FlushAsync();
                await Response.Body.FlushAsync();

                if(done) break;
            }

            _contextManager.AppendMessage(conversationId, "assistant", full.ToString());
        }

        private static string BuildTranscript(List<Dictionary<string, string>> history)
        {
            if(history.Count == 0) return string.Empty;

            var sb = new StringBuilder();
            foreach(var d in history)
            {
                if(!d.TryGetValue("role", out var role)) continue;
                if(!d.TryGetValue("message", out var msg)) continue;

                role = role?.ToLowerInvariant();
                if(role != "user" && role != "assistant" && role != "system")
                    role = "user";

                // Simple readable transcript
                sb.Append(role switch
                {
                    "assistant" => "Assistant: ",
                    "system" => "System: ",
                    _ => "User: "
                });
                sb.AppendLine(msg);
            }
            return sb.ToString();
        }

        private async Task<IActionResult> HandleImagePrompt(string conversationId, string prompt)
        {
            _contextManager.AppendMessage(conversationId, "user", prompt);

            var reqBody = new
            {
                model = SelectModel("image"),
                prompt,
                conversationId
            };

            using var request = new StringContent(
                JsonSerializer.Serialize(reqBody),
                Encoding.UTF8, "application/json");

            using var response = await _httpClient.PostAsync(SelectGenerateUrl(), request);
            if(!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Image upstream returned {Status}: {Body}", response.StatusCode, err);
                return StatusCode((int)response.StatusCode, "Image generation failed");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var base64 = Convert.ToBase64String(bytes);

            _contextManager.AppendMessage(conversationId, "assistant", "[[image-bytes-returned-as-base64]]");
            return Ok(new { image = base64 });
        }

        // ----------- Config helpers -----------
        private string SelectGenerateUrl()
            => _configuration["GenerateApiUrl"]
               ?? _configuration["ApiUrl"]
               ?? throw new InvalidOperationException("GenerateApiUrl/ApiUrl is missing.");

        private string SelectChatUrl()
            => _configuration["ChatApiUrl"]
               ?? throw new InvalidOperationException("ChatApiUrl is missing (required for UpstreamMode=chat).");

        private string SelectModel(string dest) => dest switch
        {
            "code" => _configuration["CodeModel"] ?? throw new InvalidOperationException("CodeModel not configured."),
            "chat" => _configuration["ChatModel"] ?? _configuration["CodeModel"] ?? throw new InvalidOperationException("ChatModel not configured."),
            "image" => _configuration["ImageModel"] ?? throw new InvalidOperationException("ImageModel not configured."),
            _ => throw new InvalidOperationException("Invalid model destination.")
        };
    }
}
