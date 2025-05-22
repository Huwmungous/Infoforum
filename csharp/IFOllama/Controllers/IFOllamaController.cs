using IFOllama.RAG;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;

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
        private readonly IRagService _ragService;
        private readonly ILogger<IFOllamaController> _logger;

        public IFOllamaController(
            IConfiguration configuration,
            HttpClient httpClient,
            IConversationContextManager contextManager,
            ILogger<IFOllamaController> logger,
            IRagService ragService,
            CodeContextService? codeContextService = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _codeContextService = codeContextService; // May be null
            _ragService = ragService;

            if (_codeContextService == null)
            {
                _logger.LogWarning("CodeContextService is unavailable. Enhanced code context features will be disabled.");
            }
        }

        [HttpPost]
        [Authorize(Policy = "MustBeIntelligenceUser")]
        public async Task<IActionResult> SendPrompt(
            [FromQuery] string conversationId,
            [FromBody] string prompt,
            string dest = "code"
        )
        {
            try
            {
                // Retrieve existing conversation context (always needed regardless of dest)
                var conversationContext = _contextManager.GetContext(conversationId);

                if (dest == "image")
                {
                    return await HandleImagePrompt(conversationId, prompt, dest);
                }
                else
                {
                    return await HandleCodeOrChatPrompt(conversationId, prompt, dest, conversationContext);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing prompt");
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }

        private async Task<IActionResult> HandleCodeOrChatPrompt(string conversationId, string prompt, string dest, string conversationContext)
        {
            string combinedPrompt;

            // Only enhance with code context and RAG for "code" destination AND if services are available
            if (dest == "code" && _codeContextService != null && _ragService != null)
            {
                try
                {
                    _logger.LogInformation("Enhancing code prompt with context");

                    // Get code context (via embeddings)
                    var queryEmbedding = await _ragService.GetEmbeddingService().EmbedAsync(prompt);
                    var (distances, codeContextIds) = _codeContextService.Search(queryEmbedding, 3);

                    // Get RAG chunks for additional context
                    var relevantChunks = await _ragService.GetTopChunksAsync(prompt, 3);

                    // Build enhanced prompt with context
                    var promptWithContext = new StringBuilder();
                    promptWithContext.AppendLine("## Code Context:");

                    // Add code context if available
                    if (codeContextIds.Length > 0 && codeContextIds[0].Length > 0)
                    {
                        foreach (var id in codeContextIds[0])
                        {
                            _logger.LogInformation($"Adding code context ID: {id}");
                        }
                    }

                    promptWithContext.AppendLine("\n## Documentation Context:");
                    foreach (var chunk in relevantChunks)
                    {
                        promptWithContext.AppendLine(chunk);
                    }

                    promptWithContext.AppendLine("\n## User Query:");
                    promptWithContext.AppendLine(prompt);

                    // Combine with conversation context
                    combinedPrompt = $"{conversationContext}\nUser: {promptWithContext}\nAssistant:";
                    _logger.LogInformation("Enhanced prompt with code context and documentation");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to enhance prompt with context. Using regular prompt.");
                    combinedPrompt = $"{conversationContext}\nUser: {prompt}\nAssistant:";
                }
            }
            else
            {
                // For chat or when services aren't available, just use the regular conversation context and prompt
                combinedPrompt = $"{conversationContext}\nUser: {prompt}\nAssistant:";
            }

            // Create request payload
            var content = new StringContent(
                JsonConvert.SerializeObject(new OllamaRequest
                {
                    Model = SelectModel(dest),
                    Prompt = combinedPrompt
                }),
                Encoding.UTF8,
                "application/json"
            );

            // Rest of the method remains unchanged
            var response = await _httpClient.PostAsync(SelectAPIUrl(), content);

            if (!response.IsSuccessStatusCode)
                return StatusCode((int)response.StatusCode, "Request failed");

            MemoryStream responseStreamWriter = await HandleResponse(response);
            responseStreamWriter.Seek(0, SeekOrigin.Begin);

            // Read the text response
            var responseText = await new StreamReader(responseStreamWriter).ReadToEndAsync();

            // Append both the user prompt and the assistant's response to the conversation
            _contextManager.AppendMessage(conversationId, "User", prompt);
            _contextManager.AppendMessage(conversationId, "Assistant", responseText);

            // Return the text response
            return new FileStreamResult(new MemoryStream(Encoding.UTF8.GetBytes(responseText)), "text/plain");
        }

        // Rest of your controller methods remain unchanged
        private async Task<IActionResult> HandleImagePrompt(string conversationId, string prompt, string dest)
        {
            // [Existing implementation unchanged]
            // Record the user prompt in the conversation history.
            _contextManager.AppendMessage(conversationId, "User", prompt);

            // Create an image generation request that includes conversationId.
            var imageRequest = new ImageGenerationRequest
            {
                Model = SelectModel(dest),
                Prompt = prompt,
                ConversationId = conversationId
            };

            // Serialize and send the image generation request to the proper API.
            var content = new StringContent(
                JsonConvert.SerializeObject(imageRequest),
                Encoding.UTF8,
                "application/json"
            );

            // Use the API URL for image generation.
            var imageApiUrl = SelectAPIUrl();
            var imageResponse = await _httpClient.PostAsync(imageApiUrl, content);

            if (!imageResponse.IsSuccessStatusCode)
                return StatusCode((int)imageResponse.StatusCode, "Image generation request failed.");

            // Read the binary image data.
            var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();

            // Convert image bytes to base64 string
            var base64Image = Convert.ToBase64String(imageBytes);

            // Create response object with base64 image
            var response = new
            {
                image = base64Image
            };

            // Return JSON response
            return Ok(response);
        }

        [HttpGet("query")]
        [Authorize(Policy = "MustBeIntelligenceUser")]
        public async Task<IActionResult> QueryCodebase([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest(new { error = "Query parameter is required." });

            try
            {
                var response = await _httpClient.GetStringAsync($"{_configuration["QueryApiUrl"]}?query={Uri.EscapeDataString(query)}");
                var results = response.Split("\n", StringSplitOptions.RemoveEmptyEntries);
                return Ok(new { query, results });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "Internal server error", details = ex.Message });
            }
        }

        private static async Task<MemoryStream> HandleResponse(HttpResponseMessage response)
        {
            var tokenStream = await response.Content.ReadAsStreamAsync();
            var tokenStreamReader = new StreamReader(tokenStream);
            var responseStreamWriter = new MemoryStream();
            var responseWriter = new StreamWriter(responseStreamWriter);

            using (var jsonReader = new JsonTextReader(tokenStreamReader) { SupportMultipleContent = true })
            {
                var jsonSerializer = new JsonSerializer();

                while (await jsonReader.ReadAsync())
                {
                    if (jsonReader.TokenType == JsonToken.StartObject)
                    {
                        var responseObject = jsonSerializer.Deserialize<OllamaResponse>(jsonReader);
                        if (responseObject != null)
                        {
                            await responseWriter.WriteAsync(responseObject.Response);
                            await responseWriter.FlushAsync();
                        }
                    }
                }
            }
            return responseStreamWriter;
        }

        private string SelectAPIUrl()
        {
            return _configuration["ApiUrl"] ?? throw new InvalidOperationException("ApiUrl is not configured.");
        }

        private string SelectModel(string dest)
        {
            return dest switch
            {
                "code" => _configuration["CodeModel"] ?? throw new InvalidOperationException("CodeModel is not configured."),
                "chat" => _configuration["ChatModel"] ?? throw new InvalidOperationException("ChatModel is not configured."),
                "image" => _configuration["ImageModel"] ?? throw new InvalidOperationException("ImageModel is not configured."),
                _ => throw new InvalidOperationException("Destination model must be 'code', 'chat', or 'image'.")
            };
        }
    }
}