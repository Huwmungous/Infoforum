using System.Text.Json.Serialization;

namespace ConfigWebService.Models
{
    public class Bootstrap
    {
        [JsonPropertyName("realm")]
        public string Realm { get; set; } = "";

        [JsonPropertyName("clientId")]
        public string ClientId { get; set; } = "";

        [JsonPropertyName("openIdConfig")]
        public string OpenIdConfig { get; set; } = "";

        [JsonPropertyName("loggerService")]
        public string LoggerService { get; set; } = "";

        [JsonPropertyName("logLevel")]
        public LogLevel LogLevel { get; set; } = LogLevel.Information;

        [JsonPropertyName("clientSecret")]
        public string? ClientSecret { get; set; }  // Only for service type

    }
}
