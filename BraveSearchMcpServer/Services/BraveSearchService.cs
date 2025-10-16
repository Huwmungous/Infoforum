using BraveSearchMcpServer.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Web;

namespace BraveSearchMcpServer.Services;

public class BraveSearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BraveSearchService> _logger;
    private readonly string _apiKey;

    public BraveSearchService(HttpClient httpClient, ILogger<BraveSearchService> logger, string apiKey)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = apiKey;
        _httpClient.BaseAddress = new Uri("https://api.search.brave.com/");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.Add("X-Subscription-Token", _apiKey);
    }

    public async Task<BraveSearchResponse> SearchAsync(string query, int count = 10)
    {
        try
        {
            var encodedQuery = HttpUtility.UrlEncode(query);
            var url = $"res/v1/web/search?q={encodedQuery}&count={count}";

            _logger.LogInformation("Searching: {Query}", query);
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<BraveApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (apiResponse?.Web?.Results == null)
            {
                return new BraveSearchResponse
                {
                    Results = Array.Empty<SearchResult>(),
                    TotalResults = 0,
                    Query = query
                };
            }

            var results = apiResponse.Web.Results
                .Where(r => r.Title != null && r.Url != null)
                .Select(r => new SearchResult
                {
                    Title = r.Title!,
                    Url = r.Url!,
                    Description = r.Description
                })
                .ToArray();

            return new BraveSearchResponse
            {
                Results = results,
                TotalResults = results.Length,
                Query = query
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed");
            throw;
        }
    }
}