namespace IFOllama.Classes.Models;

public record ToolCategory(string Id, string Name, string Description)
{
    public bool Enabled { get; set; } = false;
}

public static class ToolCategories
{
    public static List<ToolCategory> GetAll() =>
    [
        new("file-ops", "File Operations", "Read, write, search files"),
        new("git", "Git Operations", "Status, log, diff, commit"),
        new("delphi-analysis", "Delphi Analysis", "Parse DFM, extract SQL, analyse dependencies"),
        new("database", "Database", "Query Firebird, list tables"),
        new("code-gen", "Code Generation", "Generate C# from Delphi"),
        new("documentation", "Documentation", "Generate docs, analyse code"),
        new("build", "Build Tools", "Compile, test, deploy"),
        new("search", "Search", "Find code patterns, search projects")
    ];
}
