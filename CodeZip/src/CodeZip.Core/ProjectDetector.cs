namespace CodeZip.Core;

/// <summary>
/// Detects project types within a directory by examining marker files.
/// </summary>
public static class ProjectDetector
{
    private static readonly Dictionary<string, ProjectType> FileMarkers = new(StringComparer.OrdinalIgnoreCase)
    {
        // Delphi markers
        { ".dproj", ProjectType.Delphi },
        { ".dpr", ProjectType.Delphi },
        { ".dpk", ProjectType.Delphi },
        { ".groupproj", ProjectType.Delphi },

        // C# markers
        { ".csproj", ProjectType.CSharp },
        { ".sln", ProjectType.CSharp },
        { ".fsproj", ProjectType.CSharp },
        { ".vbproj", ProjectType.CSharp },

        // TypeScript marker
        { "tsconfig.json", ProjectType.TypeScript }
    };

    /// <summary>
    /// Detects all project types present in the specified directory.
    /// </summary>
    /// <param name="rootPath">The root directory to scan.</param>
    /// <returns>A combination of detected project types.</returns>
    public static ProjectType DetectProjectTypes(string rootPath)
    {
        var detectedTypes = ProjectType.None;

        if (!Directory.Exists(rootPath))
        {
            return ProjectType.Generic;
        }

        // Check for marker files in the root and immediate subdirectories
        var searchPaths = new List<string> { rootPath };
        searchPaths.AddRange(Directory.GetDirectories(rootPath).Take(50));

        foreach (var searchPath in searchPaths)
        {
            detectedTypes |= DetectInDirectory(searchPath);
        }

        // Check package.json for React/Angular detection
        detectedTypes |= DetectFromPackageJson(rootPath);

        return detectedTypes == ProjectType.None ? ProjectType.Generic : detectedTypes;
    }

    private static ProjectType DetectInDirectory(string directory)
    {
        var detected = ProjectType.None;

        try
        {
            foreach (var file in Directory.GetFiles(directory))
            {
                var fileName = Path.GetFileName(file);
                var extension = Path.GetExtension(file);

                if (FileMarkers.TryGetValue(fileName, out var typeByName))
                {
                    detected |= typeByName;
                }
                else if (FileMarkers.TryGetValue(extension, out var typeByExt))
                {
                    detected |= typeByExt;
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Skip directories we can't access
        }

        return detected;
    }

    private static ProjectType DetectFromPackageJson(string rootPath)
    {
        var packageJsonPath = Path.Combine(rootPath, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            return ProjectType.None;
        }

        var detected = ProjectType.Node;

        try
        {
            var content = File.ReadAllText(packageJsonPath);

            if (content.Contains("\"react\"", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("\"next\"", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("\"gatsby\"", StringComparison.OrdinalIgnoreCase))
            {
                detected |= ProjectType.React;
            }

            if (content.Contains("\"@angular/core\"", StringComparison.OrdinalIgnoreCase))
            {
                detected |= ProjectType.Angular;
            }

            if (File.Exists(Path.Combine(rootPath, "angular.json")))
            {
                detected |= ProjectType.Angular;
            }
        }
        catch
        {
            // If we can't read the file, just return Node type
        }

        return detected;
    }

    /// <summary>
    /// Gets a human-readable description of the detected project types.
    /// </summary>
    public static string GetDescription(ProjectType types)
    {
        if (types == ProjectType.None || types == ProjectType.Generic)
        {
            return "Generic";
        }

        var descriptions = new List<string>();

        if (types.HasFlag(ProjectType.Delphi)) descriptions.Add("Delphi");
        if (types.HasFlag(ProjectType.CSharp)) descriptions.Add("C#/.NET");
        if (types.HasFlag(ProjectType.Angular)) descriptions.Add("Angular");
        else if (types.HasFlag(ProjectType.React)) descriptions.Add("React");
        else if (types.HasFlag(ProjectType.Node)) descriptions.Add("Node.js");
        if (types.HasFlag(ProjectType.TypeScript) && !types.HasFlag(ProjectType.Angular) && !types.HasFlag(ProjectType.React))
        {
            descriptions.Add("TypeScript");
        }

        return string.Join(" + ", descriptions);
    }
}
