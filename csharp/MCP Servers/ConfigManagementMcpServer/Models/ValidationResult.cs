namespace ConfigManagementMcpServer.Models;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string[] Errors { get; set; } = [];
    public string[] Warnings { get; set; } = [];
}