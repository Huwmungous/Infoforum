using Newtonsoft.Json;
using Npgsql;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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
        private static readonly string conversationId = Guid.NewGuid().ToString(); //"3E48E5D3-6EE3-4E6D-B816-8E6E9FB23C37";

        private static NpgsqlConnection? readConnection;
        private static readonly HashSet<string> updatedLanguages = [];

        private static readonly string sendPromptUrl = $"{ifollamaApiUrl}?conversationId={Uri.EscapeDataString(conversationId)}&dest=chat";
        private static readonly string insertQuery = "INSERT INTO dbo.languageentry( code, entry, lang) VALUES(@code, @entry, @lang);";
        private static readonly string updateLanguageQuery = "UPDATE dbo.language SET version = @version WHERE code = @code";

        static void Main()
        {
            Console.WriteLine("BTTranslate started.");
            try
            {
                AcquireToken();
                BuildConnStr();

                using (var reader = UpdatableEntriesReader())
                {
                    using (var httpClient = new HttpClient())
                    {
                        SubmitBasicIntructions(httpClient);

                        using var writeConnection = new NpgsqlConnection(connectionString);
                        writeConnection.Open();
                        string englishentry = "";
                        while (reader.Read())
                        {
                            if (reader.GetString(3) != englishentry)
                            {
                                englishentry = reader.GetString(3);
                                Console.WriteLine();
                                Console.WriteLine($"*** Translating '{englishentry}' ***");
                                Console.WriteLine(new string('-', englishentry.Length + 22));
                            }

                            string language = reader.GetString(0);
                            string langcode = reader.GetString(1);
                            string entrycode = reader.GetString(2);

                            string translation = RequestTranslation(httpClient, englishentry, language);

                            if (!string.IsNullOrEmpty(translation))
                                InsertTranslationEntry(writeConnection, langcode, entrycode, translation);
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

        private static void SubmitBasicIntructions(HttpClient httpClient)
        {
            string promptJson =
                "I want to send you a series of English words or phrases to translate into various languages. " +
                "The translation in your response MUST be enclosed within <trans></trans> tags and must contain nothing but the translated version of the English word or phrase. " +
                "It is critically important you do not give me any other extra tags, especially between the <trans> and </trans> tags in any reponse. " +
                "It is critically important that only one translation is included in any reponse. " +
                "It is critically important that you provide the response even if you have already been asked for that translation. " +
                "Please ensure that your response adheres to this format strictly. " +
                "If in doubt, the terms relate to BloodBowl, rugby, competitions, teams, or sports in general, or be captions or actions of a software application. " +
                "The term 'BreakTackle' should remain unchanged in any translation.";

            var requestContent = new StringContent(JsonConvert.SerializeObject(promptJson), Encoding.UTF8, "application/json");

            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = httpClient.PostAsync(sendPromptUrl, requestContent).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            using var responseStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            using var stream = new StreamReader(responseStream);
            _ = stream.ReadToEnd();
        }

        private static void SubmitErrorInstruction(HttpClient httpClient, string error)
        {
            string promptJson = $"Your last response was no good because {error}.  Please remember this.";
            var instructionContent = new StringContent(JsonConvert.SerializeObject(promptJson), Encoding.UTF8, "application/json");

            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var response = httpClient.PostAsync(sendPromptUrl, instructionContent).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();

            using var responseStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
            using var stream = new StreamReader(responseStream);
            _ = stream.ReadToEnd();
        }

        private static string RequestTranslation(HttpClient httpClient, string englishentry, string language)
        {
            int retries = 3;
            string result = "";
            string promptJson = JsonConvert.SerializeObject($"I want the {language} equivalent of the English '{englishentry}' enclosed in tags '<trans' and '</trans>");
            var requestContent = new StringContent(promptJson, Encoding.UTF8, "application/json");

            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            while (retries > 0)
            {
                try
                {
                    using (var response = httpClient.PostAsync(sendPromptUrl, requestContent).GetAwaiter().GetResult())
                    {
                        response.EnsureSuccessStatusCode();

                        using var responseStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                        using var stream = new StreamReader(responseStream);
                        var responseContent = stream.ReadToEnd();

                        result = ExtractTranslationFromResponse(responseContent, language);
                        retries = 0;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error: " + ex.Message);
                    //  SubmitErrorInstruction(httpClient, ex.Message);
                    result = "";
                    retries--;
                    if (retries <= 0)
                    {
                        Console.WriteLine("maximum retries reached - giving up");
                        throw;
                    }
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
            // Remove the think section if present
            string cleanedResponseContent;
            int endThinkIndex = responseContent.IndexOf("</think>");
            if (endThinkIndex >= 0)
                cleanedResponseContent = responseContent[(endThinkIndex + "</think>".Length)..];
            else
                cleanedResponseContent = responseContent;

            // Find translation between <trans> and </trans>
            int transStartIndex = cleanedResponseContent.IndexOf("<trans>");
            if (transStartIndex == -1)
                throw new Exception("The start tag '<trans>' was not found in the response content.");

            transStartIndex += "<trans>".Length;
            int endIndex = cleanedResponseContent.IndexOf("</trans>", transStartIndex);
            if (endIndex == -1)
                throw new Exception("The end tag '</trans>' was not found in the response content.");

            // Extract the translation
            return cleanedResponseContent[transStartIndex..endIndex].Trim();
        }

        private static NpgsqlDataReader UpdatableEntriesReader()
        {
            readConnection = new NpgsqlConnection(connectionString);
            readConnection.Open();
            // Query to retrieve records needing translation; update table/column names as required
            string selectQuery = @"
                WITH foreignlanguages AS  
                (  
                  SELECT l.english_name AS languagename, l.code AS language  
                  FROM dbo.language l  
                  WHERE l.code <> 'en'  
                ), 
                englishentries AS  
                (  
                  SELECT code, entry  
                  FROM dbo.languageentry  
                  WHERE lang = 'en'  
                ) 
                SELECT fl.languagename, fl.language, ee.code AS entrycode, ee.entry AS englishentry 
                FROM foreignlanguages fl 
                CROSS JOIN englishentries ee 
                LEFT JOIN dbo.languageentry fe ON fe.code = ee.code AND fe.lang = fl.language 
                WHERE fe.code IS NULL;";

            var result = new NpgsqlCommand(selectQuery, readConnection).ExecuteReader();
            return result;
        }

        private static void AcquireToken()
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

            accessToken = GetAccessToken(tokenEndpoint, clientId, clientSecret, scope);
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

