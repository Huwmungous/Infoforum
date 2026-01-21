using System.Text.Json.Serialization;

namespace BraveSearchMcpServer.Models;

public class BraveApiResponse
{
    [JsonPropertyName("web")]
    public WebResults? Web { get; set; }
}

public class WebResults
{
    [JsonPropertyName("results")]
    public WebResult[]? Results { get; set; }
}

public class WebResult
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}