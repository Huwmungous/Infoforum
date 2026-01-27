using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeZip.Core;

/// <summary>
/// Configuration settings for CodeZip operations.
/// </summary>
public sealed class CodeZipConfig
{
    private static readonly string DefaultWindowsOutputDir = @"C:\temp\CodeZipperData";
    private static readonly string DefaultLinuxOutputDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "temp", "CodeZipperData");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Gets or sets the output directory for generated zip files.
    /// </summary>
    public string OutputDirectory { get; set; } = GetDefaultOutputDirectory();

    /// <summary>
    /// Gets or sets the number of days to retain zip files before pruning.
    /// </summary>
    public int RetentionDays { get; set; } = 1;

    /// <summary>
    /// Gets or sets whether to automatically prune old zip files on each run.
    /// </summary>
    public bool PruneOnRun { get; set; } = true;

    /// <summary>
    /// Gets or sets additional directories to exclude (beyond the built-in exclusions).
    /// </summary>
    public List<string> AdditionalExcludedDirectories { get; set; } = [];

    /// <summary>
    /// Gets or sets additional file patterns to exclude (beyond the built-in exclusions).
    /// </summary>
    public List<string> AdditionalExcludedFilePatterns { get; set; } = [];

    /// <summary>
    /// Gets the path to the configuration file.
    /// </summary>
    public static string ConfigFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".codeship",
        "config.json");

    /// <summary>
    /// Gets the default output directory based on the current operating system.
    /// </summary>
    public static string GetDefaultOutputDirectory() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? DefaultWindowsOutputDir
            : DefaultLinuxOutputDir;

    /// <summary>
    /// Loads the configuration from disk, or creates a default configuration if none exists.
    /// </summary>
    public static CodeZipConfig Load()
    {
        if(!File.Exists(ConfigFilePath))
        {
            var defaultConfig = new CodeZipConfig();
            defaultConfig.Save();
            return defaultConfig;
        }

        try
        {
            var json = File.ReadAllText(ConfigFilePath);
            return JsonSerializer.Deserialize<CodeZipConfig>(json, JsonOptions) ?? new CodeZipConfig();
        }
        catch
        {
            return new CodeZipConfig();
        }
    }

    /// <summary>
    /// Saves the current configuration to disk.
    /// </summary>
    public void Save()
    {
        var directory = Path.GetDirectoryName(ConfigFilePath);
        if(!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(ConfigFilePath, json);
    }
}