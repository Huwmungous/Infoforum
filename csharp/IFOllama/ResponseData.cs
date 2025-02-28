using Newtonsoft.Json;

namespace IFOllama
{
    public partial class ConversationContextManager
    {
        private class ResponseData
        {
            [JsonProperty("message")]
            public string Message { get; set; } = string.Empty;

            [JsonProperty("timestamp")]
            public DateTime Timestamp { get; set; }

            public ResponseData() { }

            public ResponseData(string message, DateTime timestamp)
            {
                Message = message;
                Timestamp = timestamp;
            }

            public void Serialize(JsonWriter writer)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("Message");
                writer.WriteValue(Message);
                writer.WritePropertyName("Timestamp");
                writer.WriteValue(Timestamp.ToString());
                writer.WriteEndObject();
            }
        }
    }

}