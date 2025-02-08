using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace MyWebService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class OllamaController : ControllerBase
    {
        private static readonly HttpClient client = new HttpClient();
        // You might want to inject this into your controller instead of using a static field.
        // For simplicity, I'm keeping it as static for now.
        private static string apiUrl = "http://intelligence:5008/api/generate";

        [HttpPost]
        public async Task<IActionResult> SendPrompt([FromBody] OllamaModel prompt)
        {
            try
            {
                var jsonString = JsonConvert.SerializeObject(prompt);
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");

                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.PostAsync(apiUrl, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        return StatusCode((int)response.StatusCode, "Request failed");
                    }

                    var responseContent = await response.Content.ReadAsStringAsync();
                    // You might need to parse the responseContent based on your API's actual response format
                    return Ok(responseContent);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }

    public class OllamaModel
    {
        public string Model { get; set; }
        public string Prompt { get; set; }
    }
}