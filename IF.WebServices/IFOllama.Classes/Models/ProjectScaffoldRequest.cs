namespace IFOllama.Classes.Models;

/// <summary>
/// Request to scaffold a new project in a repository.
/// </summary>
public class ProjectScaffoldRequest
{
    public ProjectType ProjectType { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Sub-template within the project type, e.g. "webapi", "classlib", "console" for .NET.
    /// </summary>
    public string Template { get; set; } = string.Empty;

    /// <summary>
    /// Optional subdirectory within the repo to create the project.
    /// </summary>
    public string SubDirectory { get; set; } = string.Empty;
}
