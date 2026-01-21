using System.Text.Json;

namespace SfD.Mcp.Protocol.Models
{
    public class ToolDefinition
    {
        public string ServerName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public JsonElement InputSchema { get; set; }
    }
}
