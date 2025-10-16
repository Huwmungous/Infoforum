namespace SqliteMcpServer.Models;

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

public record QueryResult
{
    public bool Success { get; init; }
    public List<Dictionary<string, object?>>? Rows { get; init; }
    public int RowCount { get; init; }
    public string? Error { get; init; }
}

public record ExecuteResult
{
    public bool Success { get; init; }
    public int RowsAffected { get; init; }
    public string? Error { get; init; }
}