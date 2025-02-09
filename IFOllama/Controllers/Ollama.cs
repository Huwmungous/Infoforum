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
        public async Task<IActionResult> SendPrompt([FromBody] string prompt)
        {
            try
            {
                var apiUrl = _configuration["ApiUrl"] ?? throw new InvalidOperationException("ApiUrl is not configured.");
                var model = _configuration["Model"] ?? throw new InvalidOperationException("Model is not configured.");

                var jsonString = JsonConvert.SerializeObject(new OllamaModel { Model = model, Prompt = prompt });
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(apiUrl, content);

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, "Request failed");

                var resultStream = await response.Content.ReadAsStreamAsync();
                var resultStreamReader = new StreamReader(resultStream);
                var responseStreamWriter = new MemoryStream();
                var formattedWriter = new StreamWriter(responseStreamWriter);

                using (var jsonReader = new JsonTextReader(resultStreamReader) { SupportMultipleContent = true })
                {
                    var jsonSerializer = new JsonSerializer();

                    while (await jsonReader.ReadAsync())
                    {
                        if (jsonReader.TokenType == JsonToken.StartObject)
                        {
                            var responseObject = jsonSerializer.Deserialize<OllamaResponse>(jsonReader);
                            if (responseObject != null)
                            {
                                await formattedWriter.WriteAsync(responseObject.Response);
                                await formattedWriter.FlushAsync();
                            }
                        }
                    }
                }

                responseStreamWriter.Seek(0, SeekOrigin.Begin);
                return new FileStreamResult(responseStreamWriter, "text/plain");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }

    public class OllamaModel
    {
        public required string Model { get; set; }
        public required string Prompt { get; set; }
    }

    public class OllamaResponse
    {
        public required string Model { get; set; }
        public required DateTime CreatedAt { get; set; }
        public required string Response { get; set; }
        public required bool Done { get; set; }
        public required string DoneReason { get; set; }
        public required List<int> Context { get; set; }
        public required long TotalDuration { get; set; }
        public required long LoadDuration { get; set; }
        public required int PromptEvalCount { get; set; }
        public required long PromptEvalDuration { get; set; }
        public required int EvalCount { get; set; }
        public required long EvalDuration { get; set; }
    }
}