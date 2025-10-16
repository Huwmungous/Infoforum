namespace PlaywrightMcpServer.Models;

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

public record NavigationResult
{
    public bool Success { get; init; }
    public string? Url { get; init; }
    public string? Title { get; init; }
    public string? Content { get; init; }
    public string? Error { get; init; }
}

public record ScreenshotResult
{
    public bool Success { get; init; }
    public string? Base64Image { get; init; }
    public string? FilePath { get; init; }
    public string? Error { get; init; }
}

public record EvaluationResult
{
    public bool Success { get; init; }
    public object? Data { get; init; }
    public string? Error { get; init; }
}