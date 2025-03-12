using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
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

        public IFOllamaController(IConfiguration configuration, HttpClient httpClient, IConversationContextManager contextManager)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _contextManager = contextManager ?? throw new ArgumentNullException(nameof(contextManager));
        }

        [HttpPost]
        [Authorize(Policy = "MustBeIntelligenceUser")]
        public async Task<IActionResult> SendPrompt([FromQuery] string conversationId, [FromBody] string prompt, string dest = "code")
        {
            try
            {
                // Retrieve existing conversation context
                var conversationContext = _contextManager.GetContext(conversationId);
                // Combine the context with the new prompt
                var combinedPrompt = $"{conversationContext}\nUser: {prompt}\nAssistant:";

                // Send the combined prompt to Ollama
                var content = new StringContent(JsonConvert.SerializeObject(new OllamaRequest { Model = SelectModel(dest), Prompt = combinedPrompt }), Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(SelectAPIUrl(), content);

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, "Request failed");

                MemoryStream responseStreamWriter = await HandleResponse(response);
                responseStreamWriter.Seek(0, SeekOrigin.Begin);

                // Assume the response is a single block of text. You may need to update context manager here.
                var responseText = await new StreamReader(responseStreamWriter).ReadToEndAsync();
                _contextManager.AppendMessage(conversationId, "User", prompt);
                _contextManager.AppendMessage(conversationId, "Assistant", responseText);

                // Return response text
                return new FileStreamResult(new MemoryStream(Encoding.UTF8.GetBytes(responseText)), "text/plain");
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
                // Call the Bash API (Netcat on Port 5050)
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
            // DeepSeek-R1:8b deepseek-coder:33b
            return dest == "code" ?
                _configuration["CodeModel"] ?? throw new InvalidOperationException("CodeModel is not configured.") :
                dest == "chat" ?
                  _configuration["ChatModel"] ?? throw new InvalidOperationException("ChatModel is not configured.") :
                  throw new InvalidOperationException("Destination model must be 'code' or 'chat'.");
        }
    }
}
