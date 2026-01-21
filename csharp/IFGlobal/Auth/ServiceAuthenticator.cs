using IFGlobal.Configuration;
using System.Text.Json;

namespace IFGlobal.Auth;

public static class ServiceAuthenticator
{
    /// <summary>
    /// Authenticate a service account and get an access token using the ClientSecret from environment variable.
    /// Uses ServiceClientId (not ClientId) to ensure we authenticate as the service account,
    /// even when the service is configured to validate patient tokens.
    /// </summary>
    public static async Task<string> GetServiceAccessTokenAsync(IConfigService configService)
    {
        return await GetServiceAccessTokenAsync(configService.IsInitialized, configService. OpenIdConfig, configService. ServiceClientId);
    }
    public static async Task<string> GetServiceAccessTokenAsync(bool IsInitialized, string OpenIdConfig, string ServiceClientId)
    {
        if (!IsInitialized)
        {
            throw new InvalidOperationException("ConfigService must be initialized first");
        }

        using var httpClient = new HttpClient();

        var tokenEndpoint = $"{OpenIdConfig}/protocol/openid-connect/token";

        var clientSecret = Environment.GetEnvironmentVariable("IF_CLIENTSECRET");
        if (string.IsNullOrEmpty(clientSecret))
            throw new InvalidOperationException("Environment variable 'IF_CLIENTSECRET' is not set.");

        var formData = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", ServiceClientId },  // Use ServiceClientId for service auth
            { "client_secret", clientSecret }
        };

        var content = new FormUrlEncodedContent(formData);
        var response = await httpClient.PostAsync(tokenEndpoint, content);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Failed to authenticate service: {(int)response.StatusCode} - {error}");
        }

        var tokenResponse = await response.Content.ReadAsStringAsync();
        var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenResponse);

        return tokenData.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("No access token in response");
    }
}