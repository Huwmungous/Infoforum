using System.Net.Http;
using System.Text.Json;

namespace ChitterChatterClient.Services;

/// <summary>
/// Client for fetching configuration from ConfigWebService.
/// </summary>
public sealed class ConfigServiceClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _configServiceUrl;
    private readonly string _appDomain;

    public ConfigServiceClient(string configServiceUrl = "https://longmanrd.net/config", string appDomain = "Infoforum")
    {
        _httpClient = new HttpClient();
        _configServiceUrl = configServiceUrl.TrimEnd('/');
        _appDomain = appDomain;
    }

    /// <summary>
    /// Fetches the bootstrap configuration (unauthenticated - for getting auth URLs).
    /// </summary>
    public async Task<BootstrapConfig?> GetBootstrapConfigAsync()
    {
        var url = $"{_configServiceUrl}/Config?cfg=bootstrap&type=user&appDomain={_appDomain}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<BootstrapConfig>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    /// <summary>
    /// Fetches application-specific configuration (authenticated).
    /// </summary>
    public async Task<T?> GetConfigAsync<T>(string configName, string accessToken) where T : class
    {
        var url = $"{_configServiceUrl}/Config?cfg={configName}&type=user&appDomain={_appDomain}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    /// <summary>
    /// Fetches a string configuration value (authenticated).
    /// </summary>
    public async Task<string?> GetConfigStringAsync(string configName, string accessToken)
    {
        var url = $"{_configServiceUrl}/Config?cfg={configName}&type=user&appDomain={_appDomain}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
            return null;

        var content = await response.Content.ReadAsStringAsync();
        
        // If it's a JSON string (quoted), deserialize it
        if (content.StartsWith("\"") && content.EndsWith("\""))
        {
            return JsonSerializer.Deserialize<string>(content);
        }
        
        return content;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// Bootstrap configuration from ConfigWebService.
/// </summary>
public class BootstrapConfig
{
    public string Realm { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string OpenIdConfig { get; set; } = string.Empty;
    public string? LoggerService { get; set; }
    public string? ChitterChatterService { get; set; }
    public string LogLevel { get; set; } = "Information";
}

/// <summary>
/// ChitterChatter-specific configuration.
/// </summary>
public class ChitterChatterConfig
{
    public string HubUrl { get; set; } = string.Empty;
}
