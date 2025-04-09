using Newtonsoft.Json;

namespace IFGlobal
{
    public class TokenResponse
    {
        [JsonProperty("access_token")]
        public string? AccessToken { get; set; } // Maps to "access_token" in JSON

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; } // Maps to "expires_in" in JSON

        [JsonProperty("token_type")]
        public string? TokenType { get; set; } // Maps to "token_type" in JSON
    }
}