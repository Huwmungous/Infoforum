namespace UiComponentConverterMcpServer.Models;

public record McpRequest
{
    public string Jsonrpc { get; init; } = "2.0";
    public required string Method { get; init; }
    public object? Params { get; init; }
    public required object Id { get; init; }
}

public record McpResponse
{
    public string Jsonrpc { get; init; } = "2.0";
    public object? Result { get; init; }
    public McpError? Error { get; init; }
    public required object Id { get; init; }
}

public record McpError
{
    public required int Code { get; init; }
    public required string Message { get; init; }
}

public record ToolInfo
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required object InputSchema { get; init; }
}

public record DelphiForm
{
    public required string FormName { get; init; }
    public required string ClassName { get; init; }
    public Dictionary<string, object> Properties { get; init; } = new();
    public List<DelphiComponent> Components { get; init; } = new();
}

public record DelphiComponent
{
    public required string Name { get; init; }
    public required string ComponentType { get; init; }
    public Dictionary<string, object> Properties { get; init; } = new();
    public List<string> EventHandlers { get; init; } = new();
    public List<DelphiComponent> Children { get; init; } = new();
}

public record ComponentAnalysis
{
    public int TotalComponents { get; init; }
    public Dictionary<string, int> ComponentTypeCounts { get; init; } = new();
    public List<string> EventHandlers { get; init; } = new();
    public List<string> DataBoundComponents { get; init; } = new();
}

public record GeneratedComponent
{
    public bool Success { get; init; }
    public string? ComponentCode { get; init; }
    public string? StyleCode { get; init; }
    public string? StateCode { get; init; }
    public string? Message { get; init; }
    public string[]? Warnings { get; init; }
}