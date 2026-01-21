using System.Text.RegularExpressions;
using DelphiAnalysisMcpServer.Models;

namespace DelphiAnalysisMcpServer.Services;

/// <summary>
/// Service for scanning and parsing Delphi projects.
/// </summary>
public partial class DelphiScannerService
{
    private static readonly string[] DelphiExtensions = [".pas", ".dpr", ".dpk", ".dfm", ".fmx"];

    [GeneratedRegex(@"^\s*unit\s+([\w.]+)\s*;", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex UnitNameRegex();

    [GeneratedRegex(@"^\s*uses\s+([\s\S]*?);", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex UsesClauseRegex();

    [GeneratedRegex(@"^\s*type\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex TypeSectionRegex();

    [GeneratedRegex(@"(\w+)\s*=\s*class\s*\(?\s*(\w+)?\s*\)?", RegexOptions.IgnoreCase)]
    private static partial Regex ClassDeclarationRegex();

    [GeneratedRegex(@"(\w+)\s*=\s*record\b", RegexOptions.IgnoreCase)]
    private static partial Regex RecordDeclarationRegex();

    [GeneratedRegex(@"^\s*(procedure|function)\s+(\w+)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex ProcedureFunctionRegex();

    [GeneratedRegex(@"\{\$R\s+['""]?(\*\.dfm)['""]?\s*\}", RegexOptions.IgnoreCase)]
    private static partial Regex DfmResourceRegex();

    [GeneratedRegex(@"TDataModule|TForm|TFrame", RegexOptions.IgnoreCase)]
    private static partial Regex FormBaseClassRegex();

    // Additional regex patterns for parsing
    [GeneratedRegex(@"([\w.]+)\s+in\s+['""]([^'""]+)['""]", RegexOptions.IgnoreCase)]
    private static partial Regex UnitInPathRegex();

    [GeneratedRegex(@"^([\w.]+)")]
    private static partial Regex UnitNameOnlyRegex();

    [GeneratedRegex(@"\{\$(?:IFDEF|IFNDEF|ELSE|ENDIF|DEFINE|UNDEF|IF\s|IFEND|ELSEIF)[^}]*\}", RegexOptions.IgnoreCase)]
    private static partial Regex ConditionalDirectiveRegex();

    [GeneratedRegex(@"object\s+(\w+)\s*:\s*(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex DfmObjectRegex();

    [GeneratedRegex(@"^\s+object\s+(\w+)\s*:\s*(\w+)", RegexOptions.Multiline)]
    private static partial Regex DfmComponentRegex();

    /// <summary>
    /// Scans a Delphi project from a .dpr, .dproj file, or folder path.
    /// When a .dproj file is provided, it extracts metadata and locates the .dpr for cross-referencing.
    /// </summary>
    public async Task<DelphiProject> ScanProjectAsync(string path, DprojMetadata? dprojMetadata = null)
    {
        var project = new DelphiProject();
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (File.Exists(path))
        {
            if (ext == ".dproj")
            {
                // .dproj provided - find and parse corresponding .dpr
                project.DprojFilePath = path;
                project.RootPath = Path.GetDirectoryName(path) ?? path;
                project.Name = Path.GetFileNameWithoutExtension(path);

                // Store metadata if provided (from DprojParserService)
                project.DprojMetadata = dprojMetadata;

                // Find corresponding .dpr file
                var dprPath = Path.ChangeExtension(path, ".dpr");
                if (File.Exists(dprPath))
                {
                    project.DprFilePath = dprPath;
                }
                else if (dprojMetadata?.MainSource is not null)
                {
                    // Use MainSource from .dproj
                    var mainSourcePath = Path.Combine(project.RootPath, dprojMetadata.MainSource);
                    if (File.Exists(mainSourcePath))
                    {
                        project.DprFilePath = mainSourcePath;
                    }
                }

                // Parse the .dpr if found
                if (project.DprFilePath is not null)
                {
                    await ParseDprFileAsync(project);
                }

                // Enrich with .dproj metadata
                if (dprojMetadata is not null)
                {
                    EnrichProjectWithDprojMetadata(project, dprojMetadata);
                }
            }
            else if (ext == ".dpr")
            {
                project.DprFilePath = path;
                project.RootPath = Path.GetDirectoryName(path) ?? path;
                project.Name = Path.GetFileNameWithoutExtension(path);

                // Check for corresponding .dproj
                var dprojPath = Path.ChangeExtension(path, ".dproj");
                if (File.Exists(dprojPath))
                {
                    project.DprojFilePath = dprojPath;
                }

                await ParseDprFileAsync(project);
            }
        }
        else if (Directory.Exists(path))
        {
            project.RootPath = path;
            project.Name = new DirectoryInfo(path).Name;

            // Look for .dproj first, then .dpr
            var dprojFiles = Directory.GetFiles(path, "*.dproj", SearchOption.TopDirectoryOnly);
            var dprFiles = Directory.GetFiles(path, "*.dpr", SearchOption.TopDirectoryOnly);

            if (dprojFiles.Length > 0)
            {
                project.DprojFilePath = dprojFiles[0];
                project.Name = Path.GetFileNameWithoutExtension(dprojFiles[0]);
            }

            if (dprFiles.Length > 0)
            {
                project.DprFilePath = dprFiles[0];
                if (string.IsNullOrEmpty(project.Name))
                {
                    project.Name = Path.GetFileNameWithoutExtension(dprFiles[0]);
                }
                await ParseDprFileAsync(project);
            }
            else
            {
                await ScanFolderAsync(project);
            }
        }
        else
        {
            throw new ArgumentException($"Path not found: {path}");
        }

        return project;
    }

    /// <summary>
    /// Enriches project data with metadata from .dproj file.
    /// </summary>
    private static void EnrichProjectWithDprojMetadata(DelphiProject project, DprojMetadata metadata)
    {
        // Add search paths for unit resolution
        project.SearchPaths = metadata.ResolvedSearchPaths;

        // Store compiler defines for conditional compilation
        project.CompilerDefines = metadata.CompilerDefines;

        // Framework type helps determine UI conversion target
        project.FrameworkType = metadata.FrameworkType;

        // Add any units from .dproj that weren't in .dpr (might be external)
        foreach (var sourceFile in metadata.SourceFiles.Where(f => f.Exists))
        {
            var unitName = Path.GetFileNameWithoutExtension(sourceFile.FileName);

            // Check if already in project
            if (project.Units.Any(u => u.UnitName.Equals(unitName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Only add if file exists and is a .pas file
            if (sourceFile.ResolvedPath is not null &&
                sourceFile.FileType == SourceFileType.Unit)
            {
                project.Units.Add(new DelphiUnit
                {
                    UnitName = unitName,
                    FilePath = sourceFile.ResolvedPath,
                    IsFromDproj = true,
                    HasForm = sourceFile.Form is not null
                });
            }
        }

        // Add form information
        foreach (var form in metadata.FormFiles.Where(f => f.UnitExists))
        {
            var formUnitBaseName = Path.GetFileNameWithoutExtension(form.UnitPath);
            var unit = project.Units.FirstOrDefault(u =>
                // Exact match
                u.UnitName.Equals(formUnitBaseName, StringComparison.OrdinalIgnoreCase) ||
                // Dotted unit name - last segment matches
                (u.UnitName.Contains('.') && 
                 u.UnitName.Split('.').Last().Equals(formUnitBaseName, StringComparison.OrdinalIgnoreCase)) ||
                // File path match
                Path.GetFileNameWithoutExtension(u.FilePath).Equals(formUnitBaseName, StringComparison.OrdinalIgnoreCase));

            if (unit is not null)
            {
                unit.HasForm = true;
                unit.FormName = form.FormName;
                unit.FormType = form.FormType;
                unit.DfmFilePath = form.ResolvedDfmPath;
            }
        }
    }

    /// <summary>
    /// Locates a unit file using the project's search paths.
    /// </summary>
    public string? LocateUnitFile(string unitName, DelphiProject project)
    {
        var extensions = new[] { ".pas", ".pp", ".inc" };

        // First check project directory
        foreach (var ext in extensions)
        {
            var path = Path.Combine(project.RootPath, unitName + ext);
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Then check search paths from .dproj
        foreach (var searchPath in project.SearchPaths)
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

    private static async Task ParseDprFileAsync(DelphiProject project)
    {
        if (project.DprFilePath is null) return;

        var content = await File.ReadAllTextAsync(project.DprFilePath);

        // Strip out conditional compilation directives before parsing
        content = StripConditionalDirectives(content);

        // Extract units from uses clause
        var usesMatch = UsesClauseRegex().Match(content);
        if (usesMatch.Success)
        {
            var usesContent = usesMatch.Groups[1].Value;
            var unitReferences = usesContent
                .Split(',')
                .Select(u => u.Trim())
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Select(u =>
                {
                    // Handle "UnitName in 'path\file.pas'" syntax (unit name can be dotted)
                    var inMatch = UnitInPathRegex().Match(u);
                    if (inMatch.Success)
                    {
                        return (Name: inMatch.Groups[1].Value, Path: inMatch.Groups[2].Value);
                    }
                    // Just unit name (may be dotted like Vcl.Forms)
                    var nameOnly = UnitNameOnlyRegex().Match(u);
                    return nameOnly.Success ? (Name: nameOnly.Groups[1].Value, Path: (string?)null) : (Name: u, Path: null);
                })
                .Where(u => !string.IsNullOrWhiteSpace(u.Name))
                .Where(u => !IsSystemUnit(u.Name)) // Skip VCL/RTL units
                .ToList();

            foreach (var (unitName, relativePath) in unitReferences)
            {
                var filePath = await FindUnitFileAsync(project.RootPath, unitName, relativePath);
                if (filePath is not null)
                {
                    var unit = await ParseUnitFileAsync(filePath, project.RootPath);
                    project.Units.Add(unit);
                }
                else
                {
                    project.Warnings.Add($"Unit not found: {unitName}" + (relativePath != null ? $" (path: {relativePath})" : ""));
                }
            }
        }

        // Also scan for any .dfm files
        await ScanForFormsAsync(project);
    }

    /// <summary>
    /// Strips conditional compilation directives from Delphi source.
    /// This removes {$IFDEF}, {$IFNDEF}, {$ELSE}, {$ENDIF}, {$DEFINE}, etc.
    /// but preserves {$R *.dfm} and similar resource directives.
    /// </summary>
    private static string StripConditionalDirectives(string content)
    {
        // Remove conditional compilation blocks entirely
        // This is a simplified approach - removes directive lines but not their contents
        return ConditionalDirectiveRegex().Replace(content, "");
    }

    /// <summary>
    /// Checks if a unit name is a system/RTL/VCL unit that won't be in the project.
    /// </summary>
    private static bool IsSystemUnit(string unitName)
    {
        var systemPrefixes = new[]
        { 
            // Dotted namespace prefixes
            "System.", "Vcl.", "Fmx.", "Data.", "Winapi.", "Posix.",
            "System", "Xml.", "Soap.", "Web.", "REST.", "FireDAC.",
            "IBX.", "IdHTTP", "IdTCP", "Indy.",
            
            // Classic unit names (exact matches or startswith)
            "SysUtils", "Classes", "Windows", "Messages",
            "Graphics", "Controls", "Forms", "Dialogs", "StdCtrls",
            "ExtCtrls", "ComCtrls", "Menus", "Buttons", "Grids",
            "DBGrids", "DB", "ADODB", "Variants", "StrUtils",
            "DateUtils", "Math", "Types", "TypInfo", "RTTI",
            "Generics.", "IOUtils", "RegularExpressions",
            "NetEncoding", "JSON", "Threading"
        };

        return systemPrefixes.Any(prefix =>
            unitName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            unitName.Equals(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task ScanFolderAsync(DelphiProject project)
    {
        var files = Directory.GetFiles(project.RootPath, "*.*", SearchOption.AllDirectories)
            .Where(f => DelphiExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToList();

        // Look for a .dpr file first
        var dprFile = files.FirstOrDefault(f => f.EndsWith(".dpr", StringComparison.OrdinalIgnoreCase));
        if (dprFile is not null)
        {
            project.DprFilePath = dprFile;
            project.Name = Path.GetFileNameWithoutExtension(dprFile);
        }

        // Parse all .pas files
        var pasFiles = files.Where(f => f.EndsWith(".pas", StringComparison.OrdinalIgnoreCase));
        foreach (var pasFile in pasFiles)
        {
            var unit = await ParseUnitFileAsync(pasFile, project.RootPath);
            project.Units.Add(unit);
        }

        // Parse all .dfm files
        await ScanForFormsAsync(project);
    }

    private static async Task ScanForFormsAsync(DelphiProject project)
    {
        var dfmFiles = Directory.GetFiles(project.RootPath, "*.dfm", SearchOption.AllDirectories);
        foreach (var dfmFile in dfmFiles)
        {
            var form = await ParseDfmFileAsync(dfmFile, project.RootPath);
            project.Forms.Add(form);

            // Link to corresponding unit - match by:
            // 1. Exact unit name match
            // 2. Dotted unit name where last segment matches (e.g., MyApp.TreatmentDMF -> TreatmentDMF.dfm)
            // 3. File path match (same directory, same base name)
            var dfmBaseName = Path.GetFileNameWithoutExtension(dfmFile);
            var dfmDirectory = Path.GetDirectoryName(dfmFile);
            
            var correspondingUnit = project.Units.FirstOrDefault(u =>
                // Exact unit name match
                u.UnitName.Equals(dfmBaseName, StringComparison.OrdinalIgnoreCase) ||
                // Dotted unit name - last segment matches
                (u.UnitName.Contains('.') && 
                 u.UnitName.Split('.').Last().Equals(dfmBaseName, StringComparison.OrdinalIgnoreCase)) ||
                // File path match - .pas file in same directory with same base name
                (Path.GetDirectoryName(u.FilePath)?.Equals(dfmDirectory, StringComparison.OrdinalIgnoreCase) == true &&
                 Path.GetFileNameWithoutExtension(u.FilePath).Equals(dfmBaseName, StringComparison.OrdinalIgnoreCase)));
            
            if (correspondingUnit is not null)
            {
                correspondingUnit.AssociatedFormFile = dfmFile;
                correspondingUnit.DfmFilePath = dfmFile;  // Also set DfmFilePath for consistency
                correspondingUnit.IsForm = true;
            }
        }
    }

    private static async Task<DelphiUnit> ParseUnitFileAsync(string filePath, string rootPath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        var fileInfo = new FileInfo(filePath);

        var unit = new DelphiUnit
        {
            FilePath = filePath,
            RelativePath = Path.GetRelativePath(rootPath, filePath),
            FileSizeBytes = fileInfo.Length,
            LineCount = content.Split('\n').Length
        };

        // Extract unit name (can be dotted like MyApp.Utils)
        var unitMatch = UnitNameRegex().Match(content);
        unit.UnitName = unitMatch.Success ? unitMatch.Groups[1].Value : Path.GetFileNameWithoutExtension(filePath);

        // Check if it's a form, data module, or frame
        if (DfmResourceRegex().IsMatch(content))
        {
            unit.IsForm = true;
            unit.HasForm = true;
            
            // Detect the base form type from class declaration
            // Look for patterns like: TMyForm = class(TForm) or TMyDataModule = class(TDataModule)
            if (content.Contains("TDataModule", StringComparison.OrdinalIgnoreCase))
            {
                unit.IsDataModule = true;
                unit.FormType = "TDataModule";
            }
            else if (content.Contains("(TFrame)", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("( TFrame)", StringComparison.OrdinalIgnoreCase) ||
                     content.Contains("(TFrame )", StringComparison.OrdinalIgnoreCase))
            {
                unit.FormType = "TFrame";
            }
            else
            {
                unit.FormType = "TForm";
            }
            
            // Try to extract the form class name (e.g., TfrmPatient from "TfrmPatient = class(TForm)")
            var classMatch = System.Text.RegularExpressions.Regex.Match(
                content, 
                @"(T\w+)\s*=\s*class\s*\(\s*(TForm|TDataModule|TFrame)\s*\)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (classMatch.Success)
            {
                unit.FormName = classMatch.Groups[1].Value;
            }
        }

        // Extract uses clauses (simplified - interface section only for now)
        var interfaceEnd = content.IndexOf("implementation", StringComparison.OrdinalIgnoreCase);
        if (interfaceEnd > 0)
        {
            var interfaceSection = content[..interfaceEnd];
            var usesMatch = UsesClauseRegex().Match(interfaceSection);
            if (usesMatch.Success)
            {
                unit.UsesInterface = ParseUsesClause(usesMatch.Groups[1].Value);
            }
        }

        // Extract implementation uses
        if (interfaceEnd > 0)
        {
            var implementationSection = content[interfaceEnd..];
            var usesMatch = UsesClauseRegex().Match(implementationSection);
            if (usesMatch.Success)
            {
                unit.UsesImplementation = ParseUsesClause(usesMatch.Groups[1].Value);
            }
        }

        // Extract class declarations (basic parsing)
        var classMatches = ClassDeclarationRegex().Matches(content);
        foreach (Match match in classMatches)
        {
            unit.Classes.Add(new DelphiClass
            {
                ClassName = match.Groups[1].Value,
                ParentClass = match.Groups[2].Success ? match.Groups[2].Value : null
            });
        }

        // Extract record declarations
        var recordMatches = RecordDeclarationRegex().Matches(content);
        foreach (Match match in recordMatches)
        {
            unit.Records.Add(new DelphiRecord
            {
                RecordName = match.Groups[1].Value
            });
        }

        return unit;
    }

    private static async Task<DelphiForm> ParseDfmFileAsync(string filePath, string rootPath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        var fileInfo = new FileInfo(filePath);

        var form = new DelphiForm
        {
            FilePath = filePath,
            RelativePath = Path.GetRelativePath(rootPath, filePath),
            FileSizeBytes = fileInfo.Length
        };

        // Extract form name and class from first line: "object FormName: TFormClass"
        var firstLine = content.Split('\n').FirstOrDefault() ?? "";
        var objectMatch = DfmObjectRegex().Match(firstLine);
        if (objectMatch.Success)
        {
            form.FormName = objectMatch.Groups[1].Value;
            form.ParentClass = objectMatch.Groups[2].Value;
        }
        else
        {
            form.FormName = Path.GetFileNameWithoutExtension(filePath);
        }

        // Basic component extraction (top-level only for summary)
        var componentMatches = DfmComponentRegex().Matches(content);
        foreach (Match match in componentMatches)
        {
            form.Components.Add(new DelphiComponent
            {
                Name = match.Groups[1].Value,
                ClassName = match.Groups[2].Value
            });
        }

        return form;
    }

    private static List<string> ParseUsesClause(string usesContent)
    {
        return
        [
            .. usesContent
                .Split(',')
                .Select(u => UnitNameOnlyRegex().Match(u.Trim()).Groups[1].Value)
                .Where(u => !string.IsNullOrWhiteSpace(u))
        ];
    }

    private static async Task<string?> FindUnitFileAsync(string rootPath, string unitName, string? relativePath)
    {
        // If we have a relative path from the .dpr, use it
        if (relativePath is not null)
        {
            // Convert Windows backslashes to Unix forward slashes
            var normalizedPath = relativePath.Replace('\\', '/');

            // Skip absolute Windows paths (e.g., C:\Common Files\...)
            if (normalizedPath.Length > 1 && normalizedPath[1] == ':')
            {
                // This is an absolute Windows path - can't resolve on Linux
                // Fall through to search by name
            }
            else
            {
                var fullPath = Path.Combine(rootPath, normalizedPath);
                // Normalize the combined path (resolves .. etc.)
                fullPath = Path.GetFullPath(fullPath);
                if (File.Exists(fullPath))
                    return fullPath;
            }
        }

        // For dotted unit names like MyApp.Utils, the file is usually just Utils.pas
        var simpleUnitName = unitName.Contains('.')
            ? unitName.Split('.').Last()
            : unitName;

        // Search in root and subdirectories
        var candidates = Directory.GetFiles(rootPath, $"{simpleUnitName}.pas", SearchOption.AllDirectories);
        if (candidates.Length > 0)
            return candidates[0];

        // Try the full dotted name as filename (rare but possible)
        if (unitName.Contains('.'))
        {
            candidates = Directory.GetFiles(rootPath, $"{unitName}.pas", SearchOption.AllDirectories);
            if (candidates.Length > 0)
                return candidates[0];
        }

        // Try without exact match (case insensitive)
        candidates =
        [
            .. Directory.GetFiles(rootPath, "*.pas", SearchOption.AllDirectories)
                .Where(f => Path.GetFileNameWithoutExtension(f).Equals(simpleUnitName, StringComparison.OrdinalIgnoreCase))
        ];

        return candidates.Length > 0 ? candidates[0] : null;
    }
}