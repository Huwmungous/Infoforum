using System.Text;
using System.Text.RegularExpressions;
using DelphiAnalysisMcpServer.Models;
using Npgsql;

namespace DelphiAnalysisMcpServer.Services;

/// <summary>
/// Service for generating C# data access layer code from analysed Delphi database operations.
/// Generates repositories, controllers, and DTOs using raw ADO.NET patterns.
/// </summary>
public partial class CodeGenerationService(ILogger<CodeGenerationService> logger)
{
    private readonly ILogger<CodeGenerationService> _logger = logger;

    #region LoggerMessage Definitions

    [LoggerMessage(Level = LogLevel.Information, Message = "Created directory: {Directory}")]
    private partial void LogDirectoryCreated(string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated: {FilePath}")]
    private partial void LogFileGenerated(string filePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Code generation complete. Created {Count} files in {Directory}")]
    private partial void LogCodeGenerationComplete(int count, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Starting code generation for project {Project} with {Count} operations")]
    private partial void LogCodeGenerationStarted(string project, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated project file: {Path}")]
    private partial void LogProjectFileGenerated(string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generating code for project {ProjectName} (idx: {ProjectIdx}) with {Count} operations")]
    private partial void LogGeneratingForProject(string projectName, int projectIdx, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved generated code to project {ProjectIdx}: {DtoCount} DTOs, {RepoCount} repositories, {CtrlCount} controllers")]
    private partial void LogSavedGeneratedCode(int projectIdx, int dtoCount, int repoCount, int ctrlCount);

    #endregion

    #region GeneratedRegex Patterns

    [GeneratedRegex(@"^(Get|Set|Load|Save|Find|Fetch|Read|Write|Create|Update|Delete|Insert|Select)")]
    private static partial Regex MethodNameCleanupRegex();

    [GeneratedRegex(@"([a-z])([A-Z])")]
    private static partial Regex KebabCaseRegex();

    #endregion

    #region Main Generation Methods

    /// <summary>
    /// Generates a complete API specification from database operations.
    /// This is the main entry point for code generation.
    /// </summary>
    public ApiSpecification GenerateApiSpecification(
        string projectName,
        string baseNamespace,
        List<DatabaseOperation> operations)
    {
        var spec = new ApiSpecification
        {
            ProjectName = projectName,
            BaseNamespace = baseNamespace
        };

        // Group operations by table/entity
        var operationsByTable = GroupOperationsByTable(operations);

        // Generate DTOs for each table
        foreach (var (tableName, tableOps) in operationsByTable)
        {
            var dto = GenerateDtoDefinition(tableName, tableOps);
            if (dto != null)
            {
                spec.Dtos.Add(dto);
            }
        }

        // Generate repositories for each table
        foreach (var (tableName, tableOps) in operationsByTable)
        {
            var repository = GenerateRepositoryDefinition(tableName, tableOps, spec.Dtos);
            spec.Repositories.Add(repository);
        }

        // Generate controllers for each repository
        foreach (var repository in spec.Repositories)
        {
            var controller = GenerateControllerDefinition(repository);
            spec.Controllers.Add(controller);
        }

        return spec;
    }

    /// <summary>
    /// Generates complete C# code files from an API specification.
    /// </summary>
    /// <param name="spec">The API specification to generate code from.</param>
    /// <returns>Dictionary of relative paths to code content.</returns>
    public static Dictionary<string, string> GenerateCodeFiles(ApiSpecification spec)
    {
        var files = new Dictionary<string, string>();

        // Generate DTOs
        foreach (var dto in spec.Dtos)
        {
            var code = GenerateDtoCode(dto, spec.BaseNamespace);
            files[$"Models/{dto.Name}.cs"] = code;
        }

        // Generate repository interfaces and implementations
        foreach (var repo in spec.Repositories)
        {
            var interfaceCode = GenerateRepositoryInterfaceCode(repo, spec.BaseNamespace);
            files[$"Repositories/{repo.InterfaceName}.cs"] = interfaceCode;

            var implCode = GenerateRepositoryImplementationCode(repo, spec.BaseNamespace, spec.Dtos);
            files[$"Repositories/{repo.Name}.cs"] = implCode;
        }

        // Generate controllers
        foreach (var controller in spec.Controllers)
        {
            var code = GenerateControllerCode(controller, spec.BaseNamespace);
            files[$"Controllers/{controller.Name}.cs"] = code;
        }

        // Generate service registration extension
        var registrationCode = GenerateServiceRegistrationCode(spec);
        files["Extensions/ServiceCollectionExtensions.cs"] = registrationCode;

        return files;
    }

    /// <summary>
    /// Generates C# code files and saves them to the specified output directory.
    /// </summary>
    /// <param name="spec">The API specification to generate code from.</param>
    /// <param name="outputDirectory">The directory to save generated files to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of files that were created.</returns>
    public async Task<List<string>> GenerateAndSaveCodeFilesAsync(
        ApiSpecification spec,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var files = GenerateCodeFiles(spec);
        var createdFiles = new List<string>();

        foreach (var (relativePath, content) in files)
        {
            var fullPath = Path.Combine(outputDirectory, relativePath);
            var directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                LogDirectoryCreated(directory);
            }

            await File.WriteAllTextAsync(fullPath, content, cancellationToken);
            createdFiles.Add(fullPath);
            LogFileGenerated(fullPath);
        }

        LogCodeGenerationComplete(createdFiles.Count, outputDirectory);

        return createdFiles;
    }

    /// <summary>
    /// Generates a complete C# project from database operations.
    /// This is the main entry point for full project generation.
    /// </summary>
    /// <param name="projectName">Name of the project to generate.</param>
    /// <param name="baseNamespace">Base namespace for generated code.</param>
    /// <param name="operations">Database operations extracted from Delphi code.</param>
    /// <param name="outputDirectory">Directory to save generated files.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Summary of generated files.</returns>
    public async Task<CodeGenerationResult> GenerateProjectAsync(
        string projectName,
        string baseNamespace,
        List<DatabaseOperation> operations,
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        LogCodeGenerationStarted(projectName, operations.Count);

        // Generate the API specification
        var spec = GenerateApiSpecification(projectName, baseNamespace, operations);

        // Generate and save files
        var createdFiles = await GenerateAndSaveCodeFilesAsync(spec, outputDirectory, cancellationToken);

        // Generate the .csproj file
        var csprojPath = await GenerateProjectFileAsync(spec, outputDirectory, cancellationToken);
        createdFiles.Add(csprojPath);

        return new CodeGenerationResult
        {
            ProjectName = projectName,
            OutputDirectory = outputDirectory,
            GeneratedFiles = createdFiles,
            DtoCount = spec.Dtos.Count,
            RepositoryCount = spec.Repositories.Count,
            ControllerCount = spec.Controllers.Count,
            TotalMethodCount = spec.Repositories.Sum(r => r.Methods.Count)
        };
    }

    /// <summary>
    /// Generates a .csproj file for the API project.
    /// </summary>
    private async Task<string> GenerateProjectFileAsync(
        ApiSpecification spec,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var csproj = $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">

              <PropertyGroup>
                <TargetFramework>net9.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <RootNamespace>{spec.BaseNamespace}</RootNamespace>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Npgsql" Version="9.0.2" />
                <PackageReference Include="Swashbuckle.AspNetCore" Version="7.2.0" />
              </ItemGroup>

            </Project>
            """;

        var csprojPath = Path.Combine(outputDirectory, $"{spec.ProjectName}.Api.csproj");
        await File.WriteAllTextAsync(csprojPath, csproj, cancellationToken);
        LogProjectFileGenerated(csprojPath);

        return csprojPath;
    }

    #endregion

    #region Database Storage

    /// <summary>
    /// Generates code and saves it to the project record in the database.
    /// </summary>
    /// <param name="projectIdx">The project database index.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="baseNamespace">Base namespace for generated code.</param>
    /// <param name="operations">Database operations extracted from the project.</param>
    /// <param name="dataSource">NpgsqlDataSource for database access.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Summary of generated code.</returns>
    public async Task<CodeGenerationResult> GenerateAndSaveToProjectAsync(
        int projectIdx,
        string projectName,
        string baseNamespace,
        List<DatabaseOperation> operations,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken = default)
    {
        LogGeneratingForProject(projectName, projectIdx, operations.Count);

        // Generate the API specification
        var spec = GenerateApiSpecification(projectName, baseNamespace, operations);

        // Generate code files as strings
        var files = GenerateCodeFiles(spec);

        // Combine files by category
        var dtoCode = CombineFilesByPrefix(files, "Models/");
        var dataAccessCode = CombineFilesByPrefix(files, "Repositories/");
        var controllerCode = CombineFilesByPrefix(files, "Controllers/");

        // Add extensions to data access code
        if (files.TryGetValue("Extensions/ServiceCollectionExtensions.cs", out var extensionsCode))
        {
            dataAccessCode += "\n\n// === ServiceCollectionExtensions.cs ===\n\n" + extensionsCode;
        }

        // Save to database
        await SaveGeneratedCodeAsync(
            dataSource,
            projectIdx,
            dtoCode,
            dataAccessCode,
            controllerCode,
            new GenerationConfig
            {
                BaseNamespace = baseNamespace,
                GeneratedAt = DateTime.UtcNow,
                OperationCount = operations.Count,
                DtoCount = spec.Dtos.Count,
                RepositoryCount = spec.Repositories.Count,
                ControllerCount = spec.Controllers.Count
            },
            cancellationToken);

        LogSavedGeneratedCode(projectIdx, spec.Dtos.Count, spec.Repositories.Count, spec.Controllers.Count);

        return new CodeGenerationResult
        {
            ProjectName = projectName,
            OutputDirectory = $"database:project/{projectIdx}",
            GeneratedFiles = [.. files.Keys],
            DtoCount = spec.Dtos.Count,
            RepositoryCount = spec.Repositories.Count,
            ControllerCount = spec.Controllers.Count,
            TotalMethodCount = spec.Repositories.Sum(r => r.Methods.Count)
        };
    }

    /// <summary>
    /// Combines multiple files with a given prefix into a single string.
    /// </summary>
    private static string CombineFilesByPrefix(Dictionary<string, string> files, string prefix)
    {
        var matchingFiles = files
            .Where(f => f.Key.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(f => f.Key)
            .ToList();

        if (matchingFiles.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var (path, content) in matchingFiles)
        {
            var fileName = Path.GetFileName(path);
            sb.AppendLine($"// === {fileName} ===");
            sb.AppendLine();
            sb.AppendLine(content);
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Saves generated code to the project table.
    /// </summary>
    private static async Task SaveGeneratedCodeAsync(
        NpgsqlDataSource dataSource,
        int projectIdx,
        string dtoCode,
        string dataAccessCode,
        string controllerCode,
        GenerationConfig config,
        CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = connection.CreateCommand();

        cmd.CommandText = "SELECT fn_save_generated_code(@project_idx, @dto_code, @data_access_code, @controller_code, @config)";
        cmd.Parameters.AddWithValue("project_idx", projectIdx);
        cmd.Parameters.AddWithValue("dto_code", dtoCode);
        cmd.Parameters.AddWithValue("data_access_code", dataAccessCode);
        cmd.Parameters.AddWithValue("controller_code", controllerCode);
        cmd.Parameters.AddWithValue("config", NpgsqlTypes.NpgsqlDbType.Jsonb,
            System.Text.Json.JsonSerializer.Serialize(config));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    #endregion

    #region Grouping and Analysis

    /// <summary>
    /// Groups database operations by their target table.
    /// </summary>
    private static Dictionary<string, List<DatabaseOperation>> GroupOperationsByTable(List<DatabaseOperation> operations)
    {
        var groups = new Dictionary<string, List<DatabaseOperation>>(StringComparer.OrdinalIgnoreCase);

        foreach (var op in operations)
        {
            var tableName = op.TableName ?? "Unknown";

            if (!groups.TryGetValue(tableName, out var list))
            {
                list = [];
                groups[tableName] = list;
            }
            list.Add(op);
        }

        return groups;
    }

    #endregion

    #region DTO Generation

    /// <summary>
    /// Generates a DTO definition from database operations targeting a table.
    /// </summary>
    private static DtoDefinition? GenerateDtoDefinition(string tableName, List<DatabaseOperation> operations)
    {
        if (tableName == "Unknown")
            return null;

        var dto = new DtoDefinition
        {
            Name = FieldAccessAnalyser.TableNameToDtoName(tableName),
            Description = $"Data transfer object for {tableName} table.",
            SourceTable = tableName,
            UseRecord = true,
            SourceUnits = [.. operations.Select(o => o.UnitName).Distinct()]
        };

        // Collect all field accesses from all operations on this table
        var allFieldAccesses = new Dictionary<string, FieldAccess>(StringComparer.OrdinalIgnoreCase);

        foreach (var op in operations)
        {
            foreach (var field in op.FieldAccesses)
            {
                if (!allFieldAccesses.TryGetValue(field.FieldName, out var existing))
                {
                    allFieldAccesses[field.FieldName] = field;
                }
                else
                {
                    // If we have conflicting nullable info, prefer nullable
                    if (field.IsNullable)
                    {
                        existing.IsNullable = true;
                    }
                }
            }
        }

        // Convert field accesses to DTO properties
        foreach (var (fieldName, field) in allFieldAccesses)
        {
            dto.Properties.Add(new DtoProperty
            {
                Name = FieldAccessAnalyser.ColumnNameToPropertyName(fieldName),
                ColumnName = fieldName,
                Type = field.CSharpType,
                IsNullable = field.IsNullable
            });
        }

        // If no fields were found, don't generate a DTO
        if (dto.Properties.Count == 0)
            return null;

        return dto;
    }

    /// <summary>
    /// Generates C# code for a DTO.
    /// </summary>
    private static string GenerateDtoCode(DtoDefinition dto, string baseNamespace)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {baseNamespace}.Models;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {dto.Description}");
        if (!string.IsNullOrEmpty(dto.SourceTable))
        {
            sb.AppendLine($"/// Source table: {dto.SourceTable}");
        }
        sb.AppendLine("/// </summary>");

        if (dto.UseRecord)
        {
            // Generate as a record
            sb.Append($"public record {dto.Name}(");

            var props = dto.Properties.Select(p =>
            {
                var type = p.IsNullable && !p.Type.EndsWith('?') && p.Type != "string"
                    ? $"{p.Type}?"
                    : p.Type;
                return $"{type} {p.Name}";
            });

            sb.Append(string.Join(", ", props));
            sb.AppendLine(");");
        }
        else
        {
            // Generate as a class
            sb.AppendLine($"public class {dto.Name}");
            sb.AppendLine("{");

            foreach (var prop in dto.Properties)
            {
                var type = prop.IsNullable && !prop.Type.EndsWith('?') && prop.Type != "string"
                    ? $"{prop.Type}?"
                    : prop.Type;

                if (!string.IsNullOrEmpty(prop.Description))
                {
                    sb.AppendLine($"    /// <summary>{prop.Description}</summary>");
                }
                sb.AppendLine($"    public {type} {prop.Name} {{ get; set; }}");
            }

            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    #endregion

    #region Repository Generation

    /// <summary>
    /// Generates a repository definition from database operations.
    /// </summary>
    private static RepositoryDefinition GenerateRepositoryDefinition(
        string tableName,
        List<DatabaseOperation> operations,
        List<DtoDefinition> dtos)
    {
        var entityName = FieldAccessAnalyser.ColumnNameToPropertyName(
            FieldAccessAnalyser.Singularise(tableName));

        var repo = new RepositoryDefinition
        {
            Name = $"{entityName}Repository",
            Description = $"Repository for {tableName} database operations.",
            SourceUnits = [.. operations.Select(o => o.UnitName).Distinct()],
            RequiredUsings =
            [
                "System.Data",
                "Npgsql"
            ]
        };

        var dto = dtos.FirstOrDefault(d => d.SourceTable == tableName);
        var dtoName = dto?.Name ?? "object";

        // Group operations by type and generate methods
        var selectOps = operations.Where(o => o.OperationType == DatabaseOperationType.Select).ToList();
        var insertOps = operations.Where(o => o.OperationType == DatabaseOperationType.Insert).ToList();
        var updateOps = operations.Where(o => o.OperationType == DatabaseOperationType.Update).ToList();
        var deleteOps = operations.Where(o => o.OperationType == DatabaseOperationType.Delete).ToList();

        // Generate SELECT methods
        foreach (var op in selectOps)
        {
            var method = GenerateSelectMethod(op, dtoName);
            if (method != null)
            {
                repo.Methods.Add(method);
            }
        }

        // Generate INSERT methods
        foreach (var op in insertOps)
        {
            var method = GenerateInsertMethod(op, dtoName);
            if (method != null)
            {
                repo.Methods.Add(method);
            }
        }

        // Generate UPDATE methods
        foreach (var op in updateOps)
        {
            var method = GenerateUpdateMethod(op, dtoName);
            if (method != null)
            {
                repo.Methods.Add(method);
            }
        }

        // Generate DELETE methods
        foreach (var op in deleteOps)
        {
            var method = GenerateDeleteMethod(op);
            if (method != null)
            {
                repo.Methods.Add(method);
            }
        }

        // Deduplicate methods with the same name
        repo.Methods = DeduplicateMethods(repo.Methods);

        return repo;
    }

    /// <summary>
    /// Generates a repository method for a SELECT operation.
    /// </summary>
    private static RepositoryMethod? GenerateSelectMethod(DatabaseOperation op, string dtoName)
    {
        if (string.IsNullOrEmpty(op.SqlStatement))
            return null;

        var methodName = GenerateMethodName(op, "Get");
        var sql = op.SqlStatement;

        // Rewrite SELECT * to use specific columns if we have field accesses
        if (op.FieldAccesses.Count > 0)
        {
            sql = FieldAccessAnalyser.RewriteSelectStar(sql, op.FieldAccesses);
        }

        // Determine if this returns a single item or collection
        var returnsCollection = !HasSingleRowIndicators(op);

        var method = new RepositoryMethod
        {
            Name = methodName,
            OriginalDelphiMethod = op.MethodName,
            SqlStatement = sql,
            OperationType = DatabaseOperationType.Select,
            ReturnDtoType = dtoName,
            ReturnsCollection = returnsCollection,
            ReturnType = returnsCollection
                ? $"Task<List<{dtoName}>>"
                : $"Task<{dtoName}?>",
            Description = $"Retrieves {(returnsCollection ? "multiple" : "a single")} {dtoName} record(s).",
            SourceUnits = [op.UnitName]
        };

        // Add parameters from the SQL
        foreach (var param in op.Parameters)
        {
            method.Parameters.Add(new MethodParameter
            {
                Name = ToCamelCase(param.Name),
                Type = param.CSharpType
            });
        }

        return method;
    }

    /// <summary>
    /// Generates a repository method for an INSERT operation.
    /// </summary>
    private static RepositoryMethod? GenerateInsertMethod(DatabaseOperation op, string dtoName)
    {
        if (string.IsNullOrEmpty(op.SqlStatement))
            return null;

        var methodName = GenerateMethodName(op, "Create");

        return new RepositoryMethod
        {
            Name = methodName,
            OriginalDelphiMethod = op.MethodName,
            SqlStatement = op.SqlStatement,
            OperationType = DatabaseOperationType.Insert,
            ReturnType = "Task<int>",
            Description = $"Creates a new {dtoName} record.",
            SourceUnits = [op.UnitName],
            Parameters = [.. op.Parameters.Select(p => new MethodParameter
            {
                Name = ToCamelCase(p.Name),
                Type = p.CSharpType
            })]
        };
    }

    /// <summary>
    /// Generates a repository method for an UPDATE operation.
    /// </summary>
    private static RepositoryMethod? GenerateUpdateMethod(DatabaseOperation op, string dtoName)
    {
        if (string.IsNullOrEmpty(op.SqlStatement))
            return null;

        var methodName = GenerateMethodName(op, "Update");

        return new RepositoryMethod
        {
            Name = methodName,
            OriginalDelphiMethod = op.MethodName,
            SqlStatement = op.SqlStatement,
            OperationType = DatabaseOperationType.Update,
            ReturnType = "Task<int>",
            Description = $"Updates an existing {dtoName} record.",
            SourceUnits = [op.UnitName],
            Parameters = [.. op.Parameters.Select(p => new MethodParameter
            {
                Name = ToCamelCase(p.Name),
                Type = p.CSharpType
            })]
        };
    }

    /// <summary>
    /// Generates a repository method for a DELETE operation.
    /// </summary>
    private static RepositoryMethod? GenerateDeleteMethod(DatabaseOperation op)
    {
        if (string.IsNullOrEmpty(op.SqlStatement))
            return null;

        var methodName = GenerateMethodName(op, "Delete");

        return new RepositoryMethod
        {
            Name = methodName,
            OriginalDelphiMethod = op.MethodName,
            SqlStatement = op.SqlStatement,
            OperationType = DatabaseOperationType.Delete,
            ReturnType = "Task<int>",
            Description = "Deletes a record.",
            SourceUnits = [op.UnitName],
            Parameters = [.. op.Parameters.Select(p => new MethodParameter
            {
                Name = ToCamelCase(p.Name),
                Type = p.CSharpType
            })]
        };
    }

    /// <summary>
    /// Generates C# code for a repository interface.
    /// </summary>
    private static string GenerateRepositoryInterfaceCode(RepositoryDefinition repo, string baseNamespace)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"namespace {baseNamespace}.Repositories;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {repo.Description}");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public interface {repo.InterfaceName}");
        sb.AppendLine("{");

        foreach (var method in repo.Methods)
        {
            sb.AppendLine($"    /// <summary>{method.Description}</summary>");
            var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
            sb.AppendLine($"    {method.ReturnType} {method.Name}Async({parameters});");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Generates C# code for a repository implementation.
    /// </summary>
    private static string GenerateRepositoryImplementationCode(
        RepositoryDefinition repo,
        string baseNamespace,
        List<DtoDefinition> dtos)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using System.Data;");
        sb.AppendLine("using Npgsql;");
        sb.AppendLine($"using {baseNamespace}.Models;");
        sb.AppendLine();
        sb.AppendLine($"namespace {baseNamespace}.Repositories;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {repo.Description}");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public class {repo.Name} : {repo.InterfaceName}");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly NpgsqlDataSource _dataSource;");
        sb.AppendLine();
        sb.AppendLine($"    public {repo.Name}(NpgsqlDataSource dataSource)");
        sb.AppendLine("    {");
        sb.AppendLine("        _dataSource = dataSource;");
        sb.AppendLine("    }");

        foreach (var method in repo.Methods)
        {
            sb.AppendLine();
            GenerateRepositoryMethodCode(sb, method, dtos);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Generates the implementation code for a single repository method.
    /// </summary>
    private static void GenerateRepositoryMethodCode(StringBuilder sb, RepositoryMethod method, List<DtoDefinition> dtos)
    {
        var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));

        sb.AppendLine($"    /// <summary>{method.Description}</summary>");
        sb.AppendLine($"    /// <remarks>Original Delphi method: {method.OriginalDelphiMethod}</remarks>");
        sb.AppendLine($"    public async {method.ReturnType} {method.Name}Async({parameters})");
        sb.AppendLine("    {");

        // SQL statement
        var escapedSql = method.SqlStatement.Replace("\"", "\\\"").Replace("\r\n", "\n").Replace("\n", "\\n\" +\n            \"");
        sb.AppendLine($"        const string sql = \"{escapedSql}\";");
        sb.AppendLine();
        sb.AppendLine("        await using var connection = await _dataSource.OpenConnectionAsync();");
        sb.AppendLine("        await using var cmd = connection.CreateCommand();");
        sb.AppendLine("        cmd.CommandText = sql;");
        sb.AppendLine();

        // Add parameters
        foreach (var param in method.Parameters)
        {
            var npgsqlParam = $"@{param.Name}";
            sb.AppendLine($"        cmd.Parameters.AddWithValue(\"{npgsqlParam}\", {param.Name});");
        }

        // Execute based on operation type
        switch (method.OperationType)
        {
            case DatabaseOperationType.Select:
                GenerateSelectExecutionCode(sb, method, dtos);
                break;
            case DatabaseOperationType.Insert:
            case DatabaseOperationType.Update:
            case DatabaseOperationType.Delete:
                sb.AppendLine();
                sb.AppendLine("        return await cmd.ExecuteNonQueryAsync();");
                break;
            default:
                sb.AppendLine();
                sb.AppendLine("        await cmd.ExecuteNonQueryAsync();");
                break;
        }

        sb.AppendLine("    }");
    }

    /// <summary>
    /// Generates SELECT execution code with result mapping.
    /// </summary>
    private static void GenerateSelectExecutionCode(StringBuilder sb, RepositoryMethod method, List<DtoDefinition> dtos)
    {
        var dto = dtos.FirstOrDefault(d => d.Name == method.ReturnDtoType);

        sb.AppendLine();
        sb.AppendLine("        await using var reader = await cmd.ExecuteReaderAsync();");

        if (method.ReturnsCollection)
        {
            sb.AppendLine($"        var results = new List<{method.ReturnDtoType}>();");
            sb.AppendLine();
            sb.AppendLine("        while (await reader.ReadAsync())");
            sb.AppendLine("        {");
            GenerateReaderMappingCode(sb, method.ReturnDtoType!, dto, "            ");
            sb.AppendLine("            results.Add(item);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        return results;");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("        if (await reader.ReadAsync())");
            sb.AppendLine("        {");
            GenerateReaderMappingCode(sb, method.ReturnDtoType!, dto, "            ");
            sb.AppendLine("            return item;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        return null;");
        }
    }

    /// <summary>
    /// Generates code to map a data reader row to a DTO.
    /// </summary>
    private static void GenerateReaderMappingCode(StringBuilder sb, string dtoName, DtoDefinition? dto, string indent)
    {
        if (dto == null || dto.Properties.Count == 0)
        {
            sb.AppendLine($"{indent}var item = new {dtoName}();");
            return;
        }

        if (dto.UseRecord)
        {
            // Record - use constructor
            sb.AppendLine($"{indent}var item = new {dtoName}(");
            for (int i = 0; i < dto.Properties.Count; i++)
            {
                var prop = dto.Properties[i];
                var comma = i < dto.Properties.Count - 1 ? "," : "";
                var readerCall = GenerateReaderCall(prop);
                sb.AppendLine($"{indent}    {readerCall}{comma}");
            }
            sb.AppendLine($"{indent});");
        }
        else
        {
            // Class - use object initialiser
            sb.AppendLine($"{indent}var item = new {dtoName}");
            sb.AppendLine($"{indent}{{");
            for (int i = 0; i < dto.Properties.Count; i++)
            {
                var prop = dto.Properties[i];
                var comma = i < dto.Properties.Count - 1 ? "," : "";
                var readerCall = GenerateReaderCall(prop);
                sb.AppendLine($"{indent}    {prop.Name} = {readerCall}{comma}");
            }
            sb.AppendLine($"{indent}}};");
        }
    }

    /// <summary>
    /// Generates a reader.GetXxx call for a property.
    /// </summary>
    private static string GenerateReaderCall(DtoProperty prop)
    {
        var ordinal = $"reader.GetOrdinal(\"{prop.ColumnName}\")";

        if (prop.IsNullable)
        {
            return prop.Type switch
            {
                "string" => $"reader.IsDBNull({ordinal}) ? null : reader.GetString({ordinal})",
                "int" => $"reader.IsDBNull({ordinal}) ? null : reader.GetInt32({ordinal})",
                "long" => $"reader.IsDBNull({ordinal}) ? null : reader.GetInt64({ordinal})",
                "short" => $"reader.IsDBNull({ordinal}) ? null : reader.GetInt16({ordinal})",
                "bool" => $"reader.IsDBNull({ordinal}) ? null : reader.GetBoolean({ordinal})",
                "double" => $"reader.IsDBNull({ordinal}) ? null : reader.GetDouble({ordinal})",
                "float" => $"reader.IsDBNull({ordinal}) ? null : reader.GetFloat({ordinal})",
                "decimal" => $"reader.IsDBNull({ordinal}) ? null : reader.GetDecimal({ordinal})",
                "DateTime" => $"reader.IsDBNull({ordinal}) ? null : reader.GetDateTime({ordinal})",
                "DateOnly" => $"reader.IsDBNull({ordinal}) ? null : DateOnly.FromDateTime(reader.GetDateTime({ordinal}))",
                "TimeOnly" => $"reader.IsDBNull({ordinal}) ? null : TimeOnly.FromTimeSpan(reader.GetTimeSpan({ordinal}))",
                "Guid" => $"reader.IsDBNull({ordinal}) ? null : reader.GetGuid({ordinal})",
                "byte[]" => $"reader.IsDBNull({ordinal}) ? null : (byte[])reader.GetValue({ordinal})",
                _ => $"reader.IsDBNull({ordinal}) ? null : reader.GetValue({ordinal})"
            };
        }

        return prop.Type switch
        {
            "string" => $"reader.GetString({ordinal})",
            "int" => $"reader.GetInt32({ordinal})",
            "long" => $"reader.GetInt64({ordinal})",
            "short" => $"reader.GetInt16({ordinal})",
            "bool" => $"reader.GetBoolean({ordinal})",
            "double" => $"reader.GetDouble({ordinal})",
            "float" => $"reader.GetFloat({ordinal})",
            "decimal" => $"reader.GetDecimal({ordinal})",
            "DateTime" => $"reader.GetDateTime({ordinal})",
            "DateOnly" => $"DateOnly.FromDateTime(reader.GetDateTime({ordinal}))",
            "TimeOnly" => $"TimeOnly.FromTimeSpan(reader.GetTimeSpan({ordinal}))",
            "Guid" => $"reader.GetGuid({ordinal})",
            "byte[]" => $"(byte[])reader.GetValue({ordinal})",
            _ => $"reader.GetValue({ordinal})"
        };
    }

    #endregion

    #region Controller Generation

    /// <summary>
    /// Generates a controller definition from a repository.
    /// </summary>
    private static ControllerDefinition GenerateControllerDefinition(RepositoryDefinition repo)
    {
        var entityName = repo.Name.Replace("Repository", "");
        var route = ToKebabCase(entityName);

        var controller = new ControllerDefinition
        {
            Name = $"{entityName}Controller",
            Route = $"api/{route}",
            Description = $"API controller for {entityName} operations.",
            RequiredRepositories = [repo.Name],
            SourceUnits = repo.SourceUnits
        };

        foreach (var method in repo.Methods)
        {
            var action = GenerateControllerAction(method);
            if (action != null)
            {
                controller.Actions.Add(action);
            }
        }

        return controller;
    }

    /// <summary>
    /// Generates a controller action from a repository method.
    /// </summary>
    private static ControllerAction? GenerateControllerAction(RepositoryMethod method)
    {
        var (httpMethod, routeSuffix) = method.OperationType switch
        {
            DatabaseOperationType.Select => method.ReturnsCollection
                ? ("GET", "")
                : ("GET", method.Parameters.Count > 0 ? "/{" + method.Parameters[0].Name + "}" : ""),
            DatabaseOperationType.Insert => ("POST", ""),
            DatabaseOperationType.Update => ("PUT", method.Parameters.Count > 0 ? "/{" + method.Parameters[0].Name + "}" : ""),
            DatabaseOperationType.Delete => ("DELETE", method.Parameters.Count > 0 ? "/{" + method.Parameters[0].Name + "}" : ""),
            _ => ("POST", "")
        };

        var actionName = method.Name.Replace("Get", "").Replace("Create", "").Replace("Update", "").Replace("Delete", "");
        if (string.IsNullOrEmpty(actionName))
        {
            actionName = method.OperationType switch
            {
                DatabaseOperationType.Select => method.ReturnsCollection ? "GetAll" : "GetById",
                DatabaseOperationType.Insert => "Create",
                DatabaseOperationType.Update => "Update",
                DatabaseOperationType.Delete => "Delete",
                _ => method.Name
            };
        }

        return new ControllerAction
        {
            Name = actionName,
            HttpMethod = httpMethod,
            Route = routeSuffix,
            ReturnType = method.ReturnType.Replace("Task<", "Task<ActionResult<").Replace(">", ">>"),
            Description = method.Description,
            RepositoryMethodCalls = [$"{method.Name}Async"],
            SourceUnits = method.SourceUnits,
            Parameters = [.. method.Parameters.Select(p => new MethodParameter
            {
                Name = p.Name,
                Type = p.Type,
                IsFromRoute = httpMethod != "POST" && httpMethod != "PUT",
                IsFromBody = httpMethod == "POST" || httpMethod == "PUT"
            })]
        };
    }

    /// <summary>
    /// Generates C# code for a controller.
    /// </summary>
    private static string GenerateControllerCode(ControllerDefinition controller, string baseNamespace)
    {
        var sb = new StringBuilder();
        var repoName = controller.RequiredRepositories.FirstOrDefault() ?? "Repository";
        var interfaceName = $"I{repoName}";
        var fieldName = $"_{ToCamelCase(repoName)}";

        sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
        sb.AppendLine($"using {baseNamespace}.Models;");
        sb.AppendLine($"using {baseNamespace}.Repositories;");
        sb.AppendLine();
        sb.AppendLine($"namespace {baseNamespace}.Controllers;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {controller.Description}");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("[ApiController]");
        sb.AppendLine($"[Route(\"{controller.Route}\")]");
        sb.AppendLine($"public class {controller.Name} : ControllerBase");
        sb.AppendLine("{");
        sb.AppendLine($"    private readonly {interfaceName} {fieldName};");
        sb.AppendLine();
        sb.AppendLine($"    public {controller.Name}({interfaceName} {ToCamelCase(repoName)})");
        sb.AppendLine("    {");
        sb.AppendLine($"        {fieldName} = {ToCamelCase(repoName)};");
        sb.AppendLine("    }");

        foreach (var action in controller.Actions)
        {
            sb.AppendLine();
            GenerateControllerActionCode(sb, action, fieldName);
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// Generates code for a single controller action.
    /// </summary>
    private static void GenerateControllerActionCode(StringBuilder sb, ControllerAction action, string repoFieldName)
    {
        var parameters = action.Parameters.Select(p =>
        {
            var attr = p.IsFromRoute ? "[FromRoute] " : p.IsFromBody ? "[FromBody] " : "";
            return $"{attr}{p.Type} {p.Name}";
        });

        sb.AppendLine($"    /// <summary>{action.Description}</summary>");
        sb.AppendLine($"    [Http{action.HttpMethod}(\"{action.Route}\")]");
        sb.AppendLine($"    public async {action.ReturnType} {action.Name}({string.Join(", ", parameters)})");
        sb.AppendLine("    {");

        var methodCall = action.RepositoryMethodCalls.FirstOrDefault() ?? "UnknownAsync";
        var args = string.Join(", ", action.Parameters.Select(p => p.Name));

        sb.AppendLine($"        var result = await {repoFieldName}.{methodCall}({args});");

        if (action.ReturnType.Contains("List<") || action.ReturnType.Contains("IEnumerable<"))
        {
            sb.AppendLine("        return Ok(result);");
        }
        else if (action.HttpMethod == "POST")
        {
            sb.AppendLine("        return CreatedAtAction(nameof(GetById), new { id = result }, result);");
        }
        else
        {
            sb.AppendLine("        if (result == null)");
            sb.AppendLine("            return NotFound();");
            sb.AppendLine("        return Ok(result);");
        }

        sb.AppendLine("    }");
    }

    #endregion

    #region Service Registration

    /// <summary>
    /// Generates the service registration extension method.
    /// </summary>
    private static string GenerateServiceRegistrationCode(ApiSpecification spec)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"using {spec.BaseNamespace}.Repositories;");
        sb.AppendLine();
        sb.AppendLine($"namespace {spec.BaseNamespace}.Extensions;");
        sb.AppendLine();
        sb.AppendLine("public static class ServiceCollectionExtensions");
        sb.AppendLine("{");
        sb.AppendLine("    public static IServiceCollection AddRepositories(this IServiceCollection services)");
        sb.AppendLine("    {");

        foreach (var repo in spec.Repositories)
        {
            sb.AppendLine($"        services.AddScoped<{repo.InterfaceName}, {repo.Name}>();");
        }

        sb.AppendLine("        return services;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates a method name from the operation context.
    /// </summary>
    private static string GenerateMethodName(DatabaseOperation op, string prefix)
    {
        var baseName = op.MethodName;

        // Clean up the name
        baseName = MethodNameCleanupRegex().Replace(baseName, "");

        if (string.IsNullOrEmpty(baseName))
        {
            baseName = op.TableName ?? "Data";
        }

        // Convert to PascalCase
        baseName = FieldAccessAnalyser.ColumnNameToPropertyName(baseName);

        return prefix + baseName;
    }

    /// <summary>
    /// Determines if an operation likely returns a single row.
    /// </summary>
    private static bool HasSingleRowIndicators(DatabaseOperation op)
    {
        if (string.IsNullOrEmpty(op.SqlStatement))
            return false;

        var sql = op.SqlStatement.ToUpperInvariant();

        // Check for single-row patterns
        if (sql.Contains("WHERE") &&
            (sql.Contains("= :") || sql.Contains("= @")) &&
            !sql.Contains("IN (") &&
            !sql.Contains("LIKE"))
        {
            // Likely a lookup by ID
            return true;
        }

        // Check method name patterns
        var methodName = op.MethodName.ToUpperInvariant();
        if (methodName.Contains("BYID") ||
            methodName.Contains("BY_ID") ||
            methodName.Contains("SINGLE") ||
            methodName.Contains("FIRST") ||
            methodName.Contains("ONE"))
        {
            return true;
        }

        // Check for LIMIT 1 or TOP 1
        if (sql.Contains("LIMIT 1") || sql.Contains("TOP 1") || sql.Contains("FIRST 1"))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Deduplicates methods with the same name by appending a suffix.
    /// </summary>
    private static List<RepositoryMethod> DeduplicateMethods(List<RepositoryMethod> methods)
    {
        var nameCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var result = new List<RepositoryMethod>();

        foreach (var method in methods)
        {
            if (nameCounts.TryGetValue(method.Name, out var count))
            {
                nameCounts[method.Name] = count + 1;
                method.Name = $"{method.Name}{count + 1}";
            }
            else
            {
                nameCounts[method.Name] = 1;
            }
            result.Add(method);
        }

        return result;
    }

    /// <summary>
    /// Converts a string to camelCase.
    /// </summary>
    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    /// <summary>
    /// Converts a string to kebab-case.
    /// </summary>
    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return KebabCaseRegex().Replace(value, "$1-$2").ToLowerInvariant();
    }

    #endregion
}
