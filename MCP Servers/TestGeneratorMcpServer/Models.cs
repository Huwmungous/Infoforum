using System.Text.Json;
using System.Text.Json.Serialization;
namespace TestGeneratorMcpServer;
public class McpRequest { 
    [JsonPropertyName("jsonrpc")] 
    public string Jsonrpc { get; set; } = "2.0"; 
    
    [JsonPropertyName("id")] 
    public JsonElement? Id { get; set; }
    
    [JsonPropertyName("method")] 
    public string Method { get; set; } = "";
    
    [JsonPropertyName("params")] 
    public McpParams? Params { get; set; } 
}

public class McpParams { 
    [JsonPropertyName("name")] 
    public string? Name { get; set; } 
    
    [JsonPropertyName("arguments")] 
    public JsonElement? Arguments { get; set; } 
}

public class McpResponse { 
    [JsonPropertyName("jsonrpc")] 
    public string Jsonrpc { get; set; } = "2.0";
    
    [JsonPropertyName("id")] 
    public JsonElement? Id { get; set; }
    
    [JsonPropertyName("result")] 
    public object? Result { get; set; }
    
    [JsonPropertyName("error")] 
    public McpError? Error { get; set; }
}

public class McpError { 
    [JsonPropertyName("code")] 
    public int Code { get; set; } 
    
    [JsonPropertyName("message")] 
    public string Message { get; set; } = ""; }
