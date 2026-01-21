using System.Text.Json;

namespace BraveSearchMcpServer.Services;

public class BraveSearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BraveSearchService> _logger;
    private readonly string _apiKey;
    private readonly JsonSerializerOptions _jsonOptions;

    public BraveSearchService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<BraveSearchService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["BraveSearch:ApiKey"] ??
                  throw new InvalidOperationException("BraveSearch:ApiKey not configured");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<SearchResult> SearchAsync(string query, int count = 10)
    {
        _logger.LogInformation("Searching Brave: {Query} (count: {Count})", query, count);

        try
        {
            var url = $"https://api.search.brave.com/res/v1/web/search?q={Uri.EscapeDataString(query)}&count={count}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("X-Subscription-Token", _apiKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var braveResponse = JsonSerializer.Deserialize<BraveSearchResponse>(content, _jsonOptions);

            if (braveResponse?.Web?.Results == null)
            {
                return new SearchResult
                {
                    Query = query,
                    TotalResults = 0,
                    Results = []
                };
            }

            return new SearchResult
            {
                Query = query,
                TotalResults = braveResponse.Web.Results.Count,
                Results = braveResponse.Web.Results.Select(r => new SearchResultItem
                {
                    Title = r.Title ?? "",
                    Url = r.Url ?? "",
                    Description = r.Description ?? "",
                    Published = r.Age,
                    Language = r.Language
                }).ToList()
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error calling Brave Search API");
            throw new Exception($"Failed to search: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Brave Search API");
            throw;
        }
    }

    // Brave API response models
    private class BraveSearchResponse
    {
        public WebResults? Web { get; set; }
    }

    private class WebResults
    {
        public List<BraveResult>? Results { get; set; }
    }

    private class BraveResult
    {
        public string? Title { get; set; }
        public string? Url { get; set; }
        public string? Description { get; set; }
        public string? Age { get; set; }
        public string? Language { get; set; }
    }
}

// Public result models
public class SearchResult
{
    public string Query { get; set; } = "";
    public int TotalResults { get; set; }
    public List<SearchResultItem> Results { get; set; } = [];
}

public class SearchResultItem
{
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Description { get; set; } = "";
    public string? Published { get; set; }
    public string? Language { get; set; }
}
