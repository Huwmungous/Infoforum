using Newtonsoft.Json;
using Npgsql;
using System.Runtime.CompilerServices;
using System.Text;

namespace BTTranslate
{
    class Program
    {
        private static string accessToken = "";
        private static readonly string ifollamaApiUrl = "https://longmanrd.net/aiapi";
        private static string connectionString = "";
        private static readonly string conversationId = Guid.NewGuid().ToString();

        private static NpgsqlConnection? readConnection;
        private static NpgsqlConnection? writeConnection;

        private static string sendPromptUrl = $"{ifollamaApiUrl}?conversationId={Uri.EscapeDataString(conversationId)}&dest=chat";

        static async Task Main()
        {
            Console.WriteLine("BTTranslate started.");
            await AcquireTokenAsync();
            GetConnStr();

            try
            {
                using (var reader = UpdatableEntriesReader())
                {
                    using (var httpClient = new HttpClient())
                    {
                        using var writeConnection = new NpgsqlConnection(connectionString);
                        {
                            string englishentry = "";
                            while (reader.Read())
                            {
                                if (reader.GetString(2) != englishentry)
                                {
                                    englishentry = reader.GetString(2);
                                    Console.WriteLine();
                                    Console.WriteLine($"*** Translating '{englishentry}' ***");
                                    Console.WriteLine(new string('-', englishentry.Length + 22));
                                }

                                string langcode = reader.GetString(0);
                                string entrycode = reader.GetString(1);

                                string promptJson = JsonConvert.SerializeObject($"The equivalent of the phrase '{englishentry}' in the language with iso code '{langcode}'. The translation in your response MUST be tagged with '<trans>' and '</trans>'. Please do not include any variables or placeholders such as 'X' or [X] in the phrase you come up with.");
                                var requestContent = new StringContent(promptJson, Encoding.UTF8, "application/json");

                                httpClient.DefaultRequestHeaders.Authorization =
                                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                                var response = await httpClient.PostAsync(sendPromptUrl, requestContent);
                                if (response.IsSuccessStatusCode)
                                {
                                    string responseContent = await response.Content.ReadAsStringAsync();
                                    string translation = ExtractTranslationFromResponse(responseContent);

                                    Console.WriteLine($"'{translation}' in '{langcode}'");

                                    //// Insert the translation into your database; update the query as needed.
                                    //string insertQuery = "INSERT INTO translations (record_id, translated_text) VALUES (@id, @translation)";
                                    //using (var insertCmd = new NpgsqlCommand(insertQuery, conn))
                                    //{
                                    //    insertCmd.Parameters.AddWithValue("id", recordId);
                                    //    insertCmd.Parameters.AddWithValue("translation", translation);
                                    //    await insertCmd.ExecuteNonQueryAsync();
                                    //}
                                    responseContent = string.Empty;
                                }
                                else
                                {
                                    Console.WriteLine($"Failed to translate record {englishentry}. Status code: {response.StatusCode}");
                                }
                            }
                        }
                    }

                    readConnection?.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            Console.WriteLine("BTTranslate processing complete.");
        }


        private static string ExtractTranslationFromResponse(string responseContent)
        {
            ReadOnlySpan<char> responseSpan = responseContent.AsSpan();
            ReadOnlySpan<char> startTag = "<trans>".AsSpan();
            ReadOnlySpan<char> endTag = "</trans>".AsSpan();

            int startIndex = responseSpan.IndexOf(startTag);
            if (startIndex == -1)
            {
                throw new Exception("Start tag '<trans>' not found in response content.");
            }
            startIndex += startTag.Length;

            int endIndex = responseSpan.Slice(startIndex).IndexOf(endTag);
            if (endIndex == -1)
            {
                throw new Exception("End tag '</trans>' not found in response content.");
            }

            ReadOnlySpan<char> translationSpan = responseSpan.Slice(startIndex, endIndex);
            return translationSpan.ToString();
        }



        private static NpgsqlDataReader UpdatableEntriesReader()
        {
            readConnection = new NpgsqlConnection(connectionString);
            readConnection.Open();
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
            "SELECT langcode, ee.code entrycode, ee.entry englishentry " +
            "FROM foreignlanguages fl, englishentries ee " +
            "LEFT JOIN foreignentries fe on fe.code = ee.code " +
            "WHERE fe.entry IS NULL ";

            var result = new NpgsqlCommand(selectQuery, readConnection).ExecuteReader();

            return result;
        }

        private static async Task AcquireTokenAsync()
        {
            // Keycloak token endpoint and service account credentials
            string tokenEndpoint = "https://longmanrd.net/auth/realms/LongmanRd/protocol/openid-connect/token";
            string clientId = "E1BF0E47-FEA7-4C8E-8449-39971E549BBC";
            string clientSecret = "vIY62Sf8Obi5kBzCNmeyDH0wH3oabCj6";
            string scope = "openid";

            // Obtain access token using client credentials flow
            accessToken = await GetAccessTokenAsync(tokenEndpoint, clientId, clientSecret, scope);
            if (string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine("Failed to obtain access token from Keycloak.");
                return;
            }
            Console.WriteLine("Access token acquired.");
        }

        private static void GetConnStr()
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
            connectionString = $"Host={host};Username=languagemanager;Password={password};Database=RozeBowl";
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
