using System.Text.Json;
using System.Text.Json.Serialization;

namespace IFOllama
{
    public partial class ConversationContextManager
    {
        private class ResponseData
        {
            [JsonPropertyName("message")]
            public string Message { get; set; } = string.Empty;

            [JsonPropertyName("timestamp")]
            public DateTime Timestamp { get; set; }

            public ResponseData() { }

            public ResponseData(string message, DateTime timestamp)
            {
                Message = message;
                Timestamp = timestamp;
            }

            public void Serialize(Utf8JsonWriter writer)
            {
                writer.WriteStartObject();
                writer.WriteString("message", Message);
                writer.WriteString("timestamp", Timestamp.ToString("o")); // ISO 8601 format
                writer.WriteEndObject();
            }
        }
    }
}
