using Newtonsoft.Json;
using Npgsql;
using System.Text;

namespace BTTranslate
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("BTTranslate started.");

            // Keycloak token endpoint and service account credentials
            string tokenEndpoint = "https://longmanrd.net/auth/realms/LongmanRd/protocol/openid-connect/token";
            string clientId = "53FF08FC-C03E-4F1D-A7E9-41F2CB3EE3C7";
            string clientSecret = "YOUR_CLIENT_SECRET"; // Replace with your actual secret
            string scope = "openid"; // Add additional scopes if needed

            // Obtain access token using client credentials flow
            string accessToken = await GetAccessTokenAsync(tokenEndpoint, clientId, clientSecret, scope);
            if (string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine("Failed to obtain access token from Keycloak.");
                return;
            }
            Console.WriteLine("Access token acquired.");

            string connectionString = GetConnStr();

            // IFOllama API URL – update if necessary
            string ifollamaApiUrl = "https://longmanrd.net/AiApi";

            try
            {
                using (var conn = new NpgsqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // Query to retrieve records needing translation; update table/column names as required
                    string selectQuery =
                        "WITH foreignlanguages as " +
                        "( " +
                        "  SELECT l.code langcode " +
                        "  FROM dbo.LANGUAGE l WHERE " +
                        "  l.code<> 'en' " +
                        "), " +
                        "englishentries as " +
                        "( " +
                        "  SELECT code, entry " +
                        "  FROM dbo.languageentry " +
                        "  WHERE lang = 'en' " +
                        "), " +
                        "foreignentries as " +
                        "( " +
                        "  SELECT code, entry " +
                        "  FROM dbo.languageentry " +
                        "  WHERE lang<> 'en' " +
                        ")  " +
                        "SELECT * from foreignlanguages fl, englishentries ee " +
                        "LEFT JOIN foreignentries fe on fe.code = ee.code " +
                        "WHERE fe.entry IS NULL ";

                    using (var selectCmd = new NpgsqlCommand(selectQuery, conn))
                    using (var reader = await selectCmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int recordId = reader.GetInt32(0);
                            string originalText = reader.GetString(1);

                            // Generate a conversation ID for this translation request
                            string conversationId = Guid.NewGuid().ToString();

                            // Build the URL for the IFOllama API endpoint.
                            // Optionally, include additional query parameters (e.g., dest=code).
                            string sendPromptUrl = $"{ifollamaApiUrl}?conversationId={Uri.EscapeDataString(conversationId)}&dest=code";

                            // Prepare the JSON payload (in this example, just the text)
                            string promptJson = JsonConvert.SerializeObject(originalText);
                            var requestContent = new StringContent(promptJson, Encoding.UTF8, "application/json");

                            // Create an HTTP client and add the access token as a Bearer token in the header.
                            using (var httpClient = new HttpClient())
                            {
                                httpClient.DefaultRequestHeaders.Authorization =
                                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                                var response = await httpClient.PostAsync(sendPromptUrl, requestContent);
                                if (response.IsSuccessStatusCode)
                                {
                                    string translation = await response.Content.ReadAsStringAsync();
                                    Console.WriteLine($"Record {recordId} translated: {translation}");

                                    // Insert the translation into your database; update the query as needed.
                                    string insertQuery = "INSERT INTO translations (record_id, translated_text) VALUES (@id, @translation)";
                                    using (var insertCmd = new NpgsqlCommand(insertQuery, conn))
                                    {
                                        insertCmd.Parameters.AddWithValue("id", recordId);
                                        insertCmd.Parameters.AddWithValue("translation", translation);
                                        await insertCmd.ExecuteNonQueryAsync();
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Failed to translate record {recordId}. Status code: {response.StatusCode}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            Console.WriteLine("BTTranslate processing complete.");
        }

        private static string GetConnStr()
        {
            // Get the host and password from environment variables
            string? host = Environment.GetEnvironmentVariable("BT_DBSERVER");
            string? password = Environment.GetEnvironmentVariable("BT_LANGPASS");

            // Throw an error if either environment variable is not found
            if (string.IsNullOrEmpty(host))
            {
                throw new Exception("Environment variable BT_DBSERVER is not set.");
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new Exception("Environment variable BT_LANGPASS is not set.");
            }

            // Database connection details – update with your actual connection string
            return $"Host={host};Username=languagemanager;Password={password};Database=RozeBowl";
        }

        /// <summary>
        /// Acquires an access token from Keycloak using the client credentials flow.
        /// </summary>
        static async Task<string> GetAccessTokenAsync(string tokenEndpoint, string clientId, string clientSecret, string scope)
        {
            using (var httpClient = new HttpClient())
            {
                // Prepare the POST parameters for the token request
                var parameters = new Dictionary<string, string>
                {
                    { "client_id", clientId },
                    { "client_secret", clientSecret },
                    { "grant_type", "client_credentials" },
                    { "scope", scope }
                };

                var content = new FormUrlEncodedContent(parameters);
                var response = await httpClient.PostAsync(tokenEndpoint, content);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Token request failed: " + response.StatusCode);
                    return null;
                }

                string json = await response.Content.ReadAsStringAsync();
                var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(json);
                return tokenResponse?.access_token;
            }
        }
    }

    // Class for deserializing the token response JSON.
    public class TokenResponse
    {
        public string access_token { get; set; }
        public int expires_in { get; set; }
        public string token_type { get; set; }
    }
}
