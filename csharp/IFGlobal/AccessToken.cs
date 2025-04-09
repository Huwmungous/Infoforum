using Newtonsoft.Json;

namespace IFGlobal
{
    public class AccessToken
    {
        public static string AcquireToken()
        {
            // Read token endpoint and service account credentials from environment variables
            string? tokenEndpoint = Environment.GetEnvironmentVariable("LR_TOKEN_ENDPOINT");
            string? clientId = Environment.GetEnvironmentVariable("LR_SVC_CLIENTID");
            string? clientSecret = Environment.GetEnvironmentVariable("LR_SVC_SECRET");

            // Throw an error if any environment variable is not found
            if (string.IsNullOrEmpty(tokenEndpoint))
                throw new Exception("No LR_TOKEN_ENDPOINT.");

            if (string.IsNullOrEmpty(clientId))
                throw new Exception("No LR_SVC_CLIENTID.");

            if (string.IsNullOrEmpty(clientSecret))
                throw new Exception("No LR_SVC_SECRET.");

            string scope = "openid";

            string result = GetAccessToken(tokenEndpoint, clientId, clientSecret, scope);
            if (string.IsNullOrEmpty(result))
                throw new Exception("Failed to obtain access token.");

            return result;
        }

        private static string GetAccessToken(string tokenEndpoint, string clientId, string clientSecret, string scope)
        {
            using var httpClient = new HttpClient();
            // Prepare the POST parameters for the token request
            var parameters = new Dictionary<string, string>
            {
                { "client_id", clientId },
                { "client_secret", clientSecret },
                { "grant_type", "client_credentials" },
                { "scope", scope }
            };

            var content = new FormUrlEncodedContent(parameters);
            var response = httpClient.PostAsync(tokenEndpoint, content).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Token request failed: " + response.StatusCode);
                return "";
            }

            string json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(json);
            return tokenResponse?.AccessToken ?? "";
        }
    }
}
