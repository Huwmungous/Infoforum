using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Text;

namespace IFOllama.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class IFOllamaController(
        IConfiguration configuration,
        HttpClient httpClient,
        IConversationContextManager contextManager
    ) : ControllerBase
    {
        private readonly IConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        private readonly IConversationContextManager _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));

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
                    var imageApiUrl = _configuration["ImageApiUrl"] ?? throw new InvalidOperationException("ImageApiUrl is not configured.");
                    var imageResponse = await _httpClient.PostAsync(imageApiUrl, content);

                    if (!imageResponse.IsSuccessStatusCode)
                        return StatusCode((int)imageResponse.StatusCode, "Image generation request failed.");

                    // Read the binary image data.
                    var imageBytes = await imageResponse.Content.ReadAsByteArrayAsync();

                    // Log a placeholder message in the conversation history for the image response.
                    // You might choose to log a textual URL or description instead.
                    _contextManager.AppendMessage(conversationId, "Assistant", "[Image Generated]");

                    // Return the image bytes with a proper MIME type.
                    return new FileContentResult(imageBytes, "image/png");
                }
                else
                {
                    // For text-generation (chat, code) combine conversation context with prompt.
                    var combinedPrompt = $"{conversationContext}\nUser: {prompt}\nAssistant:";

                    // Create request payload.
                    var content = new StringContent(
                        JsonConvert.SerializeObject(new OllamaRequest
                        {
                            Model = SelectModel(dest),
                            Prompt = combinedPrompt
                        }),
                        Encoding.UTF8,
                        "application/json"
                    );

                    // Use text-generation API URL.
                    var response = await _httpClient.PostAsync(SelectAPIUrl(), content);

                    if (!response.IsSuccessStatusCode)
                        return StatusCode((int)response.StatusCode, "Request failed");

                    MemoryStream responseStreamWriter = await HandleResponse(response);
                    responseStreamWriter.Seek(0, SeekOrigin.Begin);

                    // Read the text response.
                    var responseText = await new StreamReader(responseStreamWriter).ReadToEndAsync();

                    // Append both the user prompt and the assistant's response to the conversation.
                    _contextManager.AppendMessage(conversationId, "User", prompt);
                    _contextManager.AppendMessage(conversationId, "Assistant", responseText);

                    // Return the text response.
                    return new FileStreamResult(new MemoryStream(Encoding.UTF8.GetBytes(responseText)), "text/plain");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
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
