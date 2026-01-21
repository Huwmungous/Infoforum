using DelphiAnalysisMcpServer.Models;
using SfD.Mcp.Protocol.Models;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DelphiAnalysisMcpServer.Services;

/// <summary>
/// Handles MCP tool calls and orchestrates the Delphi analysis workflow.
/// </summary>
public partial class McpToolHandler(
    DelphiScannerService scanner,
    DprojParserService dprojParser,
    OllamaService ollama,
    OutputGeneratorService outputGenerator,
    SessionService sessionService,
    ProjectPersistenceService persistenceService,
    CodeGenerationService codeGenerationService,
    ILogger<McpToolHandler> logger)
{
    private readonly DelphiScannerService _scanner = scanner;
    private readonly DprojParserService _dprojParser = dprojParser;
    private readonly OllamaService _ollama = ollama;
    private readonly OutputGeneratorService _outputGenerator = outputGenerator;
    private readonly SessionService _sessionService = sessionService;
    private readonly ProjectPersistenceService _persistenceService = persistenceService;
    private readonly CodeGenerationService _codeGenerationService = codeGenerationService;
    private readonly ILogger<McpToolHandler> _logger = logger;

    #region LoggerMessage Definitions

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling tool call: {ToolName}")]
    private partial void LogHandlingToolCall(string toolName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error handling tool {ToolName}")]
    private partial void LogToolError(Exception ex, string toolName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} Delphi projects in folder: {Folder}")]
    private partial void LogFoundProjects(int count, string folder);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to generate AI description for project {ProjectName}")]
    private partial void LogProjectDescriptionFailure(Exception ex, string projectName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Completed {ProjectName} ({Completed}/{Total})")]
    private partial void LogProjectCompleted(string projectName, int completed, int total);

    [LoggerMessage(Level = LogLevel.Information, Message = "All projects processed, building response with {Count} projects")]
    private partial void LogBuildingResponse(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Returning response")]
    private partial void LogReturningResponse();

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting persistence scan of folder: {FolderPath}")]
    private partial void LogPersistenceScanStarted(string folderPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to persist project {ProjectName} to database")]
    private partial void LogPersistFailed(Exception ex, string projectName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Response object created, serializing...")]
    private partial void LogResponseCreated();

    [LoggerMessage(Level = LogLevel.Information, Message = "Serialized response size: {Size} bytes")]
    private partial void LogSerializedResponseSize(int size);

    #endregion

    #region GeneratedRegex Patterns

    [GeneratedRegex(@"\buses\s+([\s\S]*?);", RegexOptions.IgnoreCase)]
    private static partial Regex DprUsesClauseRegex();

    [GeneratedRegex(@"([\w.]+)\s+in\s+", RegexOptions.IgnoreCase)]
    private static partial Regex UnitInPathRegex();

    [GeneratedRegex(@"^([\w.]+)")]
    private static partial Regex UnitNameRegex();

    #endregion

    /// <summary>
    /// Returns the list of available tools.
    /// </summary>
    public IEnumerable<ToolInfo> GetTools()
    {
        return
        [
            new ToolInfo
            {
                Name = "list_delphi_projects",
                Description = "Quickly lists all Delphi projects in a folder without performing analysis. Returns project paths and names for progress tracking. Use this before describe_single_project for batch processing with progress indicators.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "folder_path": {
                                "type": "string",
                                "description": "Path to folder containing Delphi project(s)"
                            },
                            "recursive": {
                                "type": "boolean",
                                "description": "Search subfolders for projects (default: true)"
                            }
                        },
                        "required": ["folder_path"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "describe_single_project",
                Description = "Scans, analyzes, and persists a single Delphi project to the database. Returns project details including unit counts, forms, and database usage. Use with list_delphi_projects for batch processing with progress.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "project_path": {
                                "type": "string",
                                "description": "Path to .dpr or .dproj file"
                            },
                            "folder_path": {
                                "type": "string",
                                "description": "Root folder path for relative path calculation"
                            },
                            "include_analysis": {
                                "type": "boolean",
                                "description": "Include AI analysis of project purpose (default: true)"
                            }
                        },
                        "required": ["project_path", "folder_path"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "describe_delphi_projects",
                Description = "Scans a folder to find and describe all Delphi projects (.dpr/.dproj files). Returns a comprehensive analysis of each project including unit counts, forms, database usage, complexity metrics, and recommendations for migration. Use this as the first step to understand a codebase before detailed analysis.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "folder_path": {
                                "type": "string",
                                "description": "Path to folder containing Delphi project(s)"
                            },
                            "recursive": {
                                "type": "boolean",
                                "description": "Search subfolders for projects (default: true)"
                            },
                            "include_analysis": {
                                "type": "boolean",
                                "description": "Include detailed analysis of each project (default: true)"
                            }
                        },
                        "required": ["folder_path"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "scan_delphi_project",
                Description = "Scans a Delphi project from a .dpr, .dproj file, or folder path. Returns a manifest of all units, forms, and dependencies. Prefers .dproj for richer metadata.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "path": {
                                "type": "string",
                                "description": "Path to .dpr, .dproj file, or folder containing Delphi source files"
                            }
                        },
                        "required": ["path"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "parse_dproj",
                Description = "Parses a .dproj file to extract project metadata including search paths, compiler defines, build configurations, source files, forms, and package references. Use this for detailed project analysis before scanning.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "path": {
                                "type": "string",
                                "description": "Path to .dproj file"
                            },
                            "cross_reference_dpr": {
                                "type": "boolean",
                                "description": "If true, cross-references with .dpr to identify active vs orphaned files (default: true)"
                            }
                        },
                        "required": ["path"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "analyze_unit",
                Description = "Analyzes a single Delphi unit using AI to extract detailed structural information.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "session_id": {
                                "type": "string",
                                "description": "Session ID from scan_delphi_project"
                            },
                            "unit_name": {
                                "type": "string",
                                "description": "Name of the unit to analyze"
                            }
                        },
                        "required": ["session_id", "unit_name"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "analyze_database_operations",
                Description = "Analyzes a unit to extract all database operations, SQL statements, and transaction boundaries.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "session_id": {
                                "type": "string",
                                "description": "Session ID"
                            },
                            "unit_name": {
                                "type": "string",
                                "description": "Name of the unit to analyze for database operations"
                            }
                        },
                        "required": ["session_id", "unit_name"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "generate_repository",
                Description = "Generates a C# repository class from analyzed database operations, inheriting from BaseRepository.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "session_id": {
                                "type": "string",
                                "description": "Session ID"
                            },
                            "repository_name": {
                                "type": "string",
                                "description": "Name for the repository class (e.g., CustomerRepository)"
                            },
                            "source_units": {
                                "type": "array",
                                "items": { "type": "string" },
                                "description": "List of unit names whose database operations to include"
                            }
                        },
                        "required": ["session_id", "repository_name"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "generate_controller",
                Description = "Generates an ASP.NET Core controller that uses the repository.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "session_id": {
                                "type": "string",
                                "description": "Session ID"
                            },
                            "controller_name": {
                                "type": "string",
                                "description": "Name for the controller class (e.g., CustomersController)"
                            },
                            "repository_name": {
                                "type": "string",
                                "description": "Name of the repository to use"
                            }
                        },
                        "required": ["session_id", "controller_name", "repository_name"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "generate_react_component",
                Description = "Generates a React TypeScript component from a Delphi form.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "session_id": {
                                "type": "string",
                                "description": "Session ID"
                            },
                            "form_name": {
                                "type": "string",
                                "description": "Name of the form to convert"
                            }
                        },
                        "required": ["session_id", "form_name"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "translate_unit",
                Description = "Translates a single Delphi unit to C#. Database operations are replaced with API calls.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "session_id": {
                                "type": "string",
                                "description": "Session ID from scan_delphi_project"
                            },
                            "unit_name": {
                                "type": "string",
                                "description": "Name of the unit to translate"
                            }
                        },
                        "required": ["session_id", "unit_name"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "translate_project",
                Description = "Translates all units in the project to C#. Database code goes to repositories, UI to React.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "session_id": {
                                "type": "string",
                                "description": "Session ID from scan_delphi_project"
                            },
                            "max_units": {
                                "type": "integer",
                                "description": "Maximum number of units to translate in this batch (default: all)"
                            },
                            "skip_forms": {
                                "type": "boolean",
                                "description": "Skip form/UI files (default: false)"
                            }
                        },
                        "required": ["session_id"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "configure_translation",
                Description = "Configures translation options for a session.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "session_id": {
                                "type": "string",
                                "description": "Session ID"
                            },
                            "base_namespace": {
                                "type": "string",
                                "description": "Root namespace for generated C# code"
                            },
                            "ui_target": {
                                "type": "string",
                                "enum": ["React", "Blazor", "WinForms", "WPF", "MAUI", "None"],
                                "description": "Target UI framework for form translations (default: React)"
                            },
                            "ollama_model": {
                                "type": "string",
                                "description": "Ollama model to use (e.g., qwen2.5-coder:32b)"
                            },
                            "ollama_base_url": {
                                "type": "string",
                                "description": "Ollama API base URL (default: http://localhost:11434)"
                            },
                            "api_route_prefix": {
                                "type": "string",
                                "description": "API route prefix (default: api)"
                            }
                        },
                        "required": ["session_id"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "generate_output",
                Description = "Generates the translated project output including API, repositories, controllers, and React app.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "session_id": {
                                "type": "string",
                                "description": "Session ID"
                            },
                            "output_path": {
                                "type": "string",
                                "description": "Output directory path"
                            },
                            "format": {
                                "type": "string",
                                "enum": ["Folder", "Zip", "Scripts"],
                                "description": "Output format (default: Folder)"
                            },
                            "generate_scripts": {
                                "type": "boolean",
                                "description": "Generate deployment scripts (default: false)"
                            },
                            "script_format": {
                                "type": "string",
                                "enum": ["PowerShell", "Bash", "Both"],
                                "description": "Script format when generate_scripts is true"
                            }
                        },
                        "required": ["session_id", "output_path"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "get_session_status",
                Description = "Gets the current status of an analysis session.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "session_id": {
                                "type": "string",
                                "description": "Session ID"
                            }
                        },
                        "required": ["session_id"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "list_sessions",
                Description = "Lists all active analysis sessions.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {}
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "persist_project_analysis",
                Description = "Scans a folder for Delphi projects and persists all analysis data to the PostgreSQL database (Sfd_DelphiAnalysis). This includes projects, units, classes, methods, queries, forms, and database operations. Use this to populate the database for the React explorer UI.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "folder_path": {
                                "type": "string",
                                "description": "Path to the folder containing Delphi projects to analyze and persist"
                            }
                        },
                        "required": ["folder_path"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "get_project_query_summary",
                Description = "Gets a summary of queries stored in the database for a specific project.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "project_idx": {
                                "type": "integer",
                                "description": "The database index of the project"
                            }
                        },
                        "required": ["project_idx"]
                    }
                    """).RootElement
            },
            new ToolInfo
            {
                Name = "generate_data_access_layer",
                Description = "Generates a complete C# data access layer from the analysed database operations in a project. This includes DTOs (Data Transfer Objects) based on fields actually accessed in the code, Repository interfaces and implementations using raw ADO.NET, and API Controllers that call the repositories. The generated code is saved to the project record in the database. SELECT * queries are automatically rewritten to select only the columns actually used.",
                InputSchema = JsonDocument.Parse("""
                    {
                        "type": "object",
                        "properties": {
                            "project_idx": {
                                "type": "integer",
                                "description": "The project index (idx) from the project table"
                            },
                            "base_namespace": {
                                "type": "string",
                                "description": "Base namespace for generated C# code (e.g., 'MyProject.Api')"
                            },
                            "output_directory": {
                                "type": "string",
                                "description": "Optional: Directory to also save generated files to disk. If not provided, code is only saved to database."
                            }
                        },
                        "required": ["project_idx", "base_namespace"]
                    }
                    """).RootElement
            }
        ];
    }

    /// <summary>
    /// Handles a tool call and returns the result.
    /// </summary>
    public async Task<object> HandleToolCallAsync(string toolName, JsonElement? arguments)
    {
        LogHandlingToolCall(toolName);

        try
        {
            return toolName switch
            {
                "list_delphi_projects" => await HandleListDelphiProjectsAsync(arguments),
                "describe_single_project" => await HandleDescribeSingleProjectAsync(arguments),
                "describe_delphi_projects" => await HandleDescribeDelphiProjectsAsync(arguments),
                "scan_delphi_project" => await HandleScanProjectAsync(arguments),
                "parse_dproj" => await HandleParseDprojAsync(arguments),
                "analyze_unit" => await HandleAnalyzeUnitAsync(arguments),
                "analyze_database_operations" => await HandleAnalyzeDatabaseOperationsAsync(arguments),
                "generate_repository" => await HandleGenerateRepositoryAsync(arguments),
                "generate_controller" => await HandleGenerateControllerAsync(arguments),
                "generate_react_component" => await HandleGenerateReactComponentAsync(arguments),
                "translate_unit" => await HandleTranslateUnitAsync(arguments),
                "translate_project" => await HandleTranslateProjectAsync(arguments),
                "configure_translation" => HandleConfigureTranslation(arguments),
                "generate_output" => await HandleGenerateOutputAsync(arguments),
                "get_session_status" => HandleGetSessionStatus(arguments),
                "list_sessions" => HandleListSessions(),
                "persist_project_analysis" => await HandlePersistProjectAnalysisAsync(arguments),
                "get_project_query_summary" => await HandleGetProjectQuerySummaryAsync(arguments),
                "generate_data_access_layer" => await HandleGenerateDataAccessLayerAsync(arguments),
                _ => new { error = $"Unknown tool: {toolName}" }
            };
        }
        catch (Exception ex)
        {
            LogToolError(ex, toolName);
            return new { error = ex.Message };
        }
    }

    #region Database Persistence Handlers

    /// <summary>
    /// Handles list_delphi_projects - quickly lists project files without analysis.
    /// </summary>
    private async Task<object> HandleListDelphiProjectsAsync(JsonElement? arguments)
    {
        var folderPath = arguments?.GetProperty("folder_path").GetString()
            ?? throw new ArgumentException("folder_path is required");

        var recursive = true;
        if (arguments?.TryGetProperty("recursive", out var recursiveEl) == true)
        {
            recursive = recursiveEl.GetBoolean();
        }

        if (!Directory.Exists(folderPath))
        {
            return new { success = false, error = $"Folder not found: {folderPath}" };
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var dprFiles = Directory.GetFiles(folderPath, "*.dpr", searchOption);
        var dprojFiles = Directory.GetFiles(folderPath, "*.dproj", searchOption);

        // Build map preferring .dproj
        var projectMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dprFile in dprFiles)
        {
            var key = Path.Combine(Path.GetDirectoryName(dprFile) ?? "", Path.GetFileNameWithoutExtension(dprFile));
            projectMap[key] = dprFile;
        }

        foreach (var dprojFile in dprojFiles)
        {
            var key = Path.Combine(Path.GetDirectoryName(dprojFile) ?? "", Path.GetFileNameWithoutExtension(dprojFile));
            projectMap[key] = dprojFile; // Prefer dproj
        }

        var projects = projectMap.Values.Select(p => new
        {
            path = p,
            name = Path.GetFileNameWithoutExtension(p),
            type = Path.GetExtension(p).ToLowerInvariant().TrimStart('.'),
            relativePath = Path.GetRelativePath(folderPath, p)
        }).OrderBy(p => p.name).ToList();

        return await Task.FromResult(new
        {
            success = true,
            folderPath,
            projectCount = projects.Count,
            projects
        });
    }

    /// <summary>
    /// Handles describe_single_project - scans, analyzes, and persists one project.
    /// </summary>
    private async Task<object> HandleDescribeSingleProjectAsync(JsonElement? arguments)
    {
        var projectPath = arguments?.GetProperty("project_path").GetString()
            ?? throw new ArgumentException("project_path is required");

        var folderPath = arguments?.GetProperty("folder_path").GetString()
            ?? throw new ArgumentException("folder_path is required");

        var includeAnalysis = true;
        if (arguments?.TryGetProperty("include_analysis", out var analysisEl) == true)
        {
            includeAnalysis = analysisEl.GetBoolean();
        }

        if (!File.Exists(projectPath))
        {
            return new { success = false, error = $"Project file not found: {projectPath}" };
        }

        // Parse dproj if available
        DprojMetadata? dprojMetadata = null;
        if (projectPath.EndsWith(".dproj", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                dprojMetadata = await _dprojParser.ParseAsync(projectPath);
            }
            catch
            {
                // Continue without dproj metadata
            }
        }

        // Scan the project
        DelphiProject project;
        try
        {
            project = await _scanner.ScanProjectAsync(projectPath, dprojMetadata);
        }
        catch (Exception ex)
        {
            return new { success = false, error = $"Failed to scan project: {ex.Message}" };
        }

        // Get AI analysis if requested
        ProjectAiDescription? aiDescription = null;
        if (includeAnalysis)
        {
            try
            {
                aiDescription = await GenerateProjectDescriptionAsync(project, dprojMetadata);
            }
            catch (Exception ex)
            {
                LogProjectDescriptionFailure(ex, project.Name);
            }
        }

        // Calculate complexity score
        var databaseUnits = project.Units.Count(u =>
            u.UsesInterface.Concat(u.UsesImplementation).Any(use =>
                use.Contains("Data", StringComparison.OrdinalIgnoreCase) ||
                use.Contains("DB", StringComparison.OrdinalIgnoreCase) ||
                use.Contains("SQL", StringComparison.OrdinalIgnoreCase)));
        var complexityScore = CalculateComplexityScore(project, databaseUnits, dprojMetadata);

        // Persist to database
        var persistResult = await _persistenceService.PersistProjectAsync(
            folderPath,
            project,
            aiDescription?.Purpose,
            aiDescription?.BusinessDomain,
            aiDescription?.KeyFeatures,
            aiDescription?.KeyEntities,
            aiDescription?.TechnicalSummary,
            complexityScore,
            dprojMetadata?.DelphiVersion);

        return new
        {
            success = persistResult.Success,
            projectName = project.Name,
            projectIdx = persistResult.ProjectIdx,
            framework = dprojMetadata?.FrameworkType ?? project.FrameworkType,
            units = project.Units.Count,
            forms = project.Forms.Count,
            unitsProcessed = persistResult.UnitsProcessed,
            queriesFound = persistResult.TotalQueriesFound,
            sourceFilesLoaded = persistResult.SourceFilesLoaded,
            purpose = aiDescription?.Purpose,
            warnings = persistResult.Warnings,
            error = persistResult.ErrorMessage
        };
    }

    /// <summary>
    /// Handles the persist_project_analysis tool - scans folder and persists to database.
    /// </summary>
    private async Task<object> HandlePersistProjectAnalysisAsync(JsonElement? arguments)
    {
        var folderPath = arguments?.GetProperty("folder_path").GetString()
            ?? throw new ArgumentException("folder_path is required");

        if (!Directory.Exists(folderPath))
        {
            return new { success = false, error = $"Folder not found: {folderPath}" };
        }

        LogPersistenceScanStarted(folderPath);

        // Find all project files
        var projectPaths = FindProjectFiles(folderPath);
        if (projectPaths.Count == 0)
        {
            return new { success = false, error = "No Delphi projects found in folder" };
        }

        LogFoundProjects(projectPaths.Count, folderPath);

        var results = new List<object>();
        var totalProjects = projectPaths.Count;
        var completedCount = 0;
        var successCount = 0;
        var failedCount = 0;
        var totalUnits = 0;
        var totalQueries = 0;

        foreach (var kvp in projectPaths)
        {
            var (dprPath, dprojPath) = kvp.Value;
            var projectName = Path.GetFileNameWithoutExtension(dprojPath ?? dprPath ?? "Unknown");

            try
            {
                // Use existing scanner to parse the project
                var projectPath = dprojPath ?? dprPath;
                if (string.IsNullOrEmpty(projectPath))
                {
                    results.Add(new { project = projectName, success = false, error = "No project file found" });
                    failedCount++;
                    completedCount++;
                    continue;
                }

                // Parse .dproj for metadata if available
                DprojMetadata? dprojMetadata = null;
                if (!string.IsNullOrEmpty(dprojPath) && File.Exists(dprojPath))
                {
                    try
                    {
                        dprojMetadata = await _dprojParser.ParseAsync(dprojPath);
                    }
                    catch
                    {
                        // Continue without dproj metadata
                    }
                }

                var project = await _scanner.ScanProjectAsync(projectPath, dprojMetadata);

                // Persist to database
                var persistResult = await _persistenceService.PersistProjectAsync(folderPath, project);

                completedCount++;
                if (persistResult.Success)
                {
                    successCount++;
                    totalUnits += persistResult.UnitsProcessed;
                    totalQueries += persistResult.TotalQueriesFound;

                    results.Add(new
                    {
                        project = projectName,
                        success = true,
                        project_idx = persistResult.ProjectIdx,
                        directory_idx = persistResult.DirectoryIdx,
                        units_processed = persistResult.UnitsProcessed,
                        queries_found = persistResult.TotalQueriesFound,
                        duration_ms = (int)persistResult.Duration.TotalMilliseconds,
                        warnings = persistResult.Warnings.Count > 0 ? persistResult.Warnings : null
                    });
                }
                else
                {
                    failedCount++;
                    results.Add(new
                    {
                        project = projectName,
                        success = false,
                        error = persistResult.ErrorMessage
                    });
                }

                LogProjectCompleted(projectName, completedCount, totalProjects);
            }
            catch (Exception ex)
            {
                failedCount++;
                completedCount++;
                results.Add(new { project = projectName, success = false, error = ex.Message });
                LogProjectCompleted(projectName, completedCount, totalProjects);
            }
        }

        return new
        {
            success = failedCount == 0,
            summary = new
            {
                folder = folderPath,
                total_projects = totalProjects,
                successful = successCount,
                failed = failedCount,
                total_units = totalUnits,
                total_queries = totalQueries
            },
            projects = results
        };
    }

    /// <summary>
    /// Handles the get_project_query_summary tool - retrieves query summary from database.
    /// </summary>
    private async Task<object> HandleGetProjectQuerySummaryAsync(JsonElement? arguments)
    {
        var projectIdx = arguments?.GetProperty("project_idx").GetInt32()
            ?? throw new ArgumentException("project_idx is required");

        var summary = await _persistenceService.GetProjectQuerySummaryAsync(projectIdx);

        return new
        {
            project_idx = projectIdx,
            query_count = summary.Count,
            queries = summary.Select(q => new
            {
                unit_name = q.UnitName,
                relative_path = q.RelativePath,
                component_type = q.QueryComponentType,
                operation_type = q.OperationType,
                table_name = q.TableName,
                method_name = q.MethodName,
                containing_class = q.ContainingClass,
                sql_preview = q.SqlText?.Length > 100 ? q.SqlText[..100] + "..." : q.SqlText
            })
        };
    }

    /// <summary>
    /// Handles the generate_data_access_layer tool - generates C# repositories, controllers and DTOs.
    /// </summary>
    private async Task<object> HandleGenerateDataAccessLayerAsync(JsonElement? arguments)
    {
        var projectIdx = arguments?.GetProperty("project_idx").GetInt32()
            ?? throw new ArgumentException("project_idx is required");

        var baseNamespace = arguments?.GetProperty("base_namespace").GetString()
            ?? throw new ArgumentException("base_namespace is required");

        string? outputDirectory = null;
        if (arguments?.TryGetProperty("output_directory", out var outputDirElement) == true)
        {
            outputDirectory = outputDirElement.GetString();
        }

        // Get project info
        var project = await _persistenceService.Repository.GetProjectByIdxAsync(projectIdx);
        if (project == null)
        {
            return new { error = $"Project with idx {projectIdx} not found" };
        }

        // Get all database operations for this project
        var operations = await _persistenceService.Repository.GetProjectDatabaseOperationsAsync(projectIdx);
        if (operations.Count == 0)
        {
            return new { error = $"No database operations found for project {projectIdx}. Run persist_project_analysis first." };
        }

        // Generate and save to database
        var result = await _codeGenerationService.GenerateAndSaveToProjectAsync(
            projectIdx,
            project.Name,
            baseNamespace,
            operations,
            _persistenceService.Repository.DataSource);

        // Optionally also save to disk
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            var spec = _codeGenerationService.GenerateApiSpecification(project.Name, baseNamespace, operations);
            var filesOnDisk = await _codeGenerationService.GenerateAndSaveCodeFilesAsync(
                spec,
                outputDirectory);

            result.GeneratedFiles = filesOnDisk;
            result.OutputDirectory = outputDirectory;
        }

        return new
        {
            success = true,
            project_idx = projectIdx,
            project_name = project.Name,
            base_namespace = baseNamespace,
            output_directory = result.OutputDirectory,
            dto_count = result.DtoCount,
            repository_count = result.RepositoryCount,
            controller_count = result.ControllerCount,
            total_methods = result.TotalMethodCount,
            files_generated = result.GeneratedFiles.Count,
            message = $"Generated {result.DtoCount} DTOs, {result.RepositoryCount} repositories, and {result.ControllerCount} controllers with {result.TotalMethodCount} methods. Code saved to project record."
        };
    }

    /// <summary>
    /// Finds all Delphi project files (.dpr and .dproj) in a folder.
    /// </summary>
    private static Dictionary<string, (string? DprPath, string? DprojPath)> FindProjectFiles(string folderPath)
    {
        var projectPaths = new Dictionary<string, (string? DprPath, string? DprojPath)>(StringComparer.OrdinalIgnoreCase);

        var dprFiles = Directory.GetFiles(folderPath, "*.dpr", SearchOption.AllDirectories);
        var dprojFiles = Directory.GetFiles(folderPath, "*.dproj", SearchOption.AllDirectories);

        foreach (var dprFile in dprFiles)
        {
            var baseName = Path.GetFileNameWithoutExtension(dprFile);
            var dir = Path.GetDirectoryName(dprFile) ?? "";
            var key = Path.Combine(dir, baseName);

            if (projectPaths.TryGetValue(key, out var existing))
            {
                projectPaths[key] = (dprFile, existing.DprojPath);
            }
            else
            {
                projectPaths[key] = (dprFile, null);
            }
        }

        foreach (var dprojFile in dprojFiles)
        {
            var baseName = Path.GetFileNameWithoutExtension(dprojFile);
            var dir = Path.GetDirectoryName(dprojFile) ?? "";
            var key = Path.Combine(dir, baseName);

            if (projectPaths.TryGetValue(key, out var existing))
            {
                projectPaths[key] = (existing.DprPath, dprojFile);
            }
            else
            {
                projectPaths[key] = (null, dprojFile);
            }
        }

        return projectPaths;
    }

    #endregion

    /// <summary>
    /// Handles the describe_delphi_projects tool - scans a folder for Delphi projects and provides detailed analysis.
    /// </summary>
    private async Task<object> HandleDescribeDelphiProjectsAsync(JsonElement? arguments)
    {
        var folderPath = arguments?.GetProperty("folder_path").GetString()
            ?? throw new ArgumentException("folder_path is required");

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Folder not found: {folderPath}");
        }

        var recursive = true;
        if (arguments?.TryGetProperty("recursive", out var recursiveProp) == true)
        {
            recursive = recursiveProp.GetBoolean();
        }

        var includeAnalysis = true;
        if (arguments?.TryGetProperty("include_analysis", out var analysisProp) == true)
        {
            includeAnalysis = analysisProp.GetBoolean();
        }

        // Find all .dpr and .dproj files
        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var dprFiles = Directory.GetFiles(folderPath, "*.dpr", searchOption);
        var dprojFiles = Directory.GetFiles(folderPath, "*.dproj", searchOption);

        // Build a map of projects (prefer .dproj if both exist)
        var projectPaths = new Dictionary<string, (string? DprPath, string? DprojPath)>(StringComparer.OrdinalIgnoreCase);

        foreach (var dprFile in dprFiles)
        {
            var baseName = Path.GetFileNameWithoutExtension(dprFile);
            var dir = Path.GetDirectoryName(dprFile) ?? "";
            var key = Path.Combine(dir, baseName);

            if (projectPaths.TryGetValue(key, out var existing))
            {
                projectPaths[key] = (dprFile, existing.DprojPath);
            }
            else
            {
                projectPaths[key] = (dprFile, null);
            }
        }

        foreach (var dprojFile in dprojFiles)
        {
            var baseName = Path.GetFileNameWithoutExtension(dprojFile);
            var dir = Path.GetDirectoryName(dprojFile) ?? "";
            var key = Path.Combine(dir, baseName);

            if (projectPaths.TryGetValue(key, out var existing))
            {
                projectPaths[key] = (existing.DprPath, dprojFile);
            }
            else
            {
                projectPaths[key] = (null, dprojFile);
            }
        }

        LogFoundProjects(projectPaths.Count, folderPath);

        var projectDescriptions = new ConcurrentBag<object>();
        var totalStats = new ProjectFolderStatistics();
        var completedCount = 0;
        var totalCount = projectPaths.Count;

        // Process projects in parallel with configurable degree of parallelism
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = includeAnalysis ? 8 : Environment.ProcessorCount
        };

        await Parallel.ForEachAsync(projectPaths, parallelOptions, async (kvp, ct) =>
        {
            var (dprPath, dprojPath) = kvp.Value;
            var projectName = Path.GetFileNameWithoutExtension(dprojPath ?? dprPath ?? "Unknown");
            var projectDesc = await AnalyzeProjectAsync(dprPath, dprojPath, folderPath, includeAnalysis);
            projectDescriptions.Add(projectDesc);

            var completed = Interlocked.Increment(ref completedCount);
            LogProjectCompleted(projectName, completed, totalCount);
        });

        LogBuildingResponse(projectDescriptions.Count);

        foreach (var proj in projectDescriptions)
        {
            try
            {
                dynamic d = proj;
                totalStats.TotalProjects++;

                if (includeAnalysis && d.statistics != null)
                {
                    totalStats.TotalUnits += (int)(d.statistics.units ?? 0);
                    totalStats.TotalForms += (int)(d.statistics.forms ?? 0);
                    totalStats.TotalLinesOfCode += (int)(d.statistics.lines_of_code ?? 0);
                    totalStats.TotalDatabaseUnits += (int)(d.database_analysis?.units_with_database_code ?? 0);
                }
                else if (!includeAnalysis)
                {
                    totalStats.TotalUnits += (int)(d.units ?? 0);
                    totalStats.TotalForms += (int)(d.forms ?? 0);
                    totalStats.TotalLinesOfCode += (int)(d.lines_of_code ?? 0);
                }

                string? framework = d.framework?.ToString();
                if (framework == "VCL") totalStats.VclProjects++;
                else if (framework == "FMX") totalStats.FmxProjects++;
                else if (framework == "Console") totalStats.ConsoleProjects++;
            }
            catch
            {
                // Ignore malformed entries
            }
        }

        LogReturningResponse();

        // Return simple summary - full data is persisted to database
        var response = new
        {
            success = true,
            message = "Completed",
            folder_path = folderPath,
            summary = new
            {
                total_projects = totalStats.TotalProjects,
                total_units = totalStats.TotalUnits,
                total_forms = totalStats.TotalForms,
                total_lines_of_code = totalStats.TotalLinesOfCode,
                database_units = totalStats.TotalDatabaseUnits,
                framework_breakdown = new
                {
                    vcl = totalStats.VclProjects,
                    fmx = totalStats.FmxProjects,
                    console = totalStats.ConsoleProjects
                }
            },
            note = "Full project data has been persisted to database. Use the React explorer or API to query results."
        };

        LogResponseCreated();
        var json = System.Text.Json.JsonSerializer.Serialize(response);
        LogSerializedResponseSize(json.Length);

        return response;
    }

    private async Task<object> AnalyzeProjectAsync(string? dprPath, string? dprojPath, string rootFolder, bool includeAnalysis)
    {
        var projectPath = dprojPath ?? dprPath;
        if (projectPath is null)
        {
            return new { error = "No project file found" };
        }

        var projectName = Path.GetFileNameWithoutExtension(projectPath);
        var projectDir = Path.GetDirectoryName(projectPath) ?? "";
        var relativePath = Path.GetRelativePath(rootFolder, projectDir);

        DprojMetadata? dprojMetadata = null;
        if (dprojPath is not null && File.Exists(dprojPath))
        {
            try
            {
                dprojMetadata = await _dprojParser.ParseAsync(dprojPath);
            }
            catch
            {
                // Continue without dproj metadata
            }
        }

        DelphiProject? project = null;
        try
        {
            project = await _scanner.ScanProjectAsync(projectPath, dprojMetadata);
        }
        catch (Exception ex)
        {
            return new
            {
                project_name = projectName,
                relative_path = relativePath,
                error = $"Failed to scan project: {ex.Message}"
            };
        }

        var totalLines = project.Units.Sum(u => u.LineCount);
        var totalSize = project.Units.Sum(u => u.FileSizeBytes);
        var dataModuleCount = project.Units.Count(u => u.IsDataModule);
        var formUnits = project.Units.Count(u => u.IsForm || u.HasForm);

        var databaseUnits = new List<string>();
        var databaseIndicators = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var unit in project.Units)
        {
            var allUses = unit.UsesInterface.Concat(unit.UsesImplementation).ToList();
            var dbUses = allUses.Where(u =>
                u.Contains("Data", StringComparison.OrdinalIgnoreCase) ||
                u.Contains("DB", StringComparison.OrdinalIgnoreCase) ||
                u.Contains("SQL", StringComparison.OrdinalIgnoreCase) ||
                u.Contains("ADO", StringComparison.OrdinalIgnoreCase) ||
                u.Contains("IBX", StringComparison.OrdinalIgnoreCase) ||
                u.Contains("FireDAC", StringComparison.OrdinalIgnoreCase) ||
                u.Contains("Interbase", StringComparison.OrdinalIgnoreCase) ||
                u.Contains("Firebird", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            if (dbUses.Count > 0)
            {
                databaseUnits.Add(unit.UnitName);
                foreach (var dbUse in dbUses)
                {
                    databaseIndicators.Add(dbUse);
                }
            }
        }

        var complexityFactors = new List<string>();
        if (databaseUnits.Count > 10) complexityFactors.Add("Heavy database usage");
        if (formUnits > 20) complexityFactors.Add("Large number of forms");
        if (totalLines > 50000) complexityFactors.Add("Large codebase (50k+ LOC)");
        if (project.Units.Any(u => u.UsesInterface.Any(x => x.Contains("Thread", StringComparison.OrdinalIgnoreCase))))
            complexityFactors.Add("Multi-threading");
        if (project.Units.Any(u => u.UsesInterface.Any(x => x.Contains("COM", StringComparison.OrdinalIgnoreCase))))
            complexityFactors.Add("COM/ActiveX integration");
        if (dprojMetadata?.PackageReferences.Count > 5)
            complexityFactors.Add("Multiple package dependencies");

        var complexityScore = CalculateComplexityScore(project, databaseUnits.Count, dprojMetadata);

        // Generate AI description if analysis is requested
        ProjectAiDescription? aiDescription = null;
        if (includeAnalysis)
        {
            aiDescription = await GenerateProjectDescriptionAsync(project, dprojMetadata);
        }

        // Persist to database automatically (AFTER AI description is generated)
        int? projectIdx = null;
        int? directoryIdx = null;
        string? persistError = null;
        try
        {
            var persistResult = await _persistenceService.PersistProjectAsync(
                rootFolder,
                project,
                aiDescription?.Purpose,
                aiDescription?.BusinessDomain,
                aiDescription?.KeyFeatures,
                aiDescription?.KeyEntities,
                aiDescription?.TechnicalSummary,
                complexityScore,
                dprojMetadata?.DelphiVersion);
            if (persistResult.Success)
            {
                projectIdx = persistResult.ProjectIdx;
                directoryIdx = persistResult.DirectoryIdx;
            }
            else
            {
                persistError = persistResult.ErrorMessage;
            }
        }
        catch (Exception ex)
        {
            persistError = ex.Message;
            LogPersistFailed(ex, projectName);
        }

        var description = new ProjectDescription
        {
            ProjectName = projectName,
            RelativePath = relativePath,
            DprFile = dprPath is not null ? Path.GetFileName(dprPath) : null,
            DprojFile = dprojPath is not null ? Path.GetFileName(dprojPath) : null,
            FrameworkType = dprojMetadata?.FrameworkType ?? project.FrameworkType,
            DelphiVersion = dprojMetadata?.DelphiVersion,
            UnitCount = project.Units.Count,
            FormCount = project.Forms.Count,
            DataModuleCount = dataModuleCount,
            TotalLinesOfCode = totalLines,
            TotalFileSizeKb = totalSize / 1024,
            DatabaseUnitCount = databaseUnits.Count,
            DatabaseIndicators = [.. databaseIndicators],
            ComplexityScore = complexityScore,
            ComplexityFactors = complexityFactors
        };

        if (includeAnalysis)
        {
            return new
            {
                project_name = description.ProjectName,
                relative_path = description.RelativePath,
                description = aiDescription is not null ? new
                {
                    purpose = aiDescription.Purpose,
                    business_domain = aiDescription.BusinessDomain,
                    key_features = aiDescription.KeyFeatures,
                    key_entities = aiDescription.KeyEntities,
                    technical_summary = aiDescription.TechnicalSummary
                } : null,
                files = new
                {
                    dpr = description.DprFile,
                    dproj = description.DprojFile
                },
                framework = description.FrameworkType,
                delphi_version = description.DelphiVersion,
                statistics = new
                {
                    units = description.UnitCount,
                    forms = description.FormCount,
                    data_modules = description.DataModuleCount,
                    lines_of_code = description.TotalLinesOfCode,
                    file_size_kb = description.TotalFileSizeKb
                },
                database_analysis = new
                {
                    units_with_database_code = description.DatabaseUnitCount,
                    database_units = databaseUnits.Take(20).ToList(),
                    database_technologies = description.DatabaseIndicators
                },
                complexity = new
                {
                    score = description.ComplexityScore,
                    rating = GetComplexityRating(description.ComplexityScore),
                    factors = description.ComplexityFactors
                },
                migration_recommendations = GetMigrationRecommendations(description, dprojMetadata),
                warnings = project.Warnings,
                largest_units = project.Units
                    .OrderByDescending(u => u.LineCount)
                    .Take(5)
                    .Select(u => new { name = u.UnitName, lines = u.LineCount })
                    .ToList(),
                persistence = new
                {
                    saved = projectIdx.HasValue,
                    project_idx = projectIdx,
                    directory_idx = directoryIdx,
                    error = persistError
                }
            };
        }
        else
        {
            return new
            {
                project_name = description.ProjectName,
                relative_path = description.RelativePath,
                framework = description.FrameworkType,
                units = description.UnitCount,
                forms = description.FormCount,
                lines_of_code = description.TotalLinesOfCode,
                complexity_score = description.ComplexityScore,
                persistence = new
                {
                    saved = projectIdx.HasValue,
                    project_idx = projectIdx,
                    directory_idx = directoryIdx,
                    error = persistError
                }
            };
        }
    }

    private async Task<ProjectAiDescription?> GenerateProjectDescriptionAsync(DelphiProject project, DprojMetadata? _)
    {
        try
        {
            var sampleCode = new StringBuilder();
            sampleCode.AppendLine($"Project: {project.Name}");
            sampleCode.AppendLine($"Framework: {project.FrameworkType}");
            sampleCode.AppendLine();

            if (project.Forms.Count > 0)
            {
                sampleCode.AppendLine("=== FORMS ===");
                foreach (var form in project.Forms.Take(15))
                {
                    sampleCode.AppendLine($"- {form.FormName} (Parent: {form.ParentClass ?? "TForm"})");
                }
                sampleCode.AppendLine();
            }

            sampleCode.AppendLine("=== UNITS ===");
            foreach (var unit in project.Units.Take(30))
            {
                var unitType = unit.IsDataModule ? "[DataModule]" : unit.IsForm ? "[Form]" : "[Unit]";
                sampleCode.AppendLine($"- {unit.UnitName} {unitType}");
            }
            sampleCode.AppendLine();

            var allClasses = project.Units
                .SelectMany(u => u.Classes.Select(c => new { Unit = u.UnitName, Class = c.ClassName, Parent = c.ParentClass }))
                .Take(30)
                .ToList();

            if (allClasses.Count > 0)
            {
                sampleCode.AppendLine("=== CLASSES ===");
                foreach (var cls in allClasses)
                {
                    sampleCode.AppendLine($"- {cls.Class} : {cls.Parent ?? "TObject"} (in {cls.Unit})");
                }
                sampleCode.AppendLine();
            }

            var mainUnit = project.Units
                .Where(u => u.UnitName.Contains("Main", StringComparison.OrdinalIgnoreCase) ||
                           u.UnitName.Contains("Principal", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault()
                ?? project.Units.OrderByDescending(u => u.LineCount).FirstOrDefault();

            if (mainUnit is not null && File.Exists(mainUnit.FilePath))
            {
                try
                {
                    var mainCode = await File.ReadAllTextAsync(mainUnit.FilePath);
                    var lines = mainCode.Split('\n').Take(200);
                    var truncatedCode = string.Join('\n', lines);
                    if (truncatedCode.Length > 8000)
                    {
                        truncatedCode = truncatedCode[..8000];
                    }

                    sampleCode.AppendLine($"=== MAIN UNIT CODE ({mainUnit.UnitName}) ===");
                    sampleCode.AppendLine(truncatedCode);
                    sampleCode.AppendLine();
                }
                catch
                {
                    // Skip if can't read file
                }
            }

            var dataModule = project.Units.FirstOrDefault(u => u.IsDataModule);
            if (dataModule is not null && File.Exists(dataModule.FilePath))
            {
                try
                {
                    var dmCode = await File.ReadAllTextAsync(dataModule.FilePath);
                    var lines = dmCode.Split('\n').Take(100);
                    var truncatedCode = string.Join('\n', lines);
                    if (truncatedCode.Length > 4000)
                    {
                        truncatedCode = truncatedCode[..4000];
                    }

                    sampleCode.AppendLine($"=== DATA MODULE ({dataModule.UnitName}) ===");
                    sampleCode.AppendLine(truncatedCode);
                }
                catch
                {
                    // Skip if can't read file
                }
            }

            var options = new TranslationOptions();
            var description = await _ollama.DescribeProjectAsync(sampleCode.ToString(), project.Name, options);

            return description;
        }
        catch (Exception ex)
        {
            LogProjectDescriptionFailure(ex, project.Name);
            return null;
        }
    }

    private static int CalculateComplexityScore(DelphiProject project, int databaseUnitCount, DprojMetadata? metadata)
    {
        var score = 1.0;
        var totalLines = project.Units.Sum(u => u.LineCount);
        score += Math.Min(3, totalLines / 20000.0);
        score += Math.Min(2, project.Forms.Count / 15.0);
        score += Math.Min(2, databaseUnitCount / 10.0);

        if (metadata is not null)
        {
            score += Math.Min(1, metadata.PackageReferences.Count / 10.0);
            score += Math.Min(1, metadata.SearchPaths.Count / 20.0);
        }

        return Math.Clamp((int)Math.Round(score), 1, 10);
    }

    private static string GetComplexityRating(int score) => score switch
    {
        <= 2 => "Simple",
        <= 4 => "Moderate",
        <= 6 => "Complex",
        <= 8 => "Very Complex",
        _ => "Highly Complex"
    };

    private static List<string> GetMigrationRecommendations(ProjectDescription desc, DprojMetadata? _)
    {
        var recommendations = new List<string>();

        if (desc.FrameworkType == "VCL")
        {
            if (desc.FormCount > 10)
                recommendations.Add("Consider migrating UI to React or Blazor for modern web deployment");
            else
                recommendations.Add("Small form count - could migrate to WinForms or WPF for desktop, or React for web");
        }
        else if (desc.FrameworkType == "FMX")
        {
            recommendations.Add("FMX project - consider MAUI for cross-platform or React Native for mobile");
        }
        else if (desc.FrameworkType == "Console")
        {
            recommendations.Add("Console application - straightforward migration to .NET console app or background service");
        }

        if (desc.DatabaseUnitCount > 0)
        {
            recommendations.Add($"Found {desc.DatabaseUnitCount} units with database code - recommend repository pattern with Dapper");

            if (desc.DatabaseIndicators.Any(d => d.Contains("IBX", StringComparison.OrdinalIgnoreCase) ||
                                                  d.Contains("Interbase", StringComparison.OrdinalIgnoreCase) ||
                                                  d.Contains("Firebird", StringComparison.OrdinalIgnoreCase)))
            {
                recommendations.Add("Firebird/Interbase detected - use FirebirdSql.Data.FirebirdClient NuGet package");
            }

            if (desc.DatabaseIndicators.Any(d => d.Contains("ADO", StringComparison.OrdinalIgnoreCase)))
            {
                recommendations.Add("ADO detected - may need to update connection strings and providers");
            }
        }

        if (desc.TotalLinesOfCode > 100000)
            recommendations.Add("Large codebase - recommend phased migration starting with core business logic");
        else if (desc.TotalLinesOfCode > 50000)
            recommendations.Add("Medium-large codebase - plan for iterative migration over multiple sprints");

        if (desc.ComplexityScore >= 7)
        {
            recommendations.Add("High complexity - recommend thorough analysis of dependencies before migration");
            recommendations.Add("Consider creating integration tests before migration to validate behavior");
        }

        if (desc.DataModuleCount > 0)
            recommendations.Add($"Found {desc.DataModuleCount} data modules - convert to repository/service classes");

        return recommendations;
    }

    private static object EstimateMigrationEffort(ProjectFolderStatistics stats)
    {
        var estimatedDays = stats.TotalLinesOfCode / 100.0;
        estimatedDays *= 1.0 + (stats.TotalDatabaseUnits * 0.02);
        estimatedDays *= 1.0 + (stats.TotalForms * 0.01);

        var weeks = estimatedDays / 5;
        var months = weeks / 4;

        return new
        {
            estimated_developer_days = (int)Math.Ceiling(estimatedDays),
            estimated_weeks = Math.Round(weeks, 1),
            estimated_months = Math.Round(months, 1),
            note = "Estimates are rough approximations. Actual effort depends on code quality, complexity, and team experience."
        };
    }

    private async Task<object> HandleScanProjectAsync(JsonElement? arguments)
    {
        var path = arguments?.GetProperty("path").GetString()
            ?? throw new ArgumentException("path is required");

        var session = _sessionService.CreateSession();
        session.Status = SessionStatus.Scanning;
        _sessionService.Log(session.SessionId, $"Scanning project at: {path}");

        DprojMetadata? dprojMetadata = null;
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext == ".dproj")
        {
            dprojMetadata = await _dprojParser.ParseAsync(path);
            _sessionService.Log(session.SessionId, $"Parsed .dproj: {dprojMetadata.SourceFiles.Count} source files, {dprojMetadata.SearchPaths.Count} search paths");
        }
        else if (ext == ".dpr")
        {
            var dprojPath = Path.ChangeExtension(path, ".dproj");
            if (File.Exists(dprojPath))
            {
                dprojMetadata = await _dprojParser.ParseAsync(dprojPath);
                _sessionService.Log(session.SessionId, $"Found companion .dproj: {dprojMetadata.SourceFiles.Count} source files");
            }
        }

        var project = await _scanner.ScanProjectAsync(path, dprojMetadata);
        session.Project = project;
        session.Status = SessionStatus.Analyzing;
        _sessionService.Log(session.SessionId, $"Found {project.Units.Count} units, {project.Forms.Count} forms");

        return new
        {
            session_id = session.SessionId,
            project_name = project.Name,
            root_path = project.RootPath,
            dpr_file = project.DprFilePath,
            dproj_file = project.DprojFilePath,
            framework_type = project.FrameworkType,
            compiler_defines = project.CompilerDefines,
            search_paths = project.SearchPaths,
            unit_count = project.Units.Count,
            form_count = project.Forms.Count,
            units = project.Units.Select(u => new
            {
                name = u.UnitName,
                path = u.RelativePath,
                lines = u.LineCount,
                has_form = u.HasForm || u.IsForm,
                is_data_module = u.IsDataModule,
                is_in_dpr = u.IsInDpr,
                is_orphaned = u.IsFromDproj && !u.IsInDpr,
                classes = u.Classes.Select(c => c.ClassName).ToList()
            }),
            forms = project.Forms.Select(f => new
            {
                name = f.FormName,
                parent_class = f.ParentClass,
                components = f.Components.Count
            }),
            warnings = project.Warnings
        };
    }

    private async Task<object> HandleParseDprojAsync(JsonElement? arguments)
    {
        var path = arguments?.GetProperty("path").GetString()
            ?? throw new ArgumentException("path is required");

        if (!Path.GetExtension(path).Equals(".dproj", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Path must be a .dproj file");

        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        var metadata = await _dprojParser.ParseAsync(path);

        CrossReferenceResult? crossRef = null;
        var crossRefDpr = true;
        if (arguments?.TryGetProperty("cross_reference_dpr", out var xref) == true)
            crossRefDpr = xref.GetBoolean();

        if (crossRefDpr && !string.IsNullOrEmpty(metadata.MainSource))
        {
            var dprPath = Path.Combine(metadata.ProjectDirectory, metadata.MainSource);
            if (!File.Exists(dprPath))
                dprPath = Path.ChangeExtension(path, ".dpr");

            if (File.Exists(dprPath))
            {
                var dprContent = await File.ReadAllTextAsync(dprPath);
                var dprUnits = ExtractUnitsFromDpr(dprContent);
                crossRef = _dprojParser.CrossReferenceWithDpr(metadata, dprUnits);
            }
        }

        return new
        {
            project_name = metadata.ProjectName,
            project_guid = metadata.ProjectGuid,
            delphi_version = metadata.DelphiVersion,
            framework_type = metadata.FrameworkType,
            platform = metadata.Platform,
            active_configuration = metadata.ActiveConfiguration,
            compiler_defines = metadata.CompilerDefines,
            search_paths = metadata.SearchPaths,
            resolved_search_paths = metadata.ResolvedSearchPaths,
            unit_scope_names = metadata.UnitScopeNames,
            build_configurations = metadata.BuildConfigurations.Select(c => new
            {
                name = c.Name,
                is_debug = c.IsDebug,
                defines = c.Defines,
                output_path = c.OutputPath
            }),
            source_files = metadata.SourceFiles.Select(f => new
            {
                file_name = f.FileName,
                resolved_path = f.ResolvedPath,
                file_type = f.FileType.ToString(),
                exists = f.Exists,
                is_main_source = f.IsMainSource,
                has_form = f.Form is not null,
                form_name = f.Form
            }),
            form_files = metadata.FormFiles.Select(f => new
            {
                unit_path = f.UnitPath,
                dfm_path = f.DfmPath,
                form_name = f.FormName,
                form_type = f.FormType,
                unit_exists = f.UnitExists,
                dfm_exists = f.DfmExists
            }),
            package_references = metadata.PackageReferences.Select(p => new
            {
                name = p.Name,
                is_runtime = p.IsRuntime,
                is_design_time = p.IsDesignTime
            }),
            resource_files = metadata.ResourceFiles,
            output_directory = metadata.OutputDirectory,
            resolved_output_directory = metadata.ResolvedOutputDirectory,
            version_info = new
            {
                version = metadata.VersionInfo.GetVersionString(),
                file_description = metadata.VersionInfo.FileDescription,
                company_name = metadata.VersionInfo.CompanyName,
                product_name = metadata.VersionInfo.ProductName
            },
            cross_reference = crossRef is null ? null : new
            {
                active_files = crossRef.ActiveFiles.Select(f => f.FileName),
                orphaned_files = crossRef.OrphanedFiles.Select(f => f.FileName),
                external_units = crossRef.ExternalUnits
            }
        };
    }

    private static HashSet<string> ExtractUnitsFromDpr(string dprContent)
    {
        var units = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usesMatch = DprUsesClauseRegex().Match(dprContent);

        if (!usesMatch.Success)
            return units;

        var usesContent = usesMatch.Groups[1].Value;

        foreach (var part in usesContent.Split(','))
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            var inMatch = UnitInPathRegex().Match(trimmed);

            if (inMatch.Success)
                units.Add(inMatch.Groups[1].Value);
            else
            {
                var nameMatch = UnitNameRegex().Match(trimmed);
                if (nameMatch.Success)
                    units.Add(nameMatch.Groups[1].Value);
            }
        }

        return units;
    }

    private async Task<object> HandleAnalyzeUnitAsync(JsonElement? arguments)
    {
        var sessionId = arguments?.GetProperty("session_id").GetString()
            ?? throw new ArgumentException("session_id is required");
        var unitName = arguments?.GetProperty("unit_name").GetString()
            ?? throw new ArgumentException("unit_name is required");

        var session = _sessionService.GetSession(sessionId)
            ?? throw new ArgumentException($"Session not found: {sessionId}");

        var unit = session.Project?.Units.FirstOrDefault(u =>
            u.UnitName.Equals(unitName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Unit not found: {unitName}");

        _sessionService.Log(sessionId, $"Analyzing unit: {unitName}");
        unit.AnalysisStatus = AnalysisStatus.InProgress;

        var sourceCode = await File.ReadAllTextAsync(unit.FilePath);
        var analysis = await _ollama.AnalyzeUnitAsync(sourceCode, unitName, session.TranslationOptions);

        unit.AnalysisStatus = AnalysisStatus.Completed;
        unit.AnalysisNotes = analysis.Notes;
        _sessionService.Log(sessionId, $"Analysis complete: {analysis.Complexity} complexity");

        return new
        {
            unit_name = unitName,
            complexity = analysis.Complexity,
            classes = analysis.Classes,
            records = analysis.Records,
            interfaces = analysis.Interfaces,
            standalone_functions = analysis.StandaloneFunctions,
            dependencies = analysis.Dependencies,
            notes = analysis.Notes
        };
    }

    private async Task<object> HandleAnalyzeDatabaseOperationsAsync(JsonElement? arguments)
    {
        var sessionId = arguments?.GetProperty("session_id").GetString()
            ?? throw new ArgumentException("session_id is required");
        var unitName = arguments?.GetProperty("unit_name").GetString()
            ?? throw new ArgumentException("unit_name is required");

        var session = _sessionService.GetSession(sessionId)
            ?? throw new ArgumentException($"Session not found: {sessionId}");

        var unit = session.Project?.Units.FirstOrDefault(u =>
            u.UnitName.Equals(unitName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Unit not found: {unitName}");

        _sessionService.Log(sessionId, $"Analyzing database operations in: {unitName}");

        var sourceCode = await File.ReadAllTextAsync(unit.FilePath);
        var operations = DatabaseExtractionService.ExtractOperations(sourceCode, unitName);
        var transactionGroups = DatabaseExtractionService.GroupByTransaction(operations);
        var aiAnalysis = await _ollama.AnalyzeDatabaseOperationsAsync(sourceCode, unitName, session.TranslationOptions);

        _sessionService.Log(sessionId, $"Found {operations.Count} operations, {transactionGroups.Count} transaction groups");

        return new
        {
            unit_name = unitName,
            operations = operations.Select(o => new
            {
                method_name = o.MethodName,
                class_name = o.ContainingClass,
                operation_type = o.OperationType.ToString(),
                sql = o.SqlStatement,
                table_name = o.TableName,
                parameters = o.Parameters.Select(p => new { p.Name, p.DelphiType, p.CSharpType }),
                is_transaction = o.IsPartOfTransaction,
                transaction_group_id = o.TransactionGroupId
            }),
            transaction_groups = transactionGroups.Select(g => new
            {
                group_id = g.GroupId,
                method_name = g.MethodName,
                operation_count = g.Operations.Count
            }),
            ai_analysis = new
            {
                operations = aiAnalysis.Operations,
                transaction_groups = aiAnalysis.TransactionGroups,
                dto_suggestions = aiAnalysis.DtoSuggestions
            }
        };
    }

    private async Task<object> HandleGenerateRepositoryAsync(JsonElement? arguments)
    {
        var sessionId = arguments?.GetProperty("session_id").GetString()
            ?? throw new ArgumentException("session_id is required");
        var repositoryName = arguments?.GetProperty("repository_name").GetString()
            ?? throw new ArgumentException("repository_name is required");

        var session = _sessionService.GetSession(sessionId)
            ?? throw new ArgumentException($"Session not found: {sessionId}");

        var sourceUnitNames = new List<string>();
        if (arguments?.TryGetProperty("source_units", out var unitsArray) == true)
        {
            foreach (var unitElement in unitsArray.EnumerateArray())
            {
                var name = unitElement.GetString();
                if (name is not null) sourceUnitNames.Add(name);
            }
        }

        _sessionService.Log(sessionId, $"Generating repository: {repositoryName}");

        var allOperations = new List<DatabaseOperation>();
        var allTransactionGroups = new List<TransactionGroup>();

        var unitsToProcess = sourceUnitNames.Count > 0
            ? session.Project?.Units.Where(u => sourceUnitNames.Contains(u.UnitName, StringComparer.OrdinalIgnoreCase))
            : session.Project?.Units;

        foreach (var unit in unitsToProcess ?? [])
        {
            var sourceCode = await File.ReadAllTextAsync(unit.FilePath);
            if (DatabaseExtractionService.ContainsDatabaseOperations(sourceCode))
            {
                var ops = DatabaseExtractionService.ExtractOperations(sourceCode, unit.UnitName);
                allOperations.AddRange(ops);
                allTransactionGroups.AddRange(DatabaseExtractionService.GroupByTransaction(ops));
            }
        }

        var repositoryCode = await _ollama.GenerateRepositoryAsync(
            repositoryName,
            allOperations,
            allTransactionGroups,
            session.TranslationOptions);

        session.ApiSpecification.Repositories.Add(new RepositoryDefinition
        {
            Name = repositoryName,
            Description = $"Repository generated from {allOperations.Count} database operations",
            Methods =
            [
                .. allOperations.Select(o => new RepositoryMethod
                {
                    Name = $"{o.OperationType}{o.TableName ?? "Data"}Async",
                    SqlStatement = o.SqlStatement ?? "",
                    OperationType = o.OperationType,
                    UsesTransaction = o.IsPartOfTransaction
                })
            ]
        });

        _sessionService.Log(sessionId, $"Generated repository with {allOperations.Count} methods");

        return new
        {
            repository_name = repositoryName,
            operation_count = allOperations.Count,
            transaction_groups = allTransactionGroups.Count,
            code_preview = repositoryCode[..Math.Min(1000, repositoryCode.Length)],
            full_code = repositoryCode
        };
    }

    private async Task<object> HandleGenerateControllerAsync(JsonElement? arguments)
    {
        var sessionId = arguments?.GetProperty("session_id").GetString()
            ?? throw new ArgumentException("session_id is required");
        var controllerName = arguments?.GetProperty("controller_name").GetString()
            ?? throw new ArgumentException("controller_name is required");
        var repositoryName = arguments?.GetProperty("repository_name").GetString()
            ?? throw new ArgumentException("repository_name is required");

        var session = _sessionService.GetSession(sessionId)
            ?? throw new ArgumentException($"Session not found: {sessionId}");

        _sessionService.Log(sessionId, $"Generating controller: {controllerName}");

        var repository = session.ApiSpecification.Repositories
            .FirstOrDefault(r => r.Name.Equals(repositoryName, StringComparison.OrdinalIgnoreCase));

        var operations = repository?.Methods.Select(m => new DatabaseOperation
        {
            MethodName = m.Name,
            SqlStatement = m.SqlStatement,
            OperationType = m.OperationType,
            IsPartOfTransaction = m.UsesTransaction
        }).ToList() ?? [];

        var controllerCode = await _ollama.GenerateControllerAsync(
            controllerName,
            repositoryName,
            operations,
            session.TranslationOptions);

        var routeName = controllerName.Replace("Controller", "").ToLowerInvariant();
        session.ApiSpecification.Controllers.Add(new ControllerDefinition
        {
            Name = controllerName,
            Route = routeName,
            Description = $"Controller for {repositoryName}",
            RequiredRepositories = [repositoryName]
        });

        _sessionService.Log(sessionId, $"Generated controller: {controllerName}");

        return new
        {
            controller_name = controllerName,
            route = $"{session.TranslationOptions.ApiOptions.ApiRoutePrefix}/{routeName}",
            repository = repositoryName,
            code_preview = controllerCode[..Math.Min(1000, controllerCode.Length)],
            full_code = controllerCode
        };
    }

    private async Task<object> HandleGenerateReactComponentAsync(JsonElement? arguments)
    {
        var sessionId = arguments?.GetProperty("session_id").GetString()
            ?? throw new ArgumentException("session_id is required");
        var formName = arguments?.GetProperty("form_name").GetString()
            ?? throw new ArgumentException("form_name is required");

        var session = _sessionService.GetSession(sessionId)
            ?? throw new ArgumentException($"Session not found: {sessionId}");

        var form = session.Project?.Forms.FirstOrDefault(f =>
            f.FormName.Equals(formName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Form not found: {formName}");

        var unit = session.Project?.Units.FirstOrDefault(u =>
            u.UnitName.Equals(formName, StringComparison.OrdinalIgnoreCase) ||
            u.AssociatedFormFile?.Contains(formName, StringComparison.OrdinalIgnoreCase) == true);

        _sessionService.Log(sessionId, $"Generating React component for: {formName}");

        var dfmContent = await File.ReadAllTextAsync(form.FilePath);
        var pasContent = unit is not null ? await File.ReadAllTextAsync(unit.FilePath) : "";

        var apiEndpoints = session.ApiSpecification.Controllers
            .SelectMany(c => c.Actions.Select(a =>
                $"{a.HttpMethod} /{session.TranslationOptions.ApiOptions.ApiRoutePrefix}/{c.Route}/{a.Route}"))
            .ToList();

        var result = await _ollama.GenerateReactComponentAsync(
            dfmContent,
            pasContent,
            formName,
            apiEndpoints,
            session.TranslationOptions);

        var componentDef = new ReactComponentDefinition
        {
            Name = $"{formName}",
            FileName = $"{formName}.tsx",
            ComponentType = formName.Contains("Page", StringComparison.OrdinalIgnoreCase)
                ? ComponentType.Page
                : ComponentType.Functional,
            OriginalFormName = formName,
            ApiEndpoints = result.ApiCalls
        };
        session.ReactComponents.Add(componentDef);

        _sessionService.Log(sessionId, $"Generated React component: {formName}");

        return new
        {
            component_name = formName,
            component_type = componentDef.ComponentType.ToString(),
            api_calls = result.ApiCalls,
            imports = result.Imports,
            component_code = result.ComponentCode,
            css_code = result.CssCode,
            types_code = result.TypesCode
        };
    }

    private async Task<object> HandleTranslateUnitAsync(JsonElement? arguments)
    {
        var sessionId = arguments?.GetProperty("session_id").GetString()
            ?? throw new ArgumentException("session_id is required");
        var unitName = arguments?.GetProperty("unit_name").GetString()
            ?? throw new ArgumentException("unit_name is required");

        var session = _sessionService.GetSession(sessionId)
            ?? throw new ArgumentException($"Session not found: {sessionId}");

        var unit = session.Project?.Units.FirstOrDefault(u =>
            u.UnitName.Equals(unitName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Unit not found: {unitName}");

        _sessionService.Log(sessionId, $"Translating unit: {unitName}");
        unit.TranslationStatus = TranslationStatus.InProgress;

        var sourceCode = await File.ReadAllTextAsync(unit.FilePath);
        var hasDbOperations = DatabaseExtractionService.ContainsDatabaseOperations(sourceCode);

        var apiEndpoints = session.ApiSpecification.Controllers
            .SelectMany(c => c.Actions.Select(a =>
                $"{a.HttpMethod} /{session.TranslationOptions.ApiOptions.ApiRoutePrefix}/{c.Route}/{a.Route}"))
            .ToList();

        TranslationResult result;

        if (hasDbOperations && apiEndpoints.Count > 0)
        {
            result = await _ollama.TranslateUnitWithApiCallsAsync(
                sourceCode,
                unitName,
                apiEndpoints,
                session.TranslationOptions);
        }
        else
        {
            result = await _ollama.TranslateUnitAsync(sourceCode, unitName, session.TranslationOptions);
        }

        if (result.Success)
        {
            unit.TranslationStatus = TranslationStatus.Completed;
            unit.TranslatedCode = result.TranslatedCode;
            _sessionService.Log(sessionId, $"Translation complete in {result.Duration.TotalSeconds:F1}s");
        }
        else
        {
            unit.TranslationStatus = TranslationStatus.Failed;
            _sessionService.Log(sessionId, $"Translation failed: {result.ErrorMessage}");
        }

        return new
        {
            success = result.Success,
            unit_name = unitName,
            duration_seconds = result.Duration.TotalSeconds,
            had_database_operations = hasDbOperations,
            api_endpoints_available = apiEndpoints.Count,
            error = result.ErrorMessage,
            code_preview = result.TranslatedCode?[..Math.Min(500, result.TranslatedCode?.Length ?? 0)],
            warnings = result.Warnings
        };
    }

    private async Task<object> HandleTranslateProjectAsync(JsonElement? arguments)
    {
        var sessionId = arguments?.GetProperty("session_id").GetString()
            ?? throw new ArgumentException("session_id is required");

        var maxUnits = arguments?.TryGetProperty("max_units", out var maxProp) == true
            ? maxProp.GetInt32()
            : int.MaxValue;

        var skipForms = arguments?.TryGetProperty("skip_forms", out var skipProp) == true
            && skipProp.GetBoolean();

        var session = _sessionService.GetSession(sessionId)
            ?? throw new ArgumentException($"Session not found: {sessionId}");

        if (session.Project is null)
            throw new InvalidOperationException("No project loaded in session");

        session.Status = SessionStatus.Translating;
        _sessionService.Log(sessionId, "Starting batch translation");

        var results = new List<TranslationResult>();
        var unitsToTranslate = session.Project.Units
            .Where(u => u.TranslationStatus == TranslationStatus.Pending)
            .Where(u => !skipForms || !u.IsForm)
            .Take(maxUnits)
            .ToList();

        foreach (var unit in unitsToTranslate)
        {
            _sessionService.Log(sessionId, $"Translating: {unit.UnitName}");
            unit.TranslationStatus = TranslationStatus.InProgress;

            var sourceCode = await File.ReadAllTextAsync(unit.FilePath);
            var result = await _ollama.TranslateUnitAsync(sourceCode, unit.UnitName, session.TranslationOptions);

            if (result.Success)
            {
                unit.TranslationStatus = TranslationStatus.Completed;
                unit.TranslatedCode = result.TranslatedCode;
            }
            else
            {
                unit.TranslationStatus = TranslationStatus.Failed;
            }

            results.Add(result);
            session.CurrentUnitIndex++;
        }

        var completed = session.Project.Units.Count(u => u.TranslationStatus == TranslationStatus.Completed);
        var pending = session.Project.Units.Count(u => u.TranslationStatus == TranslationStatus.Pending);

        return new
        {
            session_id = sessionId,
            translated_in_batch = results.Count,
            successful = results.Count(r => r.Success),
            failed = results.Count(r => !r.Success),
            total_completed = completed,
            total_pending = pending,
            total_units = session.Project.Units.Count,
            results = results.Select(r => new
            {
                unit = r.SourceFile,
                success = r.Success,
                duration_seconds = r.Duration.TotalSeconds,
                error = r.ErrorMessage
            })
        };
    }

    private object HandleConfigureTranslation(JsonElement? arguments)
    {
        var sessionId = arguments?.GetProperty("session_id").GetString()
            ?? throw new ArgumentException("session_id is required");

        var session = _sessionService.GetSession(sessionId)
            ?? throw new ArgumentException($"Session not found: {sessionId}");

        if (arguments?.TryGetProperty("base_namespace", out var ns) == true)
            session.TranslationOptions.BaseNamespace = ns.GetString() ?? session.TranslationOptions.BaseNamespace;

        if (arguments?.TryGetProperty("ui_target", out var ui) == true)
            session.TranslationOptions.UITarget = Enum.Parse<UITargetFramework>(ui.GetString() ?? "React");

        if (arguments?.TryGetProperty("ollama_model", out var model) == true)
            session.TranslationOptions.OllamaModel = model.GetString() ?? session.TranslationOptions.OllamaModel;

        if (arguments?.TryGetProperty("ollama_base_url", out var url) == true)
            session.TranslationOptions.OllamaBaseUrl = url.GetString() ?? session.TranslationOptions.OllamaBaseUrl;

        if (arguments?.TryGetProperty("api_route_prefix", out var prefix) == true)
            session.TranslationOptions.ApiOptions.ApiRoutePrefix = prefix.GetString() ?? session.TranslationOptions.ApiOptions.ApiRoutePrefix;

        _sessionService.Log(sessionId, "Translation options updated");

        return new
        {
            session_id = sessionId,
            options = new
            {
                base_namespace = session.TranslationOptions.BaseNamespace,
                ui_target = session.TranslationOptions.UITarget.ToString(),
                ollama_model = session.TranslationOptions.OllamaModel,
                ollama_base_url = session.TranslationOptions.OllamaBaseUrl,
                target_framework = session.TranslationOptions.TargetFramework,
                api_route_prefix = session.TranslationOptions.ApiOptions.ApiRoutePrefix
            }
        };
    }

    private async Task<object> HandleGenerateOutputAsync(JsonElement? arguments)
    {
        var sessionId = arguments?.GetProperty("session_id").GetString()
            ?? throw new ArgumentException("session_id is required");
        var outputPath = arguments?.GetProperty("output_path").GetString()
            ?? throw new ArgumentException("output_path is required");

        var session = _sessionService.GetSession(sessionId)
            ?? throw new ArgumentException($"Session not found: {sessionId}");

        if (session.Project is null)
            throw new InvalidOperationException("No project loaded in session");

        session.OutputOptions.OutputPath = outputPath;

        if (arguments?.TryGetProperty("format", out var format) == true)
            session.OutputOptions.Format = Enum.Parse<OutputFormat>(format.GetString() ?? "Folder");

        if (arguments?.TryGetProperty("generate_scripts", out var scripts) == true)
            session.OutputOptions.GenerateDeploymentScripts = scripts.GetBoolean();

        if (arguments?.TryGetProperty("script_format", out var scriptFormat) == true)
            session.OutputOptions.ScriptFormat = Enum.Parse<ScriptFormat>(scriptFormat.GetString() ?? "Both");

        session.Status = SessionStatus.GeneratingOutput;
        _sessionService.Log(sessionId, $"Generating output to: {outputPath}");

        var summary = new ProjectTranslationSummary
        {
            ProjectName = session.Project.Name,
            StartedAt = session.CreatedAt,
            CompletedAt = DateTime.UtcNow,
            TotalUnits = session.Project.Units.Count,
            SuccessfulTranslations = session.Project.Units.Count(u => u.TranslationStatus == TranslationStatus.Completed),
            FailedTranslations = session.Project.Units.Count(u => u.TranslationStatus == TranslationStatus.Failed),
            SkippedUnits = session.Project.Units.Count(u => u.TranslationStatus == TranslationStatus.Skipped),
            Results =
            [
                .. session.Project.Units
                    .Where(u => u.TranslationStatus == TranslationStatus.Completed)
                    .Select(u => new TranslationResult
                    {
                        Success = true,
                        SourceFile = u.UnitName,
                        TranslatedCode = u.TranslatedCode
                    })
            ]
        };

        var result = await _outputGenerator.GenerateOutputAsync(
            session.Project,
            summary,
            session.TranslationOptions,
            session.OutputOptions,
            session.ApiSpecification,
            session.ReactComponents);

        session.Status = SessionStatus.Completed;
        _sessionService.Log(sessionId, "Output generation complete");

        return new
        {
            session_id = sessionId,
            project_name = result.ProjectName,
            api_project_path = result.FolderPath,
            react_project_path = result.ReactProjectPath,
            zip_path = result.ZipPath,
            powershell_script = result.PowerShellScriptPath,
            bash_script = result.BashScriptPath,
            api_spec = new
            {
                repositories = session.ApiSpecification.Repositories.Count,
                controllers = session.ApiSpecification.Controllers.Count,
                dtos = session.ApiSpecification.Dtos.Count
            },
            react_components = session.ReactComponents.Count,
            summary = new
            {
                total_units = summary.TotalUnits,
                successful = summary.SuccessfulTranslations,
                failed = summary.FailedTranslations,
                skipped = summary.SkippedUnits
            }
        };
    }

    private object HandleGetSessionStatus(JsonElement? arguments)
    {
        var sessionId = arguments?.GetProperty("session_id").GetString()
            ?? throw new ArgumentException("session_id is required");

        var session = _sessionService.GetSession(sessionId)
            ?? throw new ArgumentException($"Session not found: {sessionId}");

        return new
        {
            session_id = session.SessionId,
            status = session.Status.ToString(),
            created_at = session.CreatedAt,
            project_name = session.Project?.Name,
            unit_count = session.Project?.Units.Count ?? 0,
            translated = session.Project?.Units.Count(u => u.TranslationStatus == TranslationStatus.Completed) ?? 0,
            pending = session.Project?.Units.Count(u => u.TranslationStatus == TranslationStatus.Pending) ?? 0,
            failed = session.Project?.Units.Count(u => u.TranslationStatus == TranslationStatus.Failed) ?? 0,
            current_unit_index = session.CurrentUnitIndex,
            recent_log = session.Log.TakeLast(10).ToList()
        };
    }

    private object HandleListSessions()
    {
        var sessions = _sessionService.GetActiveSessions().ToList();
        return new
        {
            count = sessions.Count,
            sessions = sessions.Select(s => new
            {
                session_id = s.SessionId,
                status = s.Status.ToString(),
                project_name = s.Project?.Name,
                created_at = s.CreatedAt
            })
        };
    }

    #region Helper Classes

    private class ProjectFolderStatistics
    {
        public int TotalProjects { get; set; }
        public int TotalUnits { get; set; }
        public int TotalForms { get; set; }
        public int TotalLinesOfCode { get; set; }
        public int TotalDatabaseUnits { get; set; }
        public int VclProjects { get; set; }
        public int FmxProjects { get; set; }
        public int ConsoleProjects { get; set; }
    }

    private class ProjectDescription
    {
        public string ProjectName { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string? DprFile { get; set; }
        public string? DprojFile { get; set; }
        public string FrameworkType { get; set; } = "Unknown";
        public string? DelphiVersion { get; set; }
        public int UnitCount { get; set; }
        public int FormCount { get; set; }
        public int DataModuleCount { get; set; }
        public int TotalLinesOfCode { get; set; }
        public long TotalFileSizeKb { get; set; }
        public int DatabaseUnitCount { get; set; }
        public List<string> DatabaseIndicators { get; set; } = [];
        public int ComplexityScore { get; set; }
        public List<string> ComplexityFactors { get; set; } = [];
    }

    #endregion
}