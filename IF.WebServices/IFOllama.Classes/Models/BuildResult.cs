namespace IFOllama.Classes.Models;

/// <summary>
/// Captures the result of a dotnet build operation.
/// </summary>
public class BuildResult
{
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Errors { get; set; } = string.Empty;
    public ProjectType ProjectType { get; set; }
    public string BuildCommand { get; set; } = string.Empty;
    public List<BuildDiagnostic> Diagnostics { get; set; } = [];
}

public class BuildDiagnostic
{
    public string Severity { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public int Line { get; set; }
    public int Column { get; set; }
}
