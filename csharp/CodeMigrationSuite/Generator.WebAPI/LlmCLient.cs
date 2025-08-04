// ------------------- Generator.WebAPI/LlmClient.cs -------------------
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Generator.WebAPI;

public class LlmClient
{
    private readonly HttpClient _http = new();
    public async Task<string> CallLlmAsync(string prompt)
    {
        var response = await _http.PostAsJsonAsync("http://10.9.8.3:11434/api/generate", new
        {
            model = "deepseek-coder:33b",
            prompt,
            stream = false
        });

        var result = await response.Content.ReadAsStringAsync();
        return result;
    }
}