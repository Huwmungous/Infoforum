using Newtonsoft.Json;
using Npgsql;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Transactions;

namespace BTTranslate
{
    class Program
    {
        private static string accessToken = "";
        private static readonly string ifollamaApiUrl = "https://longmanrd.net/aiapi";
        private static string connectionString = "";
        private static readonly string conversationId = Guid.NewGuid().ToString();

        private static NpgsqlConnection? readConnection;

        private static string sendPromptUrl = $"{ifollamaApiUrl}?conversationId={Uri.EscapeDataString(conversationId)}&dest=chat";
        private static string insertQuery = "INSERT INTO dbo.languageentry( code, entry, lang) VALUES(@code, @entry, @lang);";

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
                            writeConnection.Open();
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

                                string translation = await RequestTranslation(httpClient, writeConnection, englishentry, langcode, entrycode);

                                InsertTranslationEntry(writeConnection, langcode, entrycode, translation);
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

        private static async Task<string> RequestTranslation(HttpClient httpClient, NpgsqlConnection writeConnection, string englishentry, string langcode, string entrycode)
        {
            string result = "";
            string promptJson = JsonConvert.SerializeObject($"I need the equivalent of the phrase '{englishentry}' in the language with iso code '{langcode}'. The translation in your response MUST be tagged with '<trans>' and '</trans>'. Please do not include any variables or placeholders such as 'X' or [X] in the phrase you come up with.");
            var requestContent = new StringContent(promptJson, Encoding.UTF8, "application/json");

            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using (var response = await httpClient.PostAsync(sendPromptUrl, requestContent))
            {
                response.EnsureSuccessStatusCode();

                using (var responseStream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(responseStream))
                {
                    char[] buffer = new char[512];
                    int read;

                    StringBuilder responseBuilder = new();

                    // Read the stream until the end
                    while ((read = await reader.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        responseBuilder.Append(buffer, 0, read);

                    string responseContent = responseBuilder.ToString();

                    result = ExtractTranslationFromResponse(responseContent);
                }
            }

            return result;
        }

        private static void InsertTranslationEntry(NpgsqlConnection writeConnection, string langcode, string entrycode, string translation)
        {
            using (var insertCmd = new NpgsqlCommand(insertQuery, writeConnection))
            {
                insertCmd.Parameters.AddWithValue("@code", entrycode);
                insertCmd.Parameters.AddWithValue("@entry", translation);
                insertCmd.Parameters.AddWithValue("@lang", langcode);
                insertCmd.ExecuteNonQuery();
            }
        }

        private static string ExtractTranslationFromResponse(string responseContent)
        {
            ReadOnlySpan<char> responseSpan = responseContent.AsSpan();
            ReadOnlySpan<char> startTag = "<trans>".AsSpan();
            ReadOnlySpan<char> endTag = "</trans>".AsSpan();
            ReadOnlySpan<char> thinkStartTag = "<think>".AsSpan();
            ReadOnlySpan<char> thinkEndTag = "</think>".AsSpan();

            // Remove text between <think> and </think> tags
            int thinkEndIndex = -1;
            while (true)
            {
                int thinkStartIndex = responseSpan.IndexOf(thinkStartTag);
                if (thinkStartIndex >= 0)
                {
                    thinkEndIndex = responseSpan.Slice(thinkStartIndex).IndexOf(thinkEndTag);

                    if (thinkEndIndex == -1)
                        throw new Exception("End tag '</think>' not found in response content.");

                    thinkEndIndex += thinkEndTag.Length;
                }
                responseSpan = responseSpan.Slice(0, thinkStartIndex).ToString() + responseSpan.Slice(thinkStartIndex + thinkEndIndex).ToString();


                int transStartIndex = responseSpan.IndexOf(startTag);
                if (transStartIndex == -1) 
                    throw new Exception("Start tag '<trans>' not found in response content."); 

                transStartIndex += startTag.Length;

                int transEndIndex = responseSpan.Slice(transStartIndex).IndexOf(endTag);
                if (transEndIndex == -1) 
                    throw new Exception("End tag '</trans>' not found in response content."); 

                ReadOnlySpan<char> translationSpan = responseSpan.Slice(transStartIndex, transEndIndex);
                return translationSpan.ToString();
            }
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

            // Obtain access token using client credentials flow
            accessToken = await GetAccessTokenAsync(tokenEndpoint, clientId, clientSecret, scope);
            if (string.IsNullOrEmpty(accessToken))
            {
                throw new Exception("Failed to obtain access token.");
            }
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
