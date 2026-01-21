using System.Text;
using DelphiAnalysisMcpServer.Models;

namespace DelphiAnalysisMcpServer.Services;

/// <summary>
/// Service for persisting Delphi project analysis results to the database.
/// </summary>
public partial class ProjectPersistenceService(
    AnalysisRepository repository,
    ILogger<ProjectPersistenceService> logger)
{
    private readonly AnalysisRepository _repository = repository;
    private readonly ILogger<ProjectPersistenceService> _logger = logger;

    /// <summary>
    /// Gets the underlying repository for direct database access.
    /// </summary>
    public AnalysisRepository Repository => _repository;

    #region LoggerMessage Definitions

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting persistence for project: {ProjectName}")]
    private partial void LogPersistenceStarted(string projectName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Completed persistence for project: {ProjectName} (DirectoryIdx: {DirectoryIdx}, ProjectIdx: {ProjectIdx})")]
    private partial void LogPersistenceCompleted(string projectName, int directoryIdx, int projectIdx);

    [LoggerMessage(Level = LogLevel.Information, Message = "Processing unit: {UnitName} ({UnitIndex}/{TotalUnits})")]
    private partial void LogProcessingUnit(string unitName, int unitIndex, int totalUnits);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {QueryCount} database operations in unit: {UnitName}")]
    private partial void LogFoundOperations(int queryCount, string unitName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to read unit file: {FilePath}")]
    private partial void LogFailedToReadUnit(Exception ex, string filePath);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error persisting project: {ProjectName}")]
    private partial void LogPersistenceError(Exception ex, string projectName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Loaded source code for {Count} units in project: {ProjectName}")]
    private partial void LogSourceCodeLoaded(int count, string projectName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load source for unit {UnitName}: {Error}")]
    private partial void LogSourceLoadFailed(string unitName, string error);

    #endregion

    /// <summary>
    /// Loads source code from files into the database for a project's units.
    /// </summary>
    private async Task<int> LoadSourceCodeForProjectAsync(
        int projectIdx,
        List<DelphiUnit> units,
        CancellationToken cancellationToken)
    {
        var loaded = 0;
        
        foreach (var unit in units)
        {
            try
            {
                if (!File.Exists(unit.FilePath))
                    continue;

                // Read source file (Delphi files are typically Windows-1252 encoded)
                var encoding = System.Text.Encoding.GetEncoding(1252);
                var sourceCode = await File.ReadAllTextAsync(unit.FilePath, encoding, cancellationToken);
                
                // Strip null bytes - PostgreSQL TEXT columns don't allow them
                if (sourceCode.Contains('\0'))
                {
                    sourceCode = string.Concat(sourceCode.Split('\0'));
                }

                // Try to load DFM file
                string? dfmSource = null;
                var dfmPath = unit.DfmFilePath;
                
                if (string.IsNullOrEmpty(dfmPath) && !string.IsNullOrEmpty(unit.AssociatedFormFile))
                {
                    // AssociatedFormFile might be a full path or just a filename
                    if (Path.IsPathRooted(unit.AssociatedFormFile) && File.Exists(unit.AssociatedFormFile))
                    {
                        dfmPath = unit.AssociatedFormFile;
                    }
                    else
                    {
                        // Construct DFM path from .pas directory
                        var pasDir = Path.GetDirectoryName(unit.FilePath);
                        if (!string.IsNullOrEmpty(pasDir))
                        {
                            dfmPath = Path.Combine(pasDir, unit.AssociatedFormFile);
                        }
                    }
                }
                
                // Fallback: Try to find DFM file by unit name in same directory as .pas file
                if ((string.IsNullOrEmpty(dfmPath) || !File.Exists(dfmPath)) && unit.IsForm)
                {
                    var pasDir = Path.GetDirectoryName(unit.FilePath);
                    if (!string.IsNullOrEmpty(pasDir))
                    {
                        // Try unit name (last segment if dotted)
                        var baseName = unit.UnitName.Contains('.') 
                            ? unit.UnitName.Split('.').Last() 
                            : unit.UnitName;
                        var candidatePath = Path.Combine(pasDir, baseName + ".dfm");
                        if (File.Exists(candidatePath))
                        {
                            dfmPath = candidatePath;
                        }
                        else
                        {
                            // Try the .pas filename
                            baseName = Path.GetFileNameWithoutExtension(unit.FilePath);
                            candidatePath = Path.Combine(pasDir, baseName + ".dfm");
                            if (File.Exists(candidatePath))
                            {
                                dfmPath = candidatePath;
                            }
                        }
                    }
                }

                if (!string.IsNullOrEmpty(dfmPath) && File.Exists(dfmPath))
                {
                    dfmSource = await File.ReadAllTextAsync(dfmPath, encoding, cancellationToken);
                    if (dfmSource.Contains('\0'))
                    {
                        dfmSource = string.Concat(dfmSource.Split('\0'));
                    }
                }

                // Update the database
                await _repository.UpdateUnitSourceCodeAsync(projectIdx, unit.UnitName, sourceCode, dfmSource, cancellationToken);
                loaded++;
            }
            catch (Exception ex)
            {
                LogSourceLoadFailed(unit.UnitName, ex.Message);
            }
        }

        return loaded;
    }

    /// <summary>
    /// Persists a Delphi project and all its analysis data to the database.
    /// </summary>
    public async Task<ProjectPersistenceResult> PersistProjectAsync(
        string directoryPath,
        DelphiProject project,
        string? purpose = null,
        string? businessDomain = null,
        List<string>? keyFeatures = null,
        List<string>? keyEntities = null,
        string? technicalSummary = null,
        int? complexityScore = null,
        string? delphiVersion = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ProjectPersistenceResult
        {
            ProjectName = project.Name,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            LogPersistenceStarted(project.Name);

            var unitOperations = new List<(DelphiUnit Unit, List<DatabaseOperation> Operations, List<string> QueryComponentTypes, string? SourceCode)>();

            for (int i = 0; i < project.Units.Count; i++)
            {
                var unit = project.Units[i];
                LogProcessingUnit(unit.UnitName, i + 1, project.Units.Count);

                try
                {
                    if (!File.Exists(unit.FilePath))
                    {
                        result.Warnings.Add($"Unit file not found: {unit.FilePath}");
                        unitOperations.Add((unit, [], [], null));
                        continue;
                    }

                    var sourceCode = await File.ReadAllTextAsync(unit.FilePath, cancellationToken);

                    var operationsWithTypes = DatabaseExtractionService.ExtractOperationsWithTypes(sourceCode, unit.UnitName);
                    var operations = operationsWithTypes.Select(x => x.Operation).ToList();
                    var componentTypes = operationsWithTypes.Select(x => x.ComponentType).ToList();

                    if (operations.Count > 0)
                    {
                        LogFoundOperations(operations.Count, unit.UnitName);
                        result.TotalQueriesFound += operations.Count;
                    }

                    // Pass source code through to enable method extraction
                    unitOperations.Add((unit, operations, componentTypes, sourceCode));
                    result.UnitsProcessed++;
                }
                catch (Exception ex)
                {
                    LogFailedToReadUnit(ex, unit.FilePath);
                    result.Warnings.Add($"Failed to process unit {unit.UnitName}: {ex.Message}");
                    unitOperations.Add((unit, [], [], null));
                }
            }

            var (directoryIdx, projectIdx) = await _repository.SaveProjectAsync(
                directoryPath,
                project,
                unitOperations,
                purpose,
                businessDomain,
                keyFeatures,
                keyEntities,
                technicalSummary,
                complexityScore,
                delphiVersion,
                cancellationToken);

            result.DirectoryIdx = directoryIdx;
            result.ProjectIdx = projectIdx;

            // Load source code into database for all units
            var sourceLoaded = await LoadSourceCodeForProjectAsync(projectIdx, project.Units, cancellationToken);
            result.SourceFilesLoaded = sourceLoaded;
            
            if (sourceLoaded > 0)
            {
                LogSourceCodeLoaded(sourceLoaded, project.Name);
            }

            // CRITICAL FIX: Query actual statistics from database after persistence
            // This ensures we report accurate counts instead of zeros
            var stats = await _repository.GetProjectStatisticsAsync(projectIdx, cancellationToken);
            
            // Update result with actual statistics from database
            result.TotalQueriesFound = stats.QueriesFound;  // THE MAIN FIX
            result.UnitsProcessed = stats.UnitsProcessed;
            result.SourceFilesLoaded = stats.SourceFilesLoaded;
            
            _logger.LogInformation(
                "Project {ProjectName} statistics: {Units} units, {Forms} forms, {Queries} queries, {SourceFiles} source files",
                project.Name, stats.Units, stats.Forms, stats.QueriesFound, stats.SourceFilesLoaded);

            result.Success = true;
            result.CompletedAt = DateTime.UtcNow;

            LogPersistenceCompleted(project.Name, directoryIdx, projectIdx);
        }
        catch (Exception ex)
        {
            LogPersistenceError(ex, project.Name);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = DateTime.UtcNow;
        }

        return result;
    }

    /// <summary>
    /// Persists multiple projects from a folder scan to the database.
    /// </summary>
    public async Task<List<ProjectPersistenceResult>> PersistProjectsAsync(
        string folderPath,
        List<DelphiProject> projects,
        CancellationToken cancellationToken = default)
    {
        var results = new List<ProjectPersistenceResult>();

        foreach (var project in projects)
        {
            var result = await PersistProjectAsync(folderPath, project, cancellationToken: cancellationToken);
            results.Add(result);
        }

        return results;
    }

    /// <summary>
    /// Gets a summary of queries stored for a project.
    /// </summary>
    public async Task<List<QuerySummary>> GetProjectQuerySummaryAsync(
        int projectIdx,
        CancellationToken cancellationToken = default)
    {
        return await _repository.GetQuerySummaryByProjectAsync(projectIdx, cancellationToken);
    }
}

/// <summary>
/// Result of persisting a project to the database.
/// </summary>
public class ProjectPersistenceResult
{
    public string ProjectName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public int DirectoryIdx { get; set; }
    public int ProjectIdx { get; set; }
    public int UnitsProcessed { get; set; }
    public int TotalQueriesFound { get; set; }
    public int SourceFilesLoaded { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = [];
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public TimeSpan Duration => CompletedAt.HasValue
        ? CompletedAt.Value - StartedAt
        : TimeSpan.Zero;
}