namespace SqliteMcpServer.Models;



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