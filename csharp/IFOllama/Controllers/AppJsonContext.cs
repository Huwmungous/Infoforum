using System.Text.Json.Serialization;
namespace IFOllama.Controllers
{
    [JsonSerializable(typeof(ChatChunk))]
public partial class AppJsonContext : JsonSerializerContext
{
}
}