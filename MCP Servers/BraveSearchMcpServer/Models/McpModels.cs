namespace BraveSearchMcpServer.Models;

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

public record SearchResult
{
    public required string Title { get; init; }
    public required string Url { get; init; }
    public string? Description { get; init; }
}

public record BraveSearchResponse
{
    public required SearchResult[] Results { get; init; }
    public int TotalResults { get; init; }
    public string? Query { get; init; }
}