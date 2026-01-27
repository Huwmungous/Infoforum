using Microsoft.Extensions.FileSystemGlobbing;

namespace CodeZip.Core;

/// <summary>
/// Defines exclusion rules for different project types.
/// </summary>
public sealed class ExclusionRules
{
    private static readonly HashSet<string> CommonExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".svn", ".hg", ".vs", ".vscode", ".idea", ".fleet",
        "__pycache__", ".pytest_cache", ".mypy_cache", "logs", ".temp", ".tmp",
        // Always exclude - these are never source code regardless of project type detection
        "node_modules", "dist"
    };

    private static readonly HashSet<string> CommonExcludedFilePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "*.user", "*.suo", "*.db", "*.db-shm", "*.db-wal", "thumbs.db",
        ".ds_store", "desktop.ini", "*.log", "*.bak", "*.tmp", "*.temp", "*.cache"
    };

    private static readonly HashSet<string> CSharpExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "packages", ".nuget", "TestResults", "AppPackages",
        "BundleArtifacts", ".artifacts", "artifacts", "publish", "out"
    };

    private static readonly HashSet<string> CSharpExcludedFilePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "*.dll", "*.exe", "*.pdb", "*.nupkg", "*.snupkg",
        "project.lock.json", "*.nuget.props", "*.nuget.targets"
    };

    private static readonly HashSet<string> NodeExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "build", ".next", ".nuxt", ".output", ".angular",
        ".cache", "coverage", ".nyc_output", "storybook-static", ".parcel-cache",
        ".turbo", ".vercel", ".netlify", "bower_components", "jspm_packages"
    };

    private static readonly HashSet<string> NodeExcludedFilePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "*.min.js", "*.min.css", "*.bundle.js", "*.chunk.js", "*.map",
        "package-lock.json", "yarn.lock", "pnpm-lock.yaml", ".env.local", ".env.*.local"
    };

    private static readonly HashSet<string> DelphiExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "__history", "__recovery", "__astcache", "Win32", "Win64", "Linux64",
        "OSX64", "Android", "Android64", "iOSDevice64", "iOSSimARM64",
        "Debug", "Release", "DCU", "DCUs", "Output", "Bin", "Exe"
    };

    private static readonly HashSet<string> DelphiExcludedFilePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "*.dcu", "*.dcp", "*.drc", "*.map", "*.dres", "*.rsm", "*.tds", "*.dof",
        "*.exe", "*.dll", "*.bpl", "*.bpi", "*.o", "*.a", "*.so", "*.dylib",
        "*.local", "*.identcache", "*.projdata", "*.tvsconfig", "*.dsk", "*.stat", "*.~*"
    };

    private readonly HashSet<string> _excludedDirectories;
    private readonly Matcher _fileMatcher;

    /// <summary>
    /// Initializes exclusion rules for the specified project types.
    /// </summary>
    public ExclusionRules(ProjectType projectTypes, CodeZipConfig? config = null)
    {
        _excludedDirectories = new HashSet<string>(CommonExcludedDirectories, StringComparer.OrdinalIgnoreCase);
        var excludedPatterns = new HashSet<string>(CommonExcludedFilePatterns, StringComparer.OrdinalIgnoreCase);

        if(projectTypes.HasFlag(ProjectType.CSharp))
        {
            _excludedDirectories.UnionWith(CSharpExcludedDirectories);
            excludedPatterns.UnionWith(CSharpExcludedFilePatterns);
        }

        if(projectTypes.HasFlag(ProjectType.Node) || projectTypes.HasFlag(ProjectType.React) ||
            projectTypes.HasFlag(ProjectType.Angular) || projectTypes.HasFlag(ProjectType.TypeScript))
        {
            _excludedDirectories.UnionWith(NodeExcludedDirectories);
            excludedPatterns.UnionWith(NodeExcludedFilePatterns);
        }

        if(projectTypes.HasFlag(ProjectType.Delphi))
        {
            _excludedDirectories.UnionWith(DelphiExcludedDirectories);
            excludedPatterns.UnionWith(DelphiExcludedFilePatterns);
        }

        if(config != null)
        {
            _excludedDirectories.UnionWith(config.AdditionalExcludedDirectories);
            excludedPatterns.UnionWith(config.AdditionalExcludedFilePatterns);
        }

        _fileMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach(var pattern in excludedPatterns)
        {
            _fileMatcher.AddInclude(pattern);
        }
    }

    /// <summary>
    /// Determines if a directory should be excluded.
    /// </summary>
    public bool ShouldExcludeDirectory(string directoryName) =>
        _excludedDirectories.Contains(directoryName);

    /// <summary>
    /// Determines if a file should be excluded based on its name.
    /// </summary>
    public bool ShouldExcludeFile(string fileName) =>
        _fileMatcher.Match(fileName).HasMatches;

    /// <summary>
    /// Gets a copy of the excluded directory names.
    /// </summary>
    public IReadOnlySet<string> ExcludedDirectories => _excludedDirectories;
}