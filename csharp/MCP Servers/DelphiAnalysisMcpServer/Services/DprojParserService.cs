using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DelphiAnalysisMcpServer.Services;

/// <summary>
/// Service for parsing Delphi .dproj project files (XML format).
/// Extracts metadata, search paths, compiler defines, and file references.
/// </summary>
public partial class DprojParserService(ILogger<DprojParserService> logger)
{
    private readonly ILogger<DprojParserService> _logger = logger;

    // MSBuild namespace used in .dproj files
    private static readonly XNamespace MsBuildNs = "http://schemas.microsoft.com/developer/msbuild/2003";

    #region LoggerMessage Definitions

    [LoggerMessage(Level = LogLevel.Information, Message = "Parsing .dproj file: {FilePath}")]
    private partial void LogParsingDproj(string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse .dproj file: {FilePath}")]
    private partial void LogDprojParseFailure(Exception ex, string filePath);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {Count} search paths in .dproj")]
    private partial void LogSearchPathsFound(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {Count} source files in .dproj")]
    private partial void LogSourceFilesFound(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Found {Count} build configurations")]
    private partial void LogBuildConfigsFound(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cross-referenced with .dpr: {ActiveCount} active units, {OrphanedCount} orphaned references")]
    private partial void LogCrossReference(int activeCount, int orphanedCount);

    #endregion

    /// <summary>
    /// Parses a .dproj file and extracts project metadata.
    /// </summary>
    public async Task<DprojMetadata> ParseAsync(string dprojPath)
    {
        LogParsingDproj(dprojPath);

        var metadata = new DprojMetadata
        {
            DprojPath = dprojPath,
            ProjectDirectory = Path.GetDirectoryName(dprojPath) ?? string.Empty
        };

        try
        {
            var content = await File.ReadAllTextAsync(dprojPath);
            var doc = XDocument.Parse(content);
            var root = doc.Root;

            if (root is null)
            {
                return metadata;
            }

            // Extract project GUID and name
            metadata.ProjectGuid = GetPropertyValue(root, "ProjectGuid") ?? string.Empty;
            metadata.MainSource = GetPropertyValue(root, "MainSource") ?? string.Empty;
            metadata.ProjectName = Path.GetFileNameWithoutExtension(dprojPath);

            // Extract framework type (VCL, FMX, Console)
            metadata.FrameworkType = DetectFrameworkType(root);

            // Extract Delphi version info
            metadata.DelphiVersion = GetPropertyValue(root, "ProjectVersion") ?? string.Empty;
            metadata.Platform = GetPropertyValue(root, "Platform") ?? "Win32";

            // Extract build configurations
            metadata.BuildConfigurations = ExtractBuildConfigurations(root);
            LogBuildConfigsFound(metadata.BuildConfigurations.Count);

            // Extract active configuration
            metadata.ActiveConfiguration = GetPropertyValue(root, "Config") ?? "Debug";
            metadata.ActivePlatform = metadata.Platform;

            // Extract compiler defines for active configuration
            metadata.CompilerDefines = ExtractCompilerDefines(root, metadata.ActiveConfiguration);

            // Extract search paths
            metadata.SearchPaths = ExtractSearchPaths(root, metadata.ActiveConfiguration);
            LogSearchPathsFound(metadata.SearchPaths.Count);

            // Extract unit scope names (namespace prefixes)
            metadata.UnitScopeNames = ExtractUnitScopeNames(root);

            // Extract all source files
            metadata.SourceFiles = ExtractSourceFiles(root);
            LogSourceFilesFound(metadata.SourceFiles.Count);

            // Extract form files
            metadata.FormFiles = ExtractFormFiles(root);

            // Extract resource files
            metadata.ResourceFiles = ExtractResourceFiles(root);

            // Extract package references
            metadata.PackageReferences = ExtractPackageReferences(root);

            // Extract output paths
            metadata.OutputDirectory = GetPropertyValue(root, "DCC_ExeOutput") ?? string.Empty;
            metadata.UnitOutputDirectory = GetPropertyValue(root, "DCC_DcuOutput") ?? string.Empty;

            // Extract version info
            metadata.VersionInfo = ExtractVersionInfo(root);

            // Resolve all paths to absolute
            ResolveAllPaths(metadata);
        }
        catch (Exception ex)
        {
            LogDprojParseFailure(ex, dprojPath);
        }

        return metadata;
    }

    /// <summary>
    /// Cross-references .dproj metadata with .dpr file to identify active vs orphaned files.
    /// </summary>
    public CrossReferenceResult CrossReferenceWithDpr(DprojMetadata metadata, HashSet<string> dprUnits)
    {
        var result = new CrossReferenceResult();

        foreach (var sourceFile in metadata.SourceFiles)
        {
            var unitName = Path.GetFileNameWithoutExtension(sourceFile.FileName);

            if (dprUnits.Contains(unitName, StringComparer.OrdinalIgnoreCase))
            {
                result.ActiveFiles.Add(sourceFile);
            }
            else
            {
                result.OrphanedFiles.Add(sourceFile);
            }
        }

        // Find units in .dpr but not in .dproj (might be in search paths)
        foreach (var unit in dprUnits)
        {
            var found = metadata.SourceFiles.Any(f =>
                Path.GetFileNameWithoutExtension(f.FileName)
                    .Equals(unit, StringComparison.OrdinalIgnoreCase));

            if (!found)
            {
                result.ExternalUnits.Add(unit);
            }
        }

        LogCrossReference(result.ActiveFiles.Count, result.OrphanedFiles.Count);
        return result;
    }

    /// <summary>
    /// Locates a unit file using the project's search paths.
    /// </summary>
    public string? LocateUnitFile(string unitName, DprojMetadata metadata)
    {
        var extensions = new[] { ".pas", ".pp", ".inc" };

        // First check project directory
        foreach (var ext in extensions)
        {
            var path = Path.Combine(metadata.ProjectDirectory, unitName + ext);
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Then check search paths
        foreach (var searchPath in metadata.ResolvedSearchPaths)
        {
            foreach (var ext in extensions)
            {
                var path = Path.Combine(searchPath, unitName + ext);
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all resolvable unit paths from the project.
    /// </summary>
    public List<string> GetAllResolvableUnitPaths(DprojMetadata metadata, HashSet<string> requiredUnits)
    {
        var paths = new List<string>();

        foreach (var unit in requiredUnits)
        {
            var path = LocateUnitFile(unit, metadata);
            if (path is not null)
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    #region Private Helper Methods

    private static string? GetPropertyValue(XElement root, string propertyName)
    {
        // Try with namespace first
        var element = root.Descendants(MsBuildNs + propertyName).FirstOrDefault();
        if (element is not null)
        {
            return element.Value;
        }

        // Try without namespace (some older .dproj files)
        element = root.Descendants(propertyName).FirstOrDefault();
        return element?.Value;
    }

    private static string DetectFrameworkType(XElement root)
    {
        var frameworkType = GetPropertyValue(root, "FrameworkType");
        if (!string.IsNullOrEmpty(frameworkType))
        {
            return frameworkType;
        }

        // Detect from uses or other hints
        // Check for FMX indicators
        var allContent = root.ToString();
        if (allContent.Contains("FMX.") || allContent.Contains("FireMonkey"))
        {
            return "FMX";
        }

        // Check for console app
        var appType = GetPropertyValue(root, "AppType");
        if (appType?.Equals("Console", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "Console";
        }

        // Default to VCL
        return "VCL";
    }

    private static List<BuildConfiguration> ExtractBuildConfigurations(XElement root)
    {
        var configs = new List<BuildConfiguration>();

        // Find all PropertyGroup elements with Condition attributes
        var propertyGroups = root.Descendants(MsBuildNs + "PropertyGroup")
            .Concat(root.Descendants("PropertyGroup"))
            .Where(pg => pg.Attribute("Condition") is not null);

        foreach (var pg in propertyGroups)
        {
            var condition = pg.Attribute("Condition")?.Value ?? string.Empty;

            // Parse condition like "'$(Cfg_1)'!=''" or "'$(Config)'=='Debug'"
            var configMatch = ConfigConditionRegex().Match(condition);
            if (configMatch.Success)
            {
                var configName = configMatch.Groups[1].Value;

                // Skip if already added
                if (configs.Any(c => c.Name.Equals(configName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var config = new BuildConfiguration
                {
                    Name = configName,
                    Defines = ExtractDefinesFromPropertyGroup(pg),
                    SearchPaths = ExtractSearchPathsFromPropertyGroup(pg),
                    OutputPath = GetElementValue(pg, "DCC_ExeOutput") ?? string.Empty,
                    IsDebug = configName.Contains("Debug", StringComparison.OrdinalIgnoreCase)
                };

                configs.Add(config);
            }
        }

        // If no configs found, add defaults
        if (configs.Count == 0)
        {
            configs.Add(new BuildConfiguration { Name = "Debug", IsDebug = true });
            configs.Add(new BuildConfiguration { Name = "Release", IsDebug = false });
        }

        return configs;
    }

    private static List<string> ExtractCompilerDefines(XElement root, string configuration)
    {
        var defines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Get base defines
        var baseDefines = GetPropertyValue(root, "DCC_Define");
        if (!string.IsNullOrEmpty(baseDefines))
        {
            foreach (var define in baseDefines.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                defines.Add(define.Trim());
            }
        }

        // Get configuration-specific defines
        var configPg = FindConfigurationPropertyGroup(root, configuration);
        if (configPg is not null)
        {
            var configDefines = GetElementValue(configPg, "DCC_Define");
            if (!string.IsNullOrEmpty(configDefines))
            {
                foreach (var define in configDefines.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    defines.Add(define.Trim());
                }
            }
        }

        // Add implicit defines based on configuration
        if (configuration.Contains("Debug", StringComparison.OrdinalIgnoreCase))
        {
            defines.Add("DEBUG");
        }
        else
        {
            defines.Add("RELEASE");
        }

        return [.. defines];
    }

    private static List<string> ExtractSearchPaths(XElement root, string configuration)
    {
        var paths = new List<string>();

        // Get base search paths
        var basePaths = GetPropertyValue(root, "DCC_UnitSearchPath");
        if (!string.IsNullOrEmpty(basePaths))
        {
            paths.AddRange(basePaths.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p) && p != "$(DCC_UnitSearchPath)"));
        }

        // Get configuration-specific paths
        var configPg = FindConfigurationPropertyGroup(root, configuration);
        if (configPg is not null)
        {
            var configPaths = GetElementValue(configPg, "DCC_UnitSearchPath");
            if (!string.IsNullOrEmpty(configPaths))
            {
                paths.AddRange(configPaths.Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p) && p != "$(DCC_UnitSearchPath)"));
            }
        }

        // Also check for namespace search paths
        var nsPaths = GetPropertyValue(root, "DCC_Namespace");
        if (!string.IsNullOrEmpty(nsPaths))
        {
            // These are namespace prefixes, not paths, but include them for reference
        }

        return [.. paths.Distinct()];
    }

    private static List<string> ExtractUnitScopeNames(XElement root)
    {
        var scopes = new List<string>();

        var unitScopes = GetPropertyValue(root, "DCC_UnitAlias");
        if (!string.IsNullOrEmpty(unitScopes))
        {
            scopes.AddRange(unitScopes.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()));
        }

        // Also check namespace prefixes
        var nsScopes = GetPropertyValue(root, "DCC_Namespace");
        if (!string.IsNullOrEmpty(nsScopes))
        {
            scopes.AddRange(nsScopes.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()));
        }

        return [.. scopes.Distinct()];
    }

    private static List<ProjectSourceFile> ExtractSourceFiles(XElement root)
    {
        var files = new List<ProjectSourceFile>();

        // Find DCCReference items (Delphi units)
        var dccRefs = root.Descendants(MsBuildNs + "DCCReference")
            .Concat(root.Descendants("DCCReference"));

        foreach (var dccRef in dccRefs)
        {
            var include = dccRef.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(include))
            {
                continue;
            }

            var file = new ProjectSourceFile
            {
                FileName = include,
                FileType = DetermineFileType(include),
                Form = GetElementValue(dccRef, "Form"),
                DesignClass = GetElementValue(dccRef, "DesignClass"),
                IsMainSource = false
            };

            files.Add(file);
        }

        // Find main source
        var mainSource = GetPropertyValue(root, "MainSource");
        if (!string.IsNullOrEmpty(mainSource))
        {
            var existing = files.FirstOrDefault(f =>
                f.FileName.Equals(mainSource, StringComparison.OrdinalIgnoreCase));

            if (existing is not null)
            {
                existing.IsMainSource = true;
            }
            else
            {
                files.Insert(0, new ProjectSourceFile
                {
                    FileName = mainSource,
                    FileType = DetermineFileType(mainSource),
                    IsMainSource = true
                });
            }
        }

        // Also find DelphiCompile items (alternative format)
        var delphiCompile = root.Descendants(MsBuildNs + "DelphiCompile")
            .Concat(root.Descendants("DelphiCompile"));

        foreach (var dc in delphiCompile)
        {
            var include = dc.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(include))
            {
                continue;
            }

            // Skip if already added
            if (files.Any(f => f.FileName.Equals(include, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            files.Add(new ProjectSourceFile
            {
                FileName = include,
                FileType = DetermineFileType(include)
            });
        }

        return files;
    }

    private static List<ProjectFormFile> ExtractFormFiles(XElement root)
    {
        var forms = new List<ProjectFormFile>();

        // Find form references from DCCReference with Form attribute
        var dccRefs = root.Descendants(MsBuildNs + "DCCReference")
            .Concat(root.Descendants("DCCReference"))
            .Where(r => r.Element(MsBuildNs + "Form") is not null ||
                       r.Element("Form") is not null);

        foreach (var dccRef in dccRefs)
        {
            var unitPath = dccRef.Attribute("Include")?.Value ?? string.Empty;
            var formName = GetElementValue(dccRef, "Form") ?? string.Empty;
            var designClass = GetElementValue(dccRef, "DesignClass");

            if (string.IsNullOrEmpty(unitPath))
            {
                continue;
            }

            // DFM file is typically same name as unit with .dfm extension
            var dfmPath = Path.ChangeExtension(unitPath, ".dfm");

            forms.Add(new ProjectFormFile
            {
                UnitPath = unitPath,
                DfmPath = dfmPath,
                FormName = formName,
                FormType = designClass ?? "TForm"
            });
        }

        return forms;
    }

    private static List<string> ExtractResourceFiles(XElement root)
    {
        var resources = new List<string>();

        // Find RcCompile items (resource files)
        var rcItems = root.Descendants(MsBuildNs + "RcCompile")
            .Concat(root.Descendants("RcCompile"));

        foreach (var rc in rcItems)
        {
            var include = rc.Attribute("Include")?.Value;
            if (!string.IsNullOrEmpty(include))
            {
                resources.Add(include);
            }
        }

        // Also find ResourceCompile items
        var resItems = root.Descendants(MsBuildNs + "ResourceCompile")
            .Concat(root.Descendants("ResourceCompile"));

        foreach (var res in resItems)
        {
            var include = res.Attribute("Include")?.Value;
            if (!string.IsNullOrEmpty(include))
            {
                resources.Add(include);
            }
        }

        return [.. resources.Distinct()];
    }

    private static List<PackageReference> ExtractPackageReferences(XElement root)
    {
        var packages = new List<PackageReference>();

        // Find package references
        var pkgRefs = root.Descendants(MsBuildNs + "PackageImport")
            .Concat(root.Descendants("PackageImport"));

        foreach (var pkg in pkgRefs)
        {
            var include = pkg.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(include))
            {
                continue;
            }

            packages.Add(new PackageReference
            {
                Name = include,
                IsRuntime = true
            });
        }

        // Find design packages
        var designPkgs = root.Descendants(MsBuildNs + "DesignOnlyPackage")
            .Concat(root.Descendants("DesignOnlyPackage"));

        foreach (var pkg in designPkgs)
        {
            var include = pkg.Attribute("Include")?.Value;
            if (string.IsNullOrEmpty(include))
            {
                continue;
            }

            packages.Add(new PackageReference
            {
                Name = include,
                IsRuntime = false,
                IsDesignTime = true
            });
        }

        // Check DCC_UsePackage property for runtime packages
        var usePackages = GetPropertyValue(root, "DCC_UsePackage");
        if (!string.IsNullOrEmpty(usePackages))
        {
            foreach (var pkg in usePackages.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var pkgName = pkg.Trim();
                if (!string.IsNullOrEmpty(pkgName) &&
                    !packages.Any(p => p.Name.Equals(pkgName, StringComparison.OrdinalIgnoreCase)))
                {
                    packages.Add(new PackageReference
                    {
                        Name = pkgName,
                        IsRuntime = true
                    });
                }
            }
        }

        return packages;
    }

    private static VersionInfo ExtractVersionInfo(XElement root)
    {
        return new VersionInfo
        {
            MajorVersion = GetPropertyValue(root, "VerInfo_MajorVer") ?? "1",
            MinorVersion = GetPropertyValue(root, "VerInfo_MinorVer") ?? "0",
            Release = GetPropertyValue(root, "VerInfo_Release") ?? "0",
            Build = GetPropertyValue(root, "VerInfo_Build") ?? "0",
            FileDescription = GetPropertyValue(root, "VerInfo_Keys")?.Split(';')
                .FirstOrDefault(k => k.StartsWith("FileDescription="))
                ?.Replace("FileDescription=", "") ?? string.Empty,
            CompanyName = GetPropertyValue(root, "VerInfo_Keys")?.Split(';')
                .FirstOrDefault(k => k.StartsWith("CompanyName="))
                ?.Replace("CompanyName=", "") ?? string.Empty,
            ProductName = GetPropertyValue(root, "VerInfo_Keys")?.Split(';')
                .FirstOrDefault(k => k.StartsWith("ProductName="))
                ?.Replace("ProductName=", "") ?? string.Empty
        };
    }

    private static XElement? FindConfigurationPropertyGroup(XElement root, string configuration)
    {
        var propertyGroups = root.Descendants(MsBuildNs + "PropertyGroup")
            .Concat(root.Descendants("PropertyGroup"));

        foreach (var pg in propertyGroups)
        {
            var condition = pg.Attribute("Condition")?.Value ?? string.Empty;
            if (condition.Contains(configuration, StringComparison.OrdinalIgnoreCase))
            {
                return pg;
            }
        }

        return null;
    }

    private static string? GetElementValue(XElement parent, string elementName)
    {
        var element = parent.Element(MsBuildNs + elementName) ?? parent.Element(elementName);
        return element?.Value;
    }

    private static List<string> ExtractDefinesFromPropertyGroup(XElement pg)
    {
        var defines = new List<string>();
        var defineValue = GetElementValue(pg, "DCC_Define");

        if (!string.IsNullOrEmpty(defineValue))
        {
            defines.AddRange(defineValue.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim()));
        }

        return defines;
    }

    private static List<string> ExtractSearchPathsFromPropertyGroup(XElement pg)
    {
        var paths = new List<string>();
        var pathValue = GetElementValue(pg, "DCC_UnitSearchPath");

        if (!string.IsNullOrEmpty(pathValue))
        {
            paths.AddRange(pathValue.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !p.StartsWith("$(")));
        }

        return paths;
    }

    private static SourceFileType DetermineFileType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".pas" => SourceFileType.Unit,
            ".dpr" => SourceFileType.Program,
            ".dpk" => SourceFileType.Package,
            ".inc" => SourceFileType.Include,
            ".dfm" => SourceFileType.Form,
            ".fmx" => SourceFileType.Form,
            ".rc" => SourceFileType.Resource,
            ".res" => SourceFileType.CompiledResource,
            _ => SourceFileType.Unknown
        };
    }

    private static void ResolveAllPaths(DprojMetadata metadata)
    {
        var baseDir = metadata.ProjectDirectory;

        // Resolve search paths
        metadata.ResolvedSearchPaths =
        [
            .. metadata.SearchPaths
                .Select(p => ResolvePath(p, baseDir))
                .Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p))
                .Select(p => p!) // Null-forgiving after Where filter
                .Distinct()
        ];

        // Resolve source file paths
        foreach (var file in metadata.SourceFiles)
        {
            file.ResolvedPath = ResolvePath(file.FileName, baseDir);
            file.Exists = !string.IsNullOrEmpty(file.ResolvedPath) &&
                         File.Exists(file.ResolvedPath);
        }

        // Resolve form file paths
        foreach (var form in metadata.FormFiles)
        {
            form.ResolvedUnitPath = ResolvePath(form.UnitPath, baseDir);
            form.ResolvedDfmPath = ResolvePath(form.DfmPath, baseDir);
            form.UnitExists = File.Exists(form.ResolvedUnitPath ?? string.Empty);
            form.DfmExists = File.Exists(form.ResolvedDfmPath ?? string.Empty);
        }

        // Resolve output paths
        if (!string.IsNullOrEmpty(metadata.OutputDirectory))
        {
            metadata.ResolvedOutputDirectory = ResolvePath(metadata.OutputDirectory, baseDir) ?? string.Empty;
        }
    }

    private static string? ResolvePath(string path, string baseDir)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        // Skip MSBuild variables
        if (path.Contains("$("))
        {
            // Try to resolve common variables
            path = path.Replace("$(BDS)", GetBdsPath() ?? string.Empty);
            path = path.Replace("$(BDSLIB)", Path.Combine(GetBdsPath() ?? string.Empty, "lib"));
            path = path.Replace("$(Platform)", "Win32");
            path = path.Replace("$(Config)", "Debug");

            // If still has unresolved variables, return as-is
            if (path.Contains("$("))
            {
                return path;
            }
        }

        // Handle relative paths
        if (!Path.IsPathRooted(path))
        {
            path = Path.Combine(baseDir, path);
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    private static string? GetBdsPath()
    {
        // Common Delphi installation paths
        var possiblePaths = new[]
        {
            @"C:\Program Files (x86)\Embarcadero\Studio\23.0", // Delphi 12
            @"C:\Program Files (x86)\Embarcadero\Studio\22.0", // Delphi 11
            @"C:\Program Files (x86)\Embarcadero\Studio\21.0", // Delphi 10.4
            @"C:\Program Files (x86)\Embarcadero\Studio\20.0", // Delphi 10.3
            @"C:\Program Files (x86)\Embarcadero\Studio\19.0", // Delphi 10.2
        };

        return possiblePaths.FirstOrDefault(Directory.Exists);
    }

    #endregion

    #region Generated Regex

    [GeneratedRegex(@"'?\$\((?:Cfg_\d+|Config)\)'?\s*[!=]=\s*'?(\w+)'?")]
    private static partial Regex ConfigConditionRegex();

    #endregion
}

#region Model Classes

/// <summary>
/// Metadata extracted from a .dproj file.
/// </summary>
public class DprojMetadata
{
    public string DprojPath { get; set; } = string.Empty;
    public string ProjectDirectory { get; set; } = string.Empty;
    public string ProjectGuid { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string MainSource { get; set; } = string.Empty;
    public string FrameworkType { get; set; } = string.Empty; // VCL, FMX, Console
    public string DelphiVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = "Win32";
    public string ActiveConfiguration { get; set; } = "Debug";
    public string ActivePlatform { get; set; } = "Win32";

    public List<BuildConfiguration> BuildConfigurations { get; set; } = [];
    public List<string> CompilerDefines { get; set; } = [];
    public List<string> SearchPaths { get; set; } = [];
    public List<string> ResolvedSearchPaths { get; set; } = [];
    public List<string> UnitScopeNames { get; set; } = [];

    public List<ProjectSourceFile> SourceFiles { get; set; } = [];
    public List<ProjectFormFile> FormFiles { get; set; } = [];
    public List<string> ResourceFiles { get; set; } = [];
    public List<PackageReference> PackageReferences { get; set; } = [];

    public string OutputDirectory { get; set; } = string.Empty;
    public string ResolvedOutputDirectory { get; set; } = string.Empty;
    public string UnitOutputDirectory { get; set; } = string.Empty;

    public VersionInfo VersionInfo { get; set; } = new();

    /// <summary>
    /// Gets all active compiler defines for conditional compilation analysis.
    /// </summary>
    public HashSet<string> GetActiveDefines()
    {
        var defines = new HashSet<string>(CompilerDefines, StringComparer.OrdinalIgnoreCase);

        // Add platform-specific defines
        if (Platform.Equals("Win32", StringComparison.OrdinalIgnoreCase))
        {
            defines.Add("WIN32");
            defines.Add("MSWINDOWS");
        }
        else if (Platform.Equals("Win64", StringComparison.OrdinalIgnoreCase))
        {
            defines.Add("WIN64");
            defines.Add("MSWINDOWS");
        }

        // Add framework defines
        if (FrameworkType.Equals("VCL", StringComparison.OrdinalIgnoreCase))
        {
            defines.Add("VCL");
        }
        else if (FrameworkType.Equals("FMX", StringComparison.OrdinalIgnoreCase))
        {
            defines.Add("FMX");
        }

        return defines;
    }
}

/// <summary>
/// Build configuration (Debug, Release, etc.)
/// </summary>
public class BuildConfiguration
{
    public string Name { get; set; } = string.Empty;
    public List<string> Defines { get; set; } = [];
    public List<string> SearchPaths { get; set; } = [];
    public string OutputPath { get; set; } = string.Empty;
    public bool IsDebug { get; set; }
}

/// <summary>
/// Source file reference from project.
/// </summary>
public class ProjectSourceFile
{
    public string FileName { get; set; } = string.Empty;
    public string? ResolvedPath { get; set; }
    public SourceFileType FileType { get; set; }
    public string? Form { get; set; }
    public string? DesignClass { get; set; }
    public bool IsMainSource { get; set; }
    public bool Exists { get; set; }
}

/// <summary>
/// Form file reference.
/// </summary>
public class ProjectFormFile
{
    public string UnitPath { get; set; } = string.Empty;
    public string DfmPath { get; set; } = string.Empty;
    public string? ResolvedUnitPath { get; set; }
    public string? ResolvedDfmPath { get; set; }
    public string FormName { get; set; } = string.Empty;
    public string FormType { get; set; } = "TForm";
    public bool UnitExists { get; set; }
    public bool DfmExists { get; set; }
}

/// <summary>
/// Package reference (BPL).
/// </summary>
public class PackageReference
{
    public string Name { get; set; } = string.Empty;
    public bool IsRuntime { get; set; }
    public bool IsDesignTime { get; set; }
}

/// <summary>
/// Version information.
/// </summary>
public class VersionInfo
{
    public string MajorVersion { get; set; } = "1";
    public string MinorVersion { get; set; } = "0";
    public string Release { get; set; } = "0";
    public string Build { get; set; } = "0";
    public string FileDescription { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    public string GetVersionString() => $"{MajorVersion}.{MinorVersion}.{Release}.{Build}";
}

/// <summary>
/// Source file type enumeration.
/// </summary>
public enum SourceFileType
{
    Unknown,
    Unit,
    Program,
    Package,
    Include,
    Form,
    Resource,
    CompiledResource
}

/// <summary>
/// Result of cross-referencing .dproj with .dpr.
/// </summary>
public class CrossReferenceResult
{
    /// <summary>
    /// Files that are both in .dproj and .dpr (actively compiled).
    /// </summary>
    public List<ProjectSourceFile> ActiveFiles { get; } = [];

    /// <summary>
    /// Files in .dproj but not in .dpr (orphaned/unused).
    /// </summary>
    public List<ProjectSourceFile> OrphanedFiles { get; } = [];

    /// <summary>
    /// Units in .dpr but not explicitly listed in .dproj (found via search paths).
    /// </summary>
    public List<string> ExternalUnits { get; } = [];
}

#endregion