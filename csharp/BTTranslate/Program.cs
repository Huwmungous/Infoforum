using Newtonsoft.Json;
using Npgsql;
using System.Text;
using System.Text.RegularExpressions;

namespace BTTranslate
{
    class Program
    {
        private static readonly int READ_BUF_SIZE = 1024;

        private static string accessToken = "";
        private static readonly string ifollamaApiUrl = "https://longmanrd.net/aiapi";
        private static string connectionString = "";
        private static readonly string conversationId = "3E48E5D3-6EE3-4E6D-B816- 8E6E9FB23C37";

        private static NpgsqlConnection? readConnection;
        private static readonly HashSet<string> updatedLanguages = [];

        private static readonly string sendPromptUrl = $"{ifollamaApiUrl}?conversationId={Uri.EscapeDataString(conversationId)}&dest=chat";
        private static readonly string insertQuery = "INSERT INTO dbo.languageentry( code, entry, lang) VALUES(@code, @entry, @lang);";
        private static readonly string updateLanguageQuery = "UPDATE dbo.language SET version = @version WHERE code = @code";

        static async Task Main()
        {
            Console.WriteLine("BTTranslate started.");
            await AcquireTokenAsync();
            BuildConnStr();

            try
            {
                using (var reader = UpdatableEntriesReader())
                {
                    using (var httpClient = new HttpClient())
                    {
                        await SubmitBasicIntructions(httpClient);

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

                                string translation = await RequestTranslation(httpClient, englishentry, langcode);

                                if (translation != string.Empty)
                                    InsertTranslationEntry(writeConnection, langcode, entrycode, translation);
                            }
                        }
                    }

                    readConnection?.Close();
                }

                // Update the dbo.language table with the new version for each language code
                UpdateLanguageVersions();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }

