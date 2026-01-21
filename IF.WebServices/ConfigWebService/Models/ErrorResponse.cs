using System.Text.Json.Serialization;

namespace ConfigWebService.Models
{
    /// <summary>
    /// Standard error response model
    /// </summary>
    public class ErrorResponse
    {
        /// <summary>
        /// Error message
        /// </summary>
        [JsonPropertyName("error")]
        public string Error { get; set; } = "";
    }
}