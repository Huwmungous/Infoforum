using SfD.Mcp.Protocol.Models;
using System.Text.Json.Serialization;

namespace PlaywrightMcpServer.Models;



public record PlaywrightToolInfo
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