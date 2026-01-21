namespace CodeFormatterMcpServer.Models;

public class FormatResult
{
    public bool Success { get; set; }
    public string? FormattedCode { get; set; }
    public string Message { get; set; } = "";
    public string[]? Errors { get; set; }
}