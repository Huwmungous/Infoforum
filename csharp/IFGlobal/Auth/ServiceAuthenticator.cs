using IFGlobal.Configuration;
using System.Text.Json;

namespace IFGlobal.Auth;

public static class ServiceAuthenticator
{
    /// <summary>
    /// Authenticate a service account and get an access token using the ClientSecret from ConfigService.
    /// Uses ServiceClientId (not ClientId) to ensure we authenticate as the service account,
    /// even when the service is configured to validate patient tokens.
    /// </summary>
    public static async Task<string> GetServiceAccessTokenAsync(IConfigService configService)
    {
        if (!configService.IsInitialized)
        {
            throw new InvalidOperationException("ConfigService must be initialized first");
        }

        var clientSecret = configService.ClientSecret;
        if (string.IsNullOrEmpty(clientSecret))
        {
            throw new InvalidOperationException(
                "ClientSecret not available from ConfigService. " +
                "Ensure bootstrap was requested with type=service.");
        }

        return await GetServiceAccessTokenAsync(
            configService.OpenIdConfig, 
            configService.ServiceClientId,
            clientSecret);
    }

    /// <summary>
    /// Authenticate a service account with explicit credentials.
    /// </summary>
    public static async Task<string> GetServiceAccessTokenAsync(
        string openIdConfig, 
        string serviceClientId,
        string clientSecret)
    {
        using var httpClient = new HttpClient();

        var tokenEndpoint = $"{openIdConfig}/protocol/openid-connect/token";

        var formData = new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", serviceClientId },
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