            Console.WriteLine("BTTranslate processing complete.");
        }

        private static async Task SubmitBasicIntructions(HttpClient httpClient)
        {
            string promptJson =
                "I want to send you a series of English words or phrases and language codes. " +
                "In each case, I want a single translation of the English phrase in the modern language denoted by the language code. " +
                "The translation in your response MUST be enclosed within <trans></trans> tags. " +
                "It is critically important you do not give me any other extra tags, especially between the <trans> and </trans> tags in any reponse. " +
                "It is critically important that only one translation is included in any reponse. " +
                "Please ensure that your response adheres to this format strictly. " +
                "If in doubt, the terms relate to BloodBowl, rugby, competitions, teams, or sports in general, or be captions or actions of a software application. " +
                "The term 'BreakTackle' should remain unchanged in any translation.";

            var requestContent = new StringContent(JsonConvert.SerializeObject(promptJson), Encoding.UTF8, "application/json");

            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.PostAsync(sendPromptUrl, requestContent);

            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var stream = new StreamReader(responseStream);
            _ = await stream.ReadToEndAsync();
        }

        private static async Task SubmitErrorInstruction(HttpClient httpClient, string error)
        {
            string promptJson = $"Your last response was no good because {error}.  Please remember this.";

            var instructionContent = new StringContent(JsonConvert.SerializeObject(promptJson), Encoding.UTF8, "application/json");

            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.PostAsync(sendPromptUrl, instructionContent);

            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync();
            using var stream = new StreamReader(responseStream);
            _ = await stream.ReadToEndAsync();
        }

        private static async Task<string> RequestTranslation(HttpClient httpClient, string englishentry, string langcode)
        {
            int retries = 3;
            string result = "";
            string promptJson = JsonConvert.SerializeObject($"Please translate '{englishentry}' into the language denoted by '{langcode}'");
            var requestContent = new StringContent(promptJson, Encoding.UTF8, "application/json");

            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            while (retries > 0)
            {
                try
                {
                    using (var response = await httpClient.PostAsync(sendPromptUrl, requestContent))
                    {

                        response.EnsureSuccessStatusCode();

                        using var responseStream = await response.Content.ReadAsStreamAsync();
                        using var stream = new StreamReader(responseStream);
                        var responseContent = await stream.ReadToEndAsync();

                        result = ExtractTranslationFromResponse(responseContent, langcode);
                        retries = 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                    await SubmitErrorInstruction(httpClient, ex.Message);
                    result = "";
                    retries--;
                    if (retries <= 0)
                        Console.WriteLine("maximum retries reached - giving up");
                    else
                        Console.WriteLine($"retrying ({retries} left)");
                }
            }
            return result;
        }

        private static void InsertTranslationEntry(NpgsqlConnection writeConnection, string langcode, string entrycode, string translation)
        {
            Console.WriteLine($"Writing '{entrycode}', '{translation}', '{langcode}'");
            using (var insertCmd = new NpgsqlCommand(insertQuery, writeConnection))
            {
                insertCmd.Parameters.AddWithValue("@code", entrycode);
                insertCmd.Parameters.AddWithValue("@entry", translation.Trim());
                insertCmd.Parameters.AddWithValue("@lang", langcode);
                insertCmd.ExecuteNonQuery();
            }

            // Add the language code to the HashSet
            updatedLanguages.Add(langcode);
        }

        private static void UpdateLanguageVersions()
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();

            foreach (var langcode in updatedLanguages)
            {

                using var updateCmd = new NpgsqlCommand(updateLanguageQuery, connection);
                updateCmd.Parameters.AddWithValue("@version", Guid.NewGuid());
                updateCmd.Parameters.AddWithValue("@code", langcode);
                updateCmd.ExecuteNonQuery();

                Console.WriteLine($"Language Updated : {langcode}");
            }
        }

        private static string ExtractTranslationFromResponse(string responseContent, string langcode)
        {
            // Log the raw response for debugging
            // Console.WriteLine("Raw Response: " + responseContent);

            // Remove the think section if present
            string cleanedResponseContent = Regex.Replace(responseContent, "<think>.*?</think>", string.Empty, RegexOptions.Singleline);

            // Find translation between <trans> and </trans>
            int startIndex = cleanedResponseContent.IndexOf("<trans>");
            if (startIndex == -1)
                throw new Exception("The start tag '<trans>' was not found in the response content.");

            startIndex += "<trans>".Length;

            int endIndex = cleanedResponseContent.IndexOf("</trans>", startIndex);
            if (endIndex == -1)
                throw new Exception("The end tag '</trans>' was not found in the response content.");

            // Extract the translation
            return cleanedResponseContent[startIndex..endIndex].Trim();
        }

        private static NpgsqlDataReader UpdatableEntriesReader()
        {
            readConnection = new NpgsqlConnection(connectionString);
            readConnection.Open();
            // Query to retrieve records needing translation; update table/column names as required
            string selectQuery =
              "WITH foreignlanguages AS  " +
              "(  " +
              "  SELECT l.code AS langcode  " +
              "  FROM dbo.LANGUAGE l  " +
              "  WHERE l.code <> 'en'  " +
              "), " +
              "englishentries AS  " +
              "(  " +
              "  SELECT code, entry  " +
              "  FROM dbo.languageentry  " +
              "  WHERE lang = 'en'  " +
              ") " +
              "SELECT fl.langcode, ee.code AS entrycode, ee.entry AS englishentry " +
              "FROM foreignlanguages fl " +
              "CROSS JOIN englishentries ee " +
              "LEFT JOIN dbo.languageentry fe ON fe.code = ee.code AND fe.lang = fl.langcode " +
              "WHERE fe.code IS NULL; ";

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

            accessToken = await GetAccessTokenAsync(tokenEndpoint, clientId, clientSecret, scope);
            if (string.IsNullOrEmpty(accessToken)) 
                throw new Exception("Failed to obtain access token."); 
        }

        private static void BuildConnStr()
        {
            // Get the host and password from environment variables
            string? host = Environment.GetEnvironmentVariable("BT_DBSERVER");
            string? password = Environment.GetEnvironmentVariable("BT_LANGPASS");

            // Throw an error if either environment variable is not found
            if (string.IsNullOrEmpty(host))
            {
                throw new Exception("No BT_DBSERVER.");
            }

            if (string.IsNullOrEmpty(password))
            {
                throw new Exception("No BT_LANGPASS.");
            }

            // Database connection details – update with your actual connection string
            connectionString = $"Host={host};Username=languagemanager;Password={password};Database=RozeBowl";
        }

        static async Task<string> GetAccessTokenAsync(string tokenEndpoint, string clientId, string clientSecret, string scope)
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
            var response = await httpClient.PostAsync(tokenEndpoint, content);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine("Token request failed: " + response.StatusCode);
                return "";
            }

            string json = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(json);
            return tokenResponse?.access_token ?? "";
        }
    }

    // Class for deserializing the token response JSON.
    public class TokenResponse
    {
        public string? access_token { get; set; }
        public int expires_in { get; set; }
        public string? token_type { get; set; }
    }
}


