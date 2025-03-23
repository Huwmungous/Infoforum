using Npgsql;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace GenerateBTTranslations
{

    public partial class MainPage : ContentPage
    {
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _ifollamaApiUrl = "https://longmanrd.net/AiApi";
        private readonly string _connectionString = "Host=yourhost;Username=youruser;Password=yourpassword;Database=yourdb";

        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnGoButtonClicked(object sender, EventArgs e)
        {
            try
            {
                // 1. Query PostgreSQL for records.
                using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync();

                string selectQuery = "SELECT id, column_to_translate FROM your_table";
                using var cmd = new NpgsqlCommand(selectQuery, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    // For example, assume id and the text column
                    var recordId = reader.GetInt32(0);
                    var originalText = reader.GetString(1);

                    // 2. Call SendPrompt API to get the translation.
                    var conversationId = Guid.NewGuid().ToString();
                    var prompt = originalText; // you might adjust the prompt to include context
                    var sendPromptUrl = $"{_ifollamaApiUrl}?conversationId={Uri.EscapeDataString(conversationId)}";

                    var content = new StringContent(JsonConvert.SerializeObject(prompt), Encoding.UTF8, "application/json");
                    var response = await _httpClient.PostAsync(sendPromptUrl, content);
                    response.EnsureSuccessStatusCode();

                    var translation = await response.Content.ReadAsStringAsync();

                    // 3. Insert the translation into your database.
                    var insertQuery = "INSERT INTO translations (record_id, translated_text) VALUES (@id, @translatedText)";
                    using var insertCmd = new NpgsqlCommand(insertQuery, conn);
                    insertCmd.Parameters.AddWithValue("id", recordId);
                    insertCmd.Parameters.AddWithValue("translatedText", translation);
                    await insertCmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions (log or show an error message)
                await DisplayAlert("Error", ex.Message, "OK");
            }
        }
    }

}