namespace ConfigManagementMcpServer.Models;

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

public record ConfigResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public object? Data { get; init; }
}

public record ValidationResult
{
    public bool IsValid { get; init; }
    public string[] Errors { get; init; } = Array.Empty<string>();
    public string[] Warnings { get; init; } = Array.Empty<string>();
}