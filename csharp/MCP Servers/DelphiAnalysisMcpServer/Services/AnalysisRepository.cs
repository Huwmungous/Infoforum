using DelphiAnalysisMcpServer.Models;
using Npgsql;
using NpgsqlTypes;

namespace DelphiAnalysisMcpServer.Services;

/// <summary>
/// Repository service for persisting Delphi analysis data to PostgreSQL.
/// </summary>
public partial class AnalysisRepository : IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<AnalysisRepository> _logger;
    private NpgsqlDataSource? _dataSource;
    private bool _disposed;

    public AnalysisRepository(IConfiguration configuration, ILogger<AnalysisRepository> logger)
    {
        _connectionString = configuration.GetConnectionString("DelphiAnalysis")
            ?? throw new InvalidOperationException("Connection string 'DelphiAnalysis' not found");
        _logger = logger;
    }

    private NpgsqlDataSource DataSourceInternal => _dataSource ??= NpgsqlDataSource.Create(_connectionString);

    /// <summary>
    /// Gets the NpgsqlDataSource for database access.
    /// </summary>
    public NpgsqlDataSource DataSource => DataSourceInternal;

    #region LoggerMessage Definitions

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved directory: {DirectoryName} (Idx: {DirectoryIdx})")]
    private partial void LogDirectorySaved(string directoryName, int directoryIdx);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved project: {ProjectName} (Idx: {ProjectIdx})")]
    private partial void LogProjectSaved(string projectName, int projectIdx);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved unit: {UnitName} (Idx: {UnitIdx})")]
    private partial void LogUnitSaved(string unitName, int unitIdx);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved {QueryCount} queries for unit: {UnitName}")]
    private partial void LogQueriesSaved(int queryCount, string unitName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error saving to database")]
    private partial void LogDatabaseError(Exception ex);

    [LoggerMessage(Level = LogLevel.Information, Message = "Cleared project data for project Idx: {ProjectIdx}")]
    private partial void LogProjectDataCleared(int projectIdx);

    [LoggerMessage(Level = LogLevel.Information, Message = "Backed up source code for {Count} units in project {ProjectIdx}")]
    private partial void LogSourceCodeBackedUp(int count, int projectIdx);

    [LoggerMessage(Level = LogLevel.Information, Message = "Restored source code for {Count} units")]
    private partial void LogSourceCodeRestored(int count);

    #endregion

    #region Directory Operations

    /// <summary>
    /// Gets or creates a directory entry.
    /// </summary>
    public async Task<int> GetOrCreateDirectoryAsync(string path, string name, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "SELECT fn_get_or_create_directory(@path, @name)";
        cmd.Parameters.AddWithValue("path", path);
        cmd.Parameters.AddWithValue("name", name);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        var directoryIdx = Convert.ToInt32(result);

        LogDirectorySaved(name, directoryIdx);
        return directoryIdx;
    }

    #endregion

    #region Project Operations

    /// <summary>
    /// Gets or creates a project entry.
    /// </summary>
    public async Task<int> GetOrCreateProjectAsync(
        int directoryIdx,
        string name,
        string rootPath,
        string? dprFilePath,
        string? dprojFilePath,
        string frameworkType,
        string? purpose = null,
        string? businessDomain = null,
        List<string>? keyFeatures = null,
        List<string>? keyEntities = null,
        string? technicalSummary = null,
        int? complexityScore = null,
        string? delphiVersion = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            SELECT fn_get_or_create_project(
                @directory_idx, @name, @root_path, 
                @dpr_file_path, @dproj_file_path, @framework_type,
                @purpose, @business_domain, @key_features, @key_entities,
                @technical_summary, @complexity_score, @delphi_version
            )
            """;
        cmd.Parameters.AddWithValue("directory_idx", directoryIdx);
        cmd.Parameters.AddWithValue("name", name);
        cmd.Parameters.AddWithValue("root_path", rootPath);
        cmd.Parameters.AddWithValue("dpr_file_path", (object?)dprFilePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("dproj_file_path", (object?)dprojFilePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("framework_type", frameworkType);
        cmd.Parameters.AddWithValue("purpose", (object?)purpose ?? DBNull.Value);
        cmd.Parameters.AddWithValue("business_domain", (object?)businessDomain ?? DBNull.Value);
        cmd.Parameters.AddWithValue("key_features", (object?)keyFeatures?.ToArray() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("key_entities", (object?)keyEntities?.ToArray() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("technical_summary", (object?)technicalSummary ?? DBNull.Value);
        cmd.Parameters.AddWithValue("complexity_score", (object?)complexityScore ?? DBNull.Value);
        cmd.Parameters.AddWithValue("delphi_version", (object?)delphiVersion ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        var projectIdx = Convert.ToInt32(result);

        LogProjectSaved(name, projectIdx);
        return projectIdx;
    }

    /// <summary>
    /// Clears all data for a project before rescanning.
    /// </summary>
    public async Task ClearProjectDataAsync(int projectIdx, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "CALL sp_clear_project_data(@project_idx)";
        cmd.Parameters.AddWithValue("project_idx", projectIdx);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        LogProjectDataCleared(projectIdx);
    }

    /// <summary>
    /// Backs up source_code and dfm_source for all units in a project before clearing.
    /// </summary>
    public async Task<Dictionary<string, (string? SourceCode, string? DfmSource)>> BackupUnitSourceCodeAsync(
        int projectIdx,
        CancellationToken cancellationToken = default)
    {
        var backup = new Dictionary<string, (string? SourceCode, string? DfmSource)>(StringComparer.OrdinalIgnoreCase);

        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            SELECT unit_name, source_code, dfm_source
            FROM unit
            WHERE project_idx = @project_idx
              AND (source_code IS NOT NULL OR dfm_source IS NOT NULL)
            """;
        cmd.Parameters.AddWithValue("project_idx", projectIdx);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var unitName = reader.GetString(0);
            var sourceCode = reader.IsDBNull(1) ? null : reader.GetString(1);
            var dfmSource = reader.IsDBNull(2) ? null : reader.GetString(2);
            backup[unitName] = (sourceCode, dfmSource);
        }

        LogSourceCodeBackedUp(backup.Count, projectIdx);
        return backup;
    }

    /// <summary>
    /// Restores source_code and dfm_source for units after they've been recreated.
    /// </summary>
    public async Task RestoreUnitSourceCodeAsync(
        Dictionary<string, int> unitIdxMap,
        Dictionary<string, (string? SourceCode, string? DfmSource)> sourceBackup,
        CancellationToken cancellationToken = default)
    {
        if (sourceBackup.Count == 0) return;

        var restored = 0;
        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);

        foreach (var (unitName, (sourceCode, dfmSource)) in sourceBackup)
        {
            if (!unitIdxMap.TryGetValue(unitName, out var unitIdx)) continue;
            if (sourceCode == null && dfmSource == null) continue;

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                UPDATE unit SET 
                    source_code = @source_code,
                    dfm_source = @dfm_source
                WHERE idx = @unit_idx
                """;
            cmd.Parameters.AddWithValue("unit_idx", unitIdx);
            cmd.Parameters.AddWithValue("source_code", (object?)sourceCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("dfm_source", (object?)dfmSource ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            restored++;
        }

        LogSourceCodeRestored(restored);
    }

    /// <summary>
    /// Updates source code for a specific unit by project and unit name.
    /// </summary>
    public async Task UpdateUnitSourceCodeAsync(
        int projectIdx,
        string unitName,
        string? sourceCode,
        string? dfmSource,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            UPDATE unit SET 
                source_code = @source_code,
                dfm_source = @dfm_source,
                updated_at = CURRENT_TIMESTAMP
            WHERE project_idx = @project_idx AND unit_name = @unit_name
            """;
        cmd.Parameters.AddWithValue("project_idx", projectIdx);
        cmd.Parameters.AddWithValue("unit_name", unitName);
        cmd.Parameters.AddWithValue("source_code", (object?)sourceCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("dfm_source", (object?)dfmSource ?? DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Saves project search paths.
    /// </summary>
    public async Task SaveProjectSearchPathsAsync(int projectIdx, List<string> searchPaths, CancellationToken cancellationToken = default)
    {
        if (searchPaths.Count == 0) return;

        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);

        // Delete existing
        await using (var deleteCmd = connection.CreateCommand())
        {
            deleteCmd.CommandText = "DELETE FROM project_search_path WHERE project_idx = @project_idx";
            deleteCmd.Parameters.AddWithValue("project_idx", projectIdx);
            await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Insert new
        for (int i = 0; i < searchPaths.Count; i++)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO project_search_path (project_idx, path, path_order)
                VALUES (@project_idx, @path, @path_order)
                """;
            cmd.Parameters.AddWithValue("project_idx", projectIdx);
            cmd.Parameters.AddWithValue("path", searchPaths[i]);
            cmd.Parameters.AddWithValue("path_order", i);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Saves project compiler defines.
    /// </summary>
    public async Task SaveProjectCompilerDefinesAsync(int projectIdx, List<string> defines, CancellationToken cancellationToken = default)
    {
        if (defines.Count == 0) return;

        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);

        // Delete existing
        await using (var deleteCmd = connection.CreateCommand())
        {
            deleteCmd.CommandText = "DELETE FROM project_compiler_define WHERE project_idx = @project_idx";
            deleteCmd.Parameters.AddWithValue("project_idx", projectIdx);
            await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Insert new
        foreach (var define in defines)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO project_compiler_define (project_idx, define_name)
                VALUES (@project_idx, @define_name)
                ON CONFLICT (project_idx, define_name) DO NOTHING
                """;
            cmd.Parameters.AddWithValue("project_idx", projectIdx);
            cmd.Parameters.AddWithValue("define_name", define);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Saves project warnings.
    /// </summary>
    public async Task SaveProjectWarningsAsync(int projectIdx, List<string> warnings, CancellationToken cancellationToken = default)
    {
        if (warnings.Count == 0) return;

        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);

        foreach (var warning in warnings)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO project_warning (project_idx, warning_message)
                VALUES (@project_idx, @warning_message)
                """;
            cmd.Parameters.AddWithValue("project_idx", projectIdx);
            cmd.Parameters.AddWithValue("warning_message", warning);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    #endregion

    #region Unit Operations

    /// <summary>
    /// Gets or creates a unit entry.
    /// </summary>
    public async Task<int> GetOrCreateUnitAsync(
        int projectIdx,
        DelphiUnit unit,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            SELECT fn_get_or_create_unit(
                @project_idx, @unit_name, @file_path, @relative_path, 
                @file_size_bytes, @line_count, @associated_dfm_file, @dfm_file_path,
                @is_data_module, @is_form, @has_form, @form_name, @form_type,
                @is_from_dproj, @is_in_dpr
            )
            """;
        cmd.Parameters.AddWithValue("project_idx", projectIdx);
        cmd.Parameters.AddWithValue("unit_name", unit.UnitName);
        cmd.Parameters.AddWithValue("file_path", unit.FilePath);
        cmd.Parameters.AddWithValue("relative_path", unit.RelativePath);
        cmd.Parameters.AddWithValue("file_size_bytes", unit.FileSizeBytes);
        cmd.Parameters.AddWithValue("line_count", unit.LineCount);
        cmd.Parameters.AddWithValue("associated_dfm_file", (object?)unit.AssociatedFormFile ?? DBNull.Value);
        cmd.Parameters.AddWithValue("dfm_file_path", (object?)unit.DfmFilePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("is_data_module", unit.IsDataModule);
        cmd.Parameters.AddWithValue("is_form", unit.IsForm);
        cmd.Parameters.AddWithValue("has_form", unit.HasForm);
        cmd.Parameters.AddWithValue("form_name", (object?)unit.FormName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("form_type", (object?)unit.FormType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("is_from_dproj", unit.IsFromDproj);
        cmd.Parameters.AddWithValue("is_in_dpr", unit.IsInDpr);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        var unitIdx = Convert.ToInt32(result);

        LogUnitSaved(unit.UnitName, unitIdx);
        return unitIdx;
    }

    /// <summary>
    /// Saves unit uses clauses (interface section).
    /// </summary>
    public async Task SaveUnitUsesInterfaceAsync(int unitIdx, List<string> uses, CancellationToken cancellationToken = default)
    {
        if (uses.Count == 0) return;

        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);

        // Delete existing
        await using (var deleteCmd = connection.CreateCommand())
        {
            deleteCmd.CommandText = "DELETE FROM unit_uses_interface WHERE unit_idx = @unit_idx";
            deleteCmd.Parameters.AddWithValue("unit_idx", unitIdx);
            await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Insert new
        foreach (var usedUnit in uses)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO unit_uses_interface (unit_idx, referenced_unit)
                VALUES (@unit_idx, @referenced_unit)
                """;
            cmd.Parameters.AddWithValue("unit_idx", unitIdx);
            cmd.Parameters.AddWithValue("referenced_unit", usedUnit);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Saves unit uses clauses (implementation section).
    /// </summary>
    public async Task SaveUnitUsesImplementationAsync(int unitIdx, List<string> uses, CancellationToken cancellationToken = default)
    {
        if (uses.Count == 0) return;

        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);

        // Delete existing
        await using (var deleteCmd = connection.CreateCommand())
        {
            deleteCmd.CommandText = "DELETE FROM unit_uses_implementation WHERE unit_idx = @unit_idx";
            deleteCmd.Parameters.AddWithValue("unit_idx", unitIdx);
            await deleteCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        // Insert new
        foreach (var usedUnit in uses)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO unit_uses_implementation (unit_idx, referenced_unit)
                VALUES (@unit_idx, @referenced_unit)
                """;
            cmd.Parameters.AddWithValue("unit_idx", unitIdx);
            cmd.Parameters.AddWithValue("referenced_unit", usedUnit);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    #endregion

    #region Class Operations

    /// <summary>
    /// Saves a class and returns its Idx.
    /// </summary>
    public async Task<int> SaveClassAsync(int unitIdx, DelphiClass delphiClass, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            INSERT INTO class (unit_idx, class_name, parent_class, default_visibility)
            VALUES (@unit_idx, @class_name, @parent_class, @default_visibility::class_visibility)
            ON CONFLICT (unit_idx, class_name) DO UPDATE SET
                parent_class = EXCLUDED.parent_class,
                default_visibility = EXCLUDED.default_visibility,
                updated_at = CURRENT_TIMESTAMP
            RETURNING idx
            """;
        cmd.Parameters.AddWithValue("unit_idx", unitIdx);
        cmd.Parameters.AddWithValue("class_name", delphiClass.ClassName);
        cmd.Parameters.AddWithValue("parent_class", (object?)delphiClass.ParentClass ?? DBNull.Value);
        cmd.Parameters.AddWithValue("default_visibility", delphiClass.DefaultVisibility.ToString());

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Saves class interfaces.
    /// </summary>
    public async Task SaveClassInterfacesAsync(int classIdx, List<string> interfaces, CancellationToken cancellationToken = default)
    {
        if (interfaces.Count == 0) return;

        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);

        foreach (var iface in interfaces)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO class_interface (class_idx, interface_name)
                VALUES (@class_idx, @interface_name)
                ON CONFLICT (class_idx, interface_name) DO NOTHING
                """;
            cmd.Parameters.AddWithValue("class_idx", classIdx);
            cmd.Parameters.AddWithValue("interface_name", iface);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    #endregion

    #region Method Operations

    /// <summary>
    /// Saves a method and returns its Idx.
    /// </summary>
    public async Task<int> SaveMethodAsync(
        int? classIdx,
        int? recordIdx,
        int? unitIdx,
        DelphiMethod method,
        bool isStandalone,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            INSERT INTO method (
                class_idx, record_idx, unit_idx, method_name, kind, return_type, visibility,
                is_virtual, is_override, is_abstract, is_static, is_overload, is_standalone, source_code
            )
            VALUES (
                @class_idx, @record_idx, @unit_idx, @method_name, @kind::method_kind, @return_type, @visibility::class_visibility,
                @is_virtual, @is_override, @is_abstract, @is_static, @is_overload, @is_standalone, @source_code
            )
            ON CONFLICT DO NOTHING
            RETURNING idx
            """;
        cmd.Parameters.AddWithValue("class_idx", (object?)classIdx ?? DBNull.Value);
        cmd.Parameters.AddWithValue("record_idx", (object?)recordIdx ?? DBNull.Value);
        cmd.Parameters.AddWithValue("unit_idx", (object?)unitIdx ?? DBNull.Value);
        cmd.Parameters.AddWithValue("method_name", method.Name);
        cmd.Parameters.AddWithValue("kind", method.Kind.ToString());
        cmd.Parameters.AddWithValue("return_type", (object?)method.ReturnType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("visibility", method.Visibility.ToString());
        cmd.Parameters.AddWithValue("is_virtual", method.IsVirtual);
        cmd.Parameters.AddWithValue("is_override", method.IsOverride);
        cmd.Parameters.AddWithValue("is_abstract", method.IsAbstract);
        cmd.Parameters.AddWithValue("is_static", method.IsStatic);
        cmd.Parameters.AddWithValue("is_overload", method.IsOverload);
        cmd.Parameters.AddWithValue("is_standalone", isStandalone);
        cmd.Parameters.AddWithValue("source_code", (object?)method.SourceCode ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result != null ? Convert.ToInt32(result) : 0;
    }

    /// <summary>
    /// Saves a standalone procedure and returns its Idx.
    /// </summary>
    public async Task<int> SaveStandaloneProcedureAsync(
        int unitIdx,
        DelphiProcedure procedure,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            INSERT INTO method (
                unit_idx, method_name, kind, return_type, visibility, is_standalone, source_code
            )
            VALUES (
                @unit_idx, @method_name, @kind::method_kind, @return_type, 'Public'::class_visibility, TRUE, @source_code
            )
            ON CONFLICT DO NOTHING
            RETURNING idx
            """;
        cmd.Parameters.AddWithValue("unit_idx", unitIdx);
        cmd.Parameters.AddWithValue("method_name", procedure.Name);
        cmd.Parameters.AddWithValue("kind", procedure.Kind.ToString());
        cmd.Parameters.AddWithValue("return_type", (object?)procedure.ReturnType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("source_code", (object?)procedure.SourceCode ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result != null ? Convert.ToInt32(result) : 0;
    }

    /// <summary>
    /// Finds a method by class and name, returning its Idx.
    /// </summary>
    public async Task<int?> FindMethodIdxAsync(
        int? classIdx,
        int? unitIdx,
        string methodName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        if (classIdx.HasValue)
        {
            cmd.CommandText = """
                SELECT idx FROM method 
                WHERE class_idx = @class_idx AND method_name = @method_name
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("class_idx", classIdx.Value);
        }
        else if (unitIdx.HasValue)
        {
            cmd.CommandText = """
                SELECT idx FROM method 
                WHERE unit_idx = @unit_idx AND method_name = @method_name AND is_standalone = TRUE
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("unit_idx", unitIdx.Value);
        }
        else
        {
            return null;
        }

        cmd.Parameters.AddWithValue("method_name", methodName);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result != null ? Convert.ToInt32(result) : null;
    }

    /// <summary>
    /// Finds a method by unit and optional class, returning its Idx.
    /// Searches class methods first, then standalone methods.
    /// </summary>
    public async Task<int?> FindMethodIdxByUnitAsync(
        int unitIdx,
        string? className,
        string methodName,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        if (!string.IsNullOrEmpty(className))
        {
            // First try to find by class name
            cmd.CommandText = """
                SELECT m.idx FROM method m
                JOIN class c ON c.idx = m.class_idx
                WHERE c.unit_idx = @unit_idx 
                  AND c.class_name = @class_name 
                  AND m.method_name = @method_name
                LIMIT 1
                """;
            cmd.Parameters.AddWithValue("class_name", className);
        }
        else
        {
            // Look for standalone method or any method with this name in the unit
            cmd.CommandText = """
                SELECT m.idx FROM method m
                LEFT JOIN class c ON c.idx = m.class_idx
                WHERE (m.unit_idx = @unit_idx OR c.unit_idx = @unit_idx)
                  AND m.method_name = @method_name
                ORDER BY m.is_standalone DESC
                LIMIT 1
                """;
        }

        cmd.Parameters.AddWithValue("unit_idx", unitIdx);
        cmd.Parameters.AddWithValue("method_name", methodName);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result != null ? Convert.ToInt32(result) : null;
    }

    /// <summary>
    /// Cleans up stale records that are not in the current unit list.
    /// </summary>
    public async Task CleanupStaleRecordsAsync(
        int projectIdx,
        List<string> currentUnitNames,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "CALL sp_cleanup_stale_records(@project_idx, @current_unit_names)";
        cmd.Parameters.AddWithValue("project_idx", projectIdx);
        cmd.Parameters.AddWithValue("current_unit_names", currentUnitNames.ToArray());

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Saves methods from parsed class declarations and standalone procedures.
    /// Used as fallback when source code extraction is not available.
    /// </summary>
    private async Task SaveMethodsFromParsedDataAsync(
        DelphiUnit unit,
        int unitIdx,
        Dictionary<(int UnitIdx, string ClassName), int> classIdxMap,
        Dictionary<(int UnitIdx, string? ClassName, string MethodName), int> methodIdxMap,
        CancellationToken cancellationToken)
    {
        // Save class methods from parsed declarations
        foreach (var delphiClass in unit.Classes)
        {
            if (classIdxMap.TryGetValue((unitIdx, delphiClass.ClassName), out var classIdx))
            {
                foreach (var method in delphiClass.Methods)
                {
                    // Pass unitIdx for all methods (class methods and standalone) for easier querying
                    var methodIdx = await SaveMethodAsync(classIdx, null, unitIdx, method, false, cancellationToken);
                    if (methodIdx > 0)
                    {
                        methodIdxMap[(unitIdx, delphiClass.ClassName, method.Name)] = methodIdx;
                    }
                }
            }
        }

        // Save standalone procedures
        foreach (var procedure in unit.StandaloneProcedures)
        {
            var delphiMethod = new DelphiMethod
            {
                Name = procedure.Name,
                Kind = procedure.Kind,
                ReturnType = procedure.ReturnType,
                Parameters = procedure.Parameters,
                SourceCode = procedure.SourceCode
            };
            var methodIdx = await SaveMethodAsync(null, null, unitIdx, delphiMethod, true, cancellationToken);
            if (methodIdx > 0)
            {
                methodIdxMap[(unitIdx, null, procedure.Name)] = methodIdx;
            }
        }
    }

    #endregion

    #region Query Operations

    /// <summary>
    /// Saves a database query detected in code.
    /// </summary>
    // REPLACEMENT FOR SaveQueryAsync METHOD IN AnalysisRepository.cs
    // This version includes transaction isolation to prevent cascade failures
    // Replace the existing SaveQueryAsync method (starting at line 732) with this code

    public async Task<int> SaveQueryAsync(
        int unitIdx,
        int? methodIdx,
        int? classIdx,
        DatabaseOperation operation,
        string queryComponentType,
        CancellationToken cancellationToken = default)
    {
        // Each query gets its own connection and transaction for isolation
        // This prevents cascade failures when one query fails
        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.Transaction = transaction;

            cmd.CommandText = """
            INSERT INTO query (
                unit_idx, method_idx, class_idx, containing_class, method_name,
                query_component_type, sql_text, operation_type, table_name,
                is_part_of_transaction, transaction_group_id, original_delphi_code,
                source_line_number
            )
            VALUES (
                @unit_idx, @method_idx, @class_idx, @containing_class, @method_name,
                @query_component_type, @sql_text, @operation_type::database_operation_type, @table_name,
                @is_part_of_transaction, @transaction_group_id, @original_delphi_code,
                @source_line_number
            )
            RETURNING idx
            """;
            cmd.Parameters.AddWithValue("unit_idx", unitIdx);
            cmd.Parameters.AddWithValue("method_idx", (object?)methodIdx ?? DBNull.Value);
            cmd.Parameters.AddWithValue("class_idx", (object?)classIdx ?? DBNull.Value);
            cmd.Parameters.AddWithValue("containing_class", (object?)operation.ContainingClass ?? DBNull.Value);
            cmd.Parameters.AddWithValue("method_name", (object?)operation.MethodName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("query_component_type", queryComponentType);
            cmd.Parameters.AddWithValue("sql_text", (object?)operation.SqlStatement ?? DBNull.Value);

            // Pass operation type as Text and let PostgreSQL cast to enum via :: operator in SQL
            var operationTypeParam = new NpgsqlParameter("operation_type", NpgsqlDbType.Text)
            {
                Value = operation.OperationType.ToString()
            };
            cmd.Parameters.Add(operationTypeParam);

            cmd.Parameters.AddWithValue("table_name", (object?)operation.TableName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("is_part_of_transaction", operation.IsPartOfTransaction);
            cmd.Parameters.AddWithValue("transaction_group_id", (object?)operation.TransactionGroupId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("original_delphi_code", (object?)operation.OriginalDelphiCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("source_line_number", (object?)operation.SourceLineNumber ?? DBNull.Value);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            int queryIdx = Convert.ToInt32(result);

            // Save parameters and field accesses within same transaction
            if (operation.Parameters.Count > 0)
            {
                for (int i = 0; i < operation.Parameters.Count; i++)
                {
                    var param = operation.Parameters[i];
                    await using var paramCmd = connection.CreateCommand();
                    paramCmd.Transaction = transaction;

                    paramCmd.CommandText = """
                    INSERT INTO query_parameter (query_idx, parameter_name, delphi_type, csharp_type, direction, parameter_order)
                    VALUES (@query_idx, @parameter_name, @delphi_type, @csharp_type, @direction, @parameter_order)
                    """;
                    paramCmd.Parameters.AddWithValue("query_idx", queryIdx);
                    paramCmd.Parameters.AddWithValue("parameter_name", param.Name);
                    paramCmd.Parameters.AddWithValue("delphi_type", param.DelphiType);
                    paramCmd.Parameters.AddWithValue("csharp_type", param.CSharpType);
                    paramCmd.Parameters.AddWithValue("direction", param.Direction.ToString());
                    paramCmd.Parameters.AddWithValue("parameter_order", i);

                    await paramCmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            if (operation.FieldAccesses.Count > 0)
            {
                foreach (var field in operation.FieldAccesses)
                {
                    await using var fieldCmd = connection.CreateCommand();
                    fieldCmd.Transaction = transaction;

                    fieldCmd.CommandText = """
                    INSERT INTO query_field_access (query_idx, field_name, delphi_accessor, delphi_type, csharp_type, is_nullable)
                    VALUES (@query_idx, @field_name, @delphi_accessor, @delphi_type, @csharp_type, @is_nullable)
                    ON CONFLICT (query_idx, field_name) DO UPDATE SET
                        delphi_accessor = EXCLUDED.delphi_accessor,
                        delphi_type = EXCLUDED.delphi_type,
                        csharp_type = EXCLUDED.csharp_type,
                        is_nullable = EXCLUDED.is_nullable
                    """;
                    fieldCmd.Parameters.AddWithValue("query_idx", queryIdx);
                    fieldCmd.Parameters.AddWithValue("field_name", field.FieldName);
                    fieldCmd.Parameters.AddWithValue("delphi_accessor", field.DelphiAccessor);
                    fieldCmd.Parameters.AddWithValue("delphi_type", field.DelphiType);
                    fieldCmd.Parameters.AddWithValue("csharp_type", field.CSharpType);
                    fieldCmd.Parameters.AddWithValue("is_nullable", field.IsNullable);

                    await fieldCmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            // Commit the transaction
            await transaction.CommitAsync(cancellationToken);

            return queryIdx;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Saves query parameters.
    /// </summary>
    public async Task SaveQueryParametersAsync(int queryIdx, List<SqlParameter> parameters, CancellationToken cancellationToken = default)
    {
        if (parameters.Count == 0) return;

        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);

        for (int i = 0; i < parameters.Count; i++)
        {
            var param = parameters[i];
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO query_parameter (query_idx, parameter_name, delphi_type, csharp_type, direction, parameter_order)
                VALUES (@query_idx, @parameter_name, @delphi_type, @csharp_type, @direction, @parameter_order)
                """;
            cmd.Parameters.AddWithValue("query_idx", queryIdx);
            cmd.Parameters.AddWithValue("parameter_name", param.Name);
            cmd.Parameters.AddWithValue("delphi_type", param.DelphiType);
            cmd.Parameters.AddWithValue("csharp_type", param.CSharpType);
            cmd.Parameters.AddWithValue("direction", param.Direction.ToString());
            cmd.Parameters.AddWithValue("parameter_order", i);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Saves query field accesses with type information.
    /// </summary>
    public async Task SaveQueryFieldAccessesAsync(int queryIdx, List<FieldAccess> fieldAccesses, CancellationToken cancellationToken = default)
    {
        if (fieldAccesses.Count == 0) return;

        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);

        foreach (var field in fieldAccesses)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO query_field_access (query_idx, field_name, delphi_accessor, delphi_type, csharp_type, is_nullable)
                VALUES (@query_idx, @field_name, @delphi_accessor, @delphi_type, @csharp_type, @is_nullable)
                ON CONFLICT (query_idx, field_name) DO UPDATE SET
                    delphi_accessor = EXCLUDED.delphi_accessor,
                    delphi_type = EXCLUDED.delphi_type,
                    csharp_type = EXCLUDED.csharp_type,
                    is_nullable = EXCLUDED.is_nullable
                """;
            cmd.Parameters.AddWithValue("query_idx", queryIdx);
            cmd.Parameters.AddWithValue("field_name", field.FieldName);
            cmd.Parameters.AddWithValue("delphi_accessor", field.DelphiAccessor);
            cmd.Parameters.AddWithValue("delphi_type", field.DelphiType);
            cmd.Parameters.AddWithValue("csharp_type", field.CSharpType);
            cmd.Parameters.AddWithValue("is_nullable", field.IsNullable);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    #endregion

    #region Form Operations

    /// <summary>
    /// Saves a form.
    /// </summary>
    public async Task<int> SaveFormAsync(int projectIdx, int? unitIdx, DelphiForm form, CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            INSERT INTO form (project_idx, unit_idx, form_name, file_path, relative_path, parent_class, file_size_bytes)
            VALUES (@project_idx, @unit_idx, @form_name, @file_path, @relative_path, @parent_class, @file_size_bytes)
            ON CONFLICT DO NOTHING
            RETURNING idx
            """;
        cmd.Parameters.AddWithValue("project_idx", projectIdx);
        cmd.Parameters.AddWithValue("unit_idx", (object?)unitIdx ?? DBNull.Value);
        cmd.Parameters.AddWithValue("form_name", form.FormName);
        cmd.Parameters.AddWithValue("file_path", form.FilePath);
        cmd.Parameters.AddWithValue("relative_path", form.RelativePath);
        cmd.Parameters.AddWithValue("parent_class", (object?)form.ParentClass ?? DBNull.Value);
        cmd.Parameters.AddWithValue("file_size_bytes", form.FileSizeBytes);

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result != null ? Convert.ToInt32(result) : 0;
    }

    /// <summary>
    /// Saves form components recursively.
    /// </summary>
    public async Task SaveFormComponentsAsync(
        int formIdx,
        List<DelphiComponent> components,
        int? parentComponentIdx = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);

        int order = 0;
        foreach (var component in components)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO form_component (form_idx, parent_component_idx, component_name, class_name, component_order)
                VALUES (@form_idx, @parent_component_idx, @component_name, @class_name, @component_order)
                RETURNING idx
                """;
            cmd.Parameters.AddWithValue("form_idx", formIdx);
            cmd.Parameters.AddWithValue("parent_component_idx", (object?)parentComponentIdx ?? DBNull.Value);
            cmd.Parameters.AddWithValue("component_name", component.Name);
            cmd.Parameters.AddWithValue("class_name", component.ClassName);
            cmd.Parameters.AddWithValue("component_order", order++);

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            var componentIdx = Convert.ToInt32(result);

            // Save component properties
            foreach (var prop in component.Properties)
            {
                await using var propCmd = connection.CreateCommand();
                propCmd.CommandText = """
                    INSERT INTO component_property (component_idx, property_name, property_value)
                    VALUES (@component_idx, @property_name, @property_value)
                    """;
                propCmd.Parameters.AddWithValue("component_idx", componentIdx);
                propCmd.Parameters.AddWithValue("property_name", prop.Key);
                propCmd.Parameters.AddWithValue("property_value", prop.Value);
                await propCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Recursively save children
            if (component.Children.Count > 0)
            {
                await SaveFormComponentsAsync(formIdx, component.Children, componentIdx, cancellationToken);
            }
        }
    }

    #endregion

    #region High-Level Save Operations

    /// <summary>
    /// Saves a complete Delphi project with all its data to the database.
    /// </summary>
    public async Task<(int DirectoryIdx, int ProjectIdx)> SaveProjectAsync(
        string directoryPath,
        DelphiProject project,
        List<(DelphiUnit Unit, List<DatabaseOperation> Operations, List<string> QueryComponentTypes, string? SourceCode)> unitOperations,
        string? purpose = null,
        string? businessDomain = null,
        List<string>? keyFeatures = null,
        List<string>? keyEntities = null,
        string? technicalSummary = null,
        int? complexityScore = null,
        string? delphiVersion = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Create or get directory
            var directoryName = Path.GetFileName(directoryPath) ?? directoryPath;
            var directoryIdx = await GetOrCreateDirectoryAsync(directoryPath, directoryName, cancellationToken);

            // Create or get project with AI description
            var projectIdx = await GetOrCreateProjectAsync(
                directoryIdx,
                project.Name,
                project.RootPath,
                project.DprFilePath,
                project.DprojFilePath,
                project.FrameworkType,
                purpose,
                businessDomain,
                keyFeatures,
                keyEntities,
                technicalSummary,
                complexityScore,
                delphiVersion,
                cancellationToken);

            // Backup existing source code before clearing (preserve source_code and dfm_source columns)
            var sourceBackup = await BackupUnitSourceCodeAsync(projectIdx, cancellationToken);

            // Clear existing project data for fresh scan
            await ClearProjectDataAsync(projectIdx, cancellationToken);

            // Save search paths
            await SaveProjectSearchPathsAsync(projectIdx, project.SearchPaths, cancellationToken);

            // Save compiler defines
            await SaveProjectCompilerDefinesAsync(projectIdx, project.CompilerDefines, cancellationToken);

            // Save warnings
            await SaveProjectWarningsAsync(projectIdx, project.Warnings, cancellationToken);

            // Save units with their queries
            var unitIdxMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var classIdxMap = new Dictionary<(int UnitIdx, string ClassName), int>();
            var methodIdxMap = new Dictionary<(int UnitIdx, string? ClassName, string MethodName), int>();

            foreach (var (unit, operations, queryComponentTypes, passedSourceCode) in unitOperations)
            {
                var unitIdx = await GetOrCreateUnitAsync(projectIdx, unit, cancellationToken);
                unitIdxMap[unit.UnitName] = unitIdx;

                // Save uses clauses
                await SaveUnitUsesInterfaceAsync(unitIdx, unit.UsesInterface, cancellationToken);
                await SaveUnitUsesImplementationAsync(unitIdx, unit.UsesImplementation, cancellationToken);

                // Save classes (methods will be extracted from source code)
                foreach (var delphiClass in unit.Classes)
                {
                    var classIdx = await SaveClassAsync(unitIdx, delphiClass, cancellationToken);
                    classIdxMap[(unitIdx, delphiClass.ClassName)] = classIdx;

                    // Save class interfaces
                    await SaveClassInterfacesAsync(classIdx, delphiClass.Interfaces, cancellationToken);
                }

                // Extract and save ALL methods from source code
                // Priority: 1) passed source code, 2) backed-up from database, 3) read from file
                string? sourceCode = passedSourceCode;

                if (string.IsNullOrEmpty(sourceCode))
                {
                    // Try backed-up source code from database
                    if (sourceBackup.TryGetValue(unit.UnitName, out var backup) && !string.IsNullOrEmpty(backup.SourceCode))
                    {
                        sourceCode = backup.SourceCode;
                        _logger.LogDebug("Using backed-up source code for method extraction in {UnitName} ({Length} chars)",
                            unit.UnitName, sourceCode.Length);
                    }
                    // Fall back to reading from file if available
                    else if (File.Exists(unit.FilePath))
                    {
                        try
                        {
                            sourceCode = await File.ReadAllTextAsync(unit.FilePath, cancellationToken);
                            _logger.LogDebug("Read source code from file for method extraction in {UnitName} ({Length} chars)",
                                unit.UnitName, sourceCode.Length);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Could not read source file {FilePath}: {Message}", unit.FilePath, ex.Message);
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("Using passed source code for method extraction in {UnitName} ({Length} chars)",
                        unit.UnitName, sourceCode.Length);
                }

                if (!string.IsNullOrEmpty(sourceCode))
                {
                    try
                    {
                        var extractedMethods = MethodExtractionService.ExtractAllMethods(sourceCode);
                        _logger.LogDebug("Extracted {Count} methods from {UnitName}", extractedMethods.Count, unit.UnitName);

                        foreach (var extractedMethod in extractedMethods)
                        {
                            int? classIdx = null;
                            int? methodUnitIdx = null;
                            bool isStandalone = extractedMethod.IsStandalone;

                            if (!string.IsNullOrEmpty(extractedMethod.ContainingClass))
                            {
                                // This is a class method - find the class
                                if (classIdxMap.TryGetValue((unitIdx, extractedMethod.ContainingClass), out var cid))
                                {
                                    classIdx = cid;
                                }
                                else
                                {
                                    // Class might not have been parsed from interface section - create it now
                                    var newClass = new DelphiClass { ClassName = extractedMethod.ContainingClass };
                                    var newClassIdx = await SaveClassAsync(unitIdx, newClass, cancellationToken);
                                    classIdxMap[(unitIdx, extractedMethod.ContainingClass)] = newClassIdx;
                                    classIdx = newClassIdx;
                                    _logger.LogDebug("Created class {ClassName} for method {MethodName}",
                                        extractedMethod.ContainingClass, extractedMethod.Name);
                                }
                                // Class methods also get unit_idx for easier querying
                                methodUnitIdx = unitIdx;
                            }
                            else
                            {
                                // Standalone method
                                methodUnitIdx = unitIdx;
                            }

                            var delphiMethod = extractedMethod.ToDelphiMethod();
                            var methodIdx = await SaveMethodAsync(classIdx, null, methodUnitIdx, delphiMethod, isStandalone, cancellationToken);

                            if (methodIdx > 0)
                            {
                                methodIdxMap[(unitIdx, extractedMethod.ContainingClass, extractedMethod.Name)] = methodIdx;
                            }
                        }

                        if (extractedMethods.Count > 0)
                        {
                            _logger.LogInformation("Saved {Count} methods for unit {UnitName}", extractedMethods.Count, unit.UnitName);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Method extraction failed for {UnitName}: {Message}", unit.UnitName, ex.Message);
                        // Fall back to saving methods from parsed class declarations
                        await SaveMethodsFromParsedDataAsync(unit, unitIdx, classIdxMap, methodIdxMap, cancellationToken);
                    }
                }
                else
                {
                    // No source code available - save methods from parsed class declarations only
                    _logger.LogDebug("No source code available for {UnitName}, using parsed declarations", unit.UnitName);
                    await SaveMethodsFromParsedDataAsync(unit, unitIdx, classIdxMap, methodIdxMap, cancellationToken);
                }

                // Save queries with their component types and link to methods
                for (int i = 0; i < operations.Count; i++)
                {
                    var operation = operations[i];
                    var componentType = i < queryComponentTypes.Count ? queryComponentTypes[i] : "TQuery";

                    // Try to find the class Idx if we have a containing class
                    int? classIdx = null;
                    if (!string.IsNullOrEmpty(operation.ContainingClass))
                    {
                        classIdxMap.TryGetValue((unitIdx, operation.ContainingClass), out var cid);
                        if (cid > 0) classIdx = cid;
                    }

                    // Try to find the method Idx if we have a method name
                    int? methodIdx = null;
                    if (!string.IsNullOrEmpty(operation.MethodName))
                    {
                        methodIdxMap.TryGetValue((unitIdx, operation.ContainingClass, operation.MethodName), out var mid);
                        if (mid > 0) methodIdx = mid;
                    }

                    var queryIdx = await SaveQueryAsync(unitIdx, methodIdx, classIdx, operation, componentType, cancellationToken);

                    // Save query parameters
                    await SaveQueryParametersAsync(queryIdx, operation.Parameters, cancellationToken);

                    // Save field accesses for DTO generation
                    await SaveQueryFieldAccessesAsync(queryIdx, operation.FieldAccesses, cancellationToken);
                }

                LogQueriesSaved(operations.Count, unit.UnitName);
            }

            // Save forms
            foreach (var form in project.Forms)
            {
                // Find corresponding unit
                int? unitIdx = null;
                var formUnitName = Path.GetFileNameWithoutExtension(form.FilePath);
                if (unitIdxMap.TryGetValue(formUnitName, out var uid))
                {
                    unitIdx = uid;
                }

                var formIdx = await SaveFormAsync(projectIdx, unitIdx, form, cancellationToken);
                if (formIdx > 0)
                {
                    await SaveFormComponentsAsync(formIdx, form.Components, null, cancellationToken);
                }
            }

            // Restore source code that was backed up before clearing
            await RestoreUnitSourceCodeAsync(unitIdxMap, sourceBackup, cancellationToken);

            return (directoryIdx, projectIdx);
        }
        catch (Exception ex)
        {
            LogDatabaseError(ex);
            throw;
        }
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Gets project statistics after persistence - actual counts from database.
    /// CRITICAL FIX: This ensures accurate statistics reporting after all data is persisted.
    /// </summary>
    public async Task<ProjectStatistics> GetProjectStatisticsAsync(int projectIdx, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var conn = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = """
                SELECT 
                    COUNT(DISTINCT u.unit_idx) as units,
                    COUNT(DISTINCT CASE WHEN u.is_form = true THEN u.unit_idx END) as forms,
                    COUNT(DISTINCT CASE WHEN u.source_code IS NOT NULL AND LENGTH(u.source_code) > 0 
                          THEN u.unit_idx END) as source_files_loaded,
                    COUNT(DISTINCT CASE WHEN EXISTS (
                        SELECT 1 FROM delphi_methods m WHERE m.unit_idx = u.unit_idx
                    ) THEN u.unit_idx END) as units_processed,
                    (SELECT COUNT(*) FROM delphi_sql_queries WHERE project_idx = @projectIdx) as queries_found
                FROM delphi_units u
                WHERE u.project_idx = @projectIdx
                """;

            cmd.Parameters.AddWithValue("projectIdx", projectIdx);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new ProjectStatistics
                {
                    Units = reader.GetInt32(0),
                    Forms = reader.GetInt32(1),
                    SourceFilesLoaded = reader.GetInt32(2),
                    UnitsProcessed = reader.GetInt32(3),
                    QueriesFound = reader.GetInt32(4)
                };
            }

            return new ProjectStatistics();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project statistics for project_idx {ProjectIdx}", projectIdx);
            return new ProjectStatistics();
        }
    }


    /// <summary>
    /// Gets query summary by project.
    /// </summary>
    public async Task<List<QuerySummary>> GetQuerySummaryByProjectAsync(int projectIdx, CancellationToken cancellationToken = default)
    {
        var summaries = new List<QuerySummary>();

        await using var connection = await DataSourceInternal.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            SELECT 
                u.unit_name,
                u.relative_path,
                u.associated_dfm_file,
                q.query_component_type,
                q.sql_text,
                q.operation_type,
                q.table_name,
                q.method_name,
                q.containing_class
            FROM query q
            JOIN unit u ON u.idx = q.unit_idx
            WHERE u.project_idx = @project_idx
            ORDER BY u.unit_name, q.idx
            """;
        cmd.Parameters.AddWithValue("project_idx", projectIdx);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            summaries.Add(new QuerySummary
            {
                UnitName = reader.GetString(0),
                RelativePath = reader.GetString(1),
                AssociatedDfmFile = reader.IsDBNull(2) ? null : reader.GetString(2),
                QueryComponentType = reader.GetString(3),
                SqlText = reader.IsDBNull(4) ? null : reader.GetString(4),
                OperationType = reader.GetString(5),
                TableName = reader.IsDBNull(6) ? null : reader.GetString(6),
                MethodName = reader.IsDBNull(7) ? null : reader.GetString(7),
                ContainingClass = reader.IsDBNull(8) ? null : reader.GetString(8)
            });
        }

        return summaries;
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _dataSource?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Summary of a query for reporting.
/// </summary>
public class QuerySummary
{
    public string UnitName { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string? AssociatedDfmFile { get; set; }
    public string QueryComponentType { get; set; } = string.Empty;
    public string? SqlText { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string? TableName { get; set; }
    public string? MethodName { get; set; }
    public string? ContainingClass { get; set; }
}

/// <summary>
/// Extension methods for AnalysisRepository to support code generation.
/// </summary>
public static class AnalysisRepositoryExtensions
{
    /// <summary>
    /// Gets a project by its index.
    /// </summary>
    public static async Task<ProjectInfo?> GetProjectByIdxAsync(
        this AnalysisRepository repository,
        int projectIdx,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await repository.DataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = """
            SELECT idx, name, root_path, dpr_file_path, dproj_file_path, framework_type
            FROM project
            WHERE idx = @idx
            """;
        cmd.Parameters.AddWithValue("idx", projectIdx);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return new ProjectInfo
            {
                Idx = reader.GetInt32(0),
                Name = reader.GetString(1),
                RootPath = reader.IsDBNull(2) ? null : reader.GetString(2),
                DprFilePath = reader.IsDBNull(3) ? null : reader.GetString(3),
                DprojFilePath = reader.IsDBNull(4) ? null : reader.GetString(4),
                FrameworkType = reader.IsDBNull(5) ? null : reader.GetString(5)
            };
        }

        return null;
    }

    /// <summary>
    /// Gets all database operations for a project with their field accesses.
    /// </summary>
    public static async Task<List<DatabaseOperation>> GetProjectDatabaseOperationsAsync(
        this AnalysisRepository repository,
        int projectIdx,
        CancellationToken cancellationToken = default)
    {
        var operations = new List<DatabaseOperation>();

        await using var connection = await repository.DataSource.OpenConnectionAsync(cancellationToken);

        // Get all queries for this project
        await using var queryCmd = connection.CreateCommand();
        queryCmd.CommandText = """
            SELECT q.idx, q.method_name, q.sql_text, q.operation_type, q.table_name,
                   q.is_part_of_transaction, q.transaction_group_id, q.original_delphi_code,
                   u.unit_name, c.class_name
            FROM query q
            JOIN unit u ON u.idx = q.unit_idx
            LEFT JOIN class c ON c.idx = q.class_idx
            WHERE u.project_idx = @project_idx
            ORDER BY u.unit_name, q.method_name
            """;
        queryCmd.Parameters.AddWithValue("project_idx", projectIdx);

        var queryResults = new List<(int Idx, DatabaseOperation Op)>();

        await using (var reader = await queryCmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var queryIdx = reader.GetInt32(0);
                var op = new DatabaseOperation
                {
                    MethodName = reader.IsDBNull(1) ? "" : reader.GetString(1),
                    SqlStatement = reader.IsDBNull(2) ? null : reader.GetString(2),
                    OperationType = ParseOperationType(reader.IsDBNull(3) ? "Unknown" : reader.GetString(3)),
                    TableName = reader.IsDBNull(4) ? null : reader.GetString(4),
                    IsPartOfTransaction = !reader.IsDBNull(5) && reader.GetBoolean(5),
                    TransactionGroupId = reader.IsDBNull(6) ? null : reader.GetString(6),
                    OriginalDelphiCode = reader.IsDBNull(7) ? "" : reader.GetString(7),
                    UnitName = reader.GetString(8),
                    ContainingClass = reader.IsDBNull(9) ? "" : reader.GetString(9)
                };
                queryResults.Add((queryIdx, op));
            }
        }

        // Get field accesses for each query
        foreach (var (queryIdx, op) in queryResults)
        {
            await using var fieldCmd = connection.CreateCommand();
            fieldCmd.CommandText = """
                SELECT field_name, 
                       COALESCE(delphi_accessor, '') as delphi_accessor, 
                       COALESCE(delphi_type, 'Variant') as delphi_type, 
                       COALESCE(csharp_type, 'object') as csharp_type, 
                       COALESCE(is_nullable, false) as is_nullable
                FROM query_field_access
                WHERE query_idx = @query_idx
                """;
            fieldCmd.Parameters.AddWithValue("query_idx", queryIdx);

            await using var fieldReader = await fieldCmd.ExecuteReaderAsync(cancellationToken);
            while (await fieldReader.ReadAsync(cancellationToken))
            {
                op.FieldAccesses.Add(new FieldAccess
                {
                    FieldName = fieldReader.GetString(0),
                    DelphiAccessor = fieldReader.GetString(1),
                    DelphiType = fieldReader.GetString(2),
                    CSharpType = fieldReader.GetString(3),
                    IsNullable = fieldReader.GetBoolean(4)
                });
            }

            // Get parameters for each query
            await using var paramCmd = connection.CreateCommand();
            paramCmd.CommandText = """
                SELECT name, delphi_type, csharp_type, direction
                FROM query_parameter
                WHERE query_idx = @query_idx
                """;
            paramCmd.Parameters.AddWithValue("query_idx", queryIdx);

            await using var paramReader = await paramCmd.ExecuteReaderAsync(cancellationToken);
            while (await paramReader.ReadAsync(cancellationToken))
            {
                op.Parameters.Add(new SqlParameter
                {
                    Name = paramReader.GetString(0),
                    DelphiType = paramReader.IsDBNull(1) ? "" : paramReader.GetString(1),
                    CSharpType = paramReader.IsDBNull(2) ? "object" : paramReader.GetString(2),
                    Direction = ParseParameterDirection(paramReader.IsDBNull(3) ? "Input" : paramReader.GetString(3))
                });
            }

            operations.Add(op);
        }

        return operations;
    }

    private static DatabaseOperationType ParseOperationType(string value)
    {
        return Enum.TryParse<DatabaseOperationType>(value, true, out var result)
            ? result
            : DatabaseOperationType.Unknown;
    }

    private static ParameterDirection ParseParameterDirection(string value)
    {
        return Enum.TryParse<ParameterDirection>(value, true, out var result)
            ? result
            : ParameterDirection.Input;
    }
}