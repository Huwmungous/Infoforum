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

                var responseStream = await response.Content.ReadAsStreamAsync();
                var responseStreamReader = new StreamReader(responseStream);
                var responseStreamWriter = new MemoryStream();
                var responseStreamWriterWriter = new StreamWriter(responseStreamWriter);

                using (var jsonReader = new JsonTextReader(responseStreamReader))
                {
                    var jsonSerializer = new JsonSerializer();

                    while (await jsonReader.ReadAsync())
                    {
                        if (jsonReader.TokenType == JsonToken.StartObject)
                        {
                            var responseObject = jsonSerializer.Deserialize<OllamaResponse>(jsonReader);
                            if (responseObject != null)
                            {
                                await responseStreamWriterWriter.WriteLineAsync(responseObject.Response);
                                await responseStreamWriterWriter.FlushAsync();
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
        public string Model { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Response { get; set; }
        public bool Done { get; set; }
        public string DoneReason { get; set; }
        public List<int> Context { get; set; }
        public long TotalDuration { get; set; }
        public long LoadDuration { get; set; }
        public int PromptEvalCount { get; set; }
        public long PromptEvalDuration { get; set; }
        public int EvalCount { get; set; }
        public long EvalDuration { get; set; }
    }
}