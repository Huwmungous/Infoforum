using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Text;

namespace IFOllama.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OllamaController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public OllamaController(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        [HttpPost]
        public async Task<IActionResult> SendPrompt([FromBody] string prompt, string dest = "code")
        {
            try
            {
                var content = new StringContent(JsonConvert.SerializeObject(new OllamaRequest { Model = SelectModel(dest), Prompt = prompt }), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(SelectAPIUrl(), content);

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, "Request failed");

                MemoryStream responseStreamWriter = await HandleResponse(response);

                responseStreamWriter.Seek(0, SeekOrigin.Begin);
                return new FileStreamResult(responseStreamWriter, "text/plain");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
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
            return dest == "code" ?
                _configuration["CodeModel"] ?? throw new InvalidOperationException("CodeModel is not configured.") :
                dest == "chat" ?
                  _configuration["ChatModel"] ?? throw new InvalidOperationException("ChatModel is not configured.") :
                  throw new InvalidOperationException("Destination model must be 'code' or 'chat'.");
        }
    }
}