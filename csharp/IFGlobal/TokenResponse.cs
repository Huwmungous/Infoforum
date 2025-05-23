namespace IFGlobal
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public partial class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }

    public partial class TokenResponse
    {
        internal static readonly JsonSerializerOptions _options = new()
        {
            ReadCommentHandling = JsonCommentHandling.Skip,
            PropertyNameCaseInsensitive = false
        };

        public static TokenResponse? FromJson(string json) =>
            JsonSerializer.Deserialize<TokenResponse>(json, _options);

        public string ToJson() =>
            JsonSerializer.Serialize(this, _options);
    }

    public static class SerializeTokenResponse
    {
        public static string ToJson(this TokenResponse self) =>
            JsonSerializer.Serialize(self, TokenResponse._options);
    }
}
