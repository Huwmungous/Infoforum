using System.Text.Json;
using SfD.Global.Logging;

namespace CodeAnalysisMcpServer.Tools;

/// <summary>
/// Provides comprehensive code analysis tools for Delphi projects
/// Now fully ANTLR-based for accurate AST parsing
/// </summary>
public class CodeAnalysisTools
{
    private readonly AntlrDelphiSqlExtractor _sqlExtractor;
    private readonly DelphiSqlPatternExtractor _regexExtractor;
    private readonly AntlrDelphiCodeAnalyzer _codeAnalyzer;
    private readonly SfdLogger _logger;
    private const string DefaultSourcePath = @"/srv/sfddevelopment/Source/embarcadero/Embarcadero/"; // TODO: Make configurable

    public CodeAnalysisTools(
        AntlrDelphiSqlExtractor sqlExtractor,
        DelphiSqlPatternExtractor regexExtractor,
        AntlrDelphiCodeAnalyzer codeAnalyzer,
        SfdLogger logger)
    {
        _sqlExtractor = sqlExtractor;
        _regexExtractor = regexExtractor;
        _codeAnalyzer = codeAnalyzer;
        _logger = logger;
    }

    #region Project Analysis

    public Task<object> ParseGroupProjectsAsync()
    {
        _logger.LogInformation("Parsing group projects");

        var groupProjects = Directory.GetFiles(DefaultSourcePath, "*.groupproj", SearchOption.AllDirectories);

        var result = new
        {
            totalGroups = groupProjects.Length,
            groups = groupProjects.Select(gp => new
            {
                path = gp,
                name = Path.GetFileNameWithoutExtension(gp),
                directory = Path.GetDirectoryName(gp)
            }).ToList()
        };

        return Task.FromResult<object>(result);
    }

    public Task<object> ParseDelphiFileAsync(string path)
    {
        _logger.LogInformation("Parsing Delphi file: {Path}", path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"File not found: {path}");
        }

        var content = File.ReadAllText(path);
        var lines = content.Split('\n');

        var result = new
        {
            path,
            fileName = Path.GetFileName(path),
            fileType = Path.GetExtension(path),
            lineCount = lines.Length,
            sizeBytes = new FileInfo(path).Length,
            lastModified = File.GetLastWriteTime(path)
        };

        return Task.FromResult<object>(result);
    }

    #endregion

    #region SQL Extraction

    public Task<object> ExtractSqlStatementsAsync(string path)
    {
        _logger.LogInformation("Extracting SQL statements from {Path} using REGEX (proven working)", path);

        List<DelphiSqlPatternExtractor.ExtractedSql> extracted;

        if (File.Exists(path))
        {
            extracted = _regexExtractor.ExtractFromFile(path);
        }
        else if (Directory.Exists(path))
        {
            extracted = _regexExtractor.ExtractFromDirectory(path, "*.pas");
        }
        else
        {
            throw new FileNotFoundException($"Path not found: {path}");
        }

        var results = extracted.Select(e => new
        {
            file = e.FileName,
            method = e.MethodName,
            line = e.LineNumber,
            queryVariable = e.QueryVariable,
            sql = e.SqlStatement,
            parameters = e.Parameters,
            sqlType = DetermineSqlType(e.SqlStatement),
            extractionMethod = "regex"
        }).ToList();

        return Task.FromResult<object>(new
        {
            path,
            method = "regex",
            totalStatements = results.Count,
            statements = results
        });
    }

    public Task<object> GetUnifiedSqlExtractionAsync()
    {
        _logger.LogInformation("Getting unified SQL extraction from all sources using REGEX (proven working)");

        var pasResults = _regexExtractor.ExtractFromDirectory(DefaultSourcePath, "*.pas");
        var dfmResults = _regexExtractor.ExtractFromDirectory(DefaultSourcePath, "*.dfm");

        var allResults = pasResults.Concat(dfmResults)
            .Select(e => new
            {
                file = e.FileName,
                method = e.MethodName,
                line = e.LineNumber,
                queryVariable = e.QueryVariable,
                sql = e.SqlStatement,
                parameters = e.Parameters,
                sqlType = DetermineSqlType(e.SqlStatement)
            })
            .ToList();

        return Task.FromResult<object>(new
        {
            totalFiles = Directory.GetFiles(DefaultSourcePath, "*.pas", SearchOption.AllDirectories).Length +
                        Directory.GetFiles(DefaultSourcePath, "*.dfm", SearchOption.AllDirectories).Length,
            totalStatements = allResults.Count,
            pasStatements = pasResults.Count,
            dfmStatements = dfmResults.Count,
            statements = allResults
        });
    }

    public Task<object> GetSqlSummaryAsync()
    {
        _logger.LogInformation("Getting SQL summary using REGEX (proven working): {Source}", DefaultSourcePath);

        var allSql = _regexExtractor.ExtractFromDirectory(DefaultSourcePath, "*.pas");

        var summary = new
        {
            totalStatements = allSql.Count,
            byType = allSql
                .GroupBy(s => DetermineSqlType(s.SqlStatement))
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList(),
            byFile = allSql
                .GroupBy(s => s.FileName)
                .Select(g => new { file = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .Take(10)
                .ToList()
        };

        return Task.FromResult<object>(summary);
    }

    public Task<object> ExtractRuntimeAssembledSqlAsync()
    {
        _logger.LogInformation("Extracting runtime-assembled SQL (SQL.Clear + SQL.Add patterns) using REGEX (proven working)");

        // Regex extractor handles runtime-assembled SQL patterns (SQL.Clear + SQL.Add)
        var results = _regexExtractor.ExtractFromDirectory(DefaultSourcePath, "*.pas");

        var formatted = results.Select(r => new
        {
            file = r.FileName,
            method = r.MethodName,
            lineNumber = r.LineNumber,
            queryVariable = r.QueryVariable,
            sql = r.SqlStatement,
            parameters = r.Parameters
        }).ToList();

        return Task.FromResult<object>(new
        {
            totalFound = results.Count,
            statements = formatted
        });
    }

    #endregion

    #region Database Analysis

    public Task<object> FindDatabaseCallsAsync(string path, string? language = null)
    {
        _logger.LogInformation("Finding database calls in {Path} using ANTLR", path);

        List<AntlrDelphiCodeAnalyzer.DatabaseCall> calls;

        if (File.Exists(path))
        {
            calls = _codeAnalyzer.FindDatabaseCalls(path);
        }
        else if (Directory.Exists(path))
        {
            calls = FindDatabaseCallsInDirectory(path, "*.pas");
        }
        else
        {
            throw new FileNotFoundException($"Path not found: {path}");
        }

        var formatted = calls.Select(c => new
        {
            line = c.LineNumber,
            component = c.Component,
            method = c.Method,
            code = c.Context,
            containingMethod = c.MethodContainer
        }).ToList();

        return Task.FromResult<object>(new
        {
            path,
            totalCalls = calls.Count,
            calls = formatted
        });
    }

    public Task<object> ExtractTableReferencesAsync(string code)
    {
        _logger.LogInformation("Extracting table references from SQL code");

        // This is SQL parsing, not Delphi code parsing
        // Use simple regex for table extraction from SQL strings
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var patterns = new[]
        {
            new System.Text.RegularExpressions.Regex(@"\bFROM\s+([a-z_][a-z0-9_]*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            new System.Text.RegularExpressions.Regex(@"\bJOIN\s+([a-z_][a-z0-9_]*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            new System.Text.RegularExpressions.Regex(@"\bINTO\s+([a-z_][a-z0-9_]*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            new System.Text.RegularExpressions.Regex(@"\bUPDATE\s+([a-z_][a-z0-9_]*)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        };

        foreach (var pattern in patterns)
        {
            var matches = pattern.Matches(code);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    tables.Add(match.Groups[1].Value);
                }
            }
        }

        return Task.FromResult<object>(new
        {
            totalTables = tables.Count,
            tables = tables.OrderBy(t => t).ToList()
        });
    }

    public Task<object> AnalyzeProcedureCallsAsync(string path)
    {
        _logger.LogInformation("Analyzing procedure calls in {Path} using ANTLR", path);

        List<AntlrDelphiCodeAnalyzer.ProcedureCall> procedures;

        if (File.Exists(path))
        {
            procedures = _codeAnalyzer.AnalyzeProcedureCalls(path);
        }
        else if (Directory.Exists(path))
        {
            procedures = AnalyzeProcedureCallsInDirectory(path, "*.pas");
        }
        else
        {
            throw new FileNotFoundException($"Path not found: {path}");
        }

        var formatted = procedures.Select(p => new
        {
            procedureName = p.Name,
            lineNumber = p.LineNumber,
            parameters = p.Parameters,
            context = p.Context
        }).ToList();

        return Task.FromResult<object>(new
        {
            path,
            totalProcedures = procedures.Count,
            procedures = formatted
        });
    }

    public Task<object> GetDatabaseObjectUsageAsync()
    {
        _logger.LogInformation("Tracking database object usage across all files using ANTLR");

        var files = Directory.GetFiles(DefaultSourcePath, "*.pas", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(DefaultSourcePath, "*.dfm", SearchOption.AllDirectories));

        var allComponents = new List<AntlrDelphiCodeAnalyzer.DatabaseComponent>();

        foreach (var file in files)
        {
            try
            {
                var components = _codeAnalyzer.FindDatabaseComponents(file);
                allComponents.AddRange(components);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing {file}", file);
            }
        }

        var formatted = allComponents.Select(c => new
        {
            componentName = c.ComponentName,
            componentType = c.ComponentType,
            sourceFile = c.SourceFile,
            lineNumber = c.LineNumber,
            fileType = Path.GetExtension(c.SourceFile)
        }).ToList();

        return Task.FromResult<object>(new
        {
            totalObjects = allComponents.Count,
            byType = formatted
                .GroupBy(o => o.componentType)
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count)
                .ToList(),
            objects = formatted
        });
    }

    #endregion

    #region Code Analysis

    public Task<object> FindPatternsAsync(string path, string regexPattern)
    {
        _logger.LogInformation("Finding pattern {Pattern} in {Path}", regexPattern, path);

        // This is explicitly a regex tool for custom pattern matching
        // Keep it for flexibility, but it's optional
        var content = File.ReadAllText(path);
        var lines = content.Split('\n');
        var matches = new List<object>();

        var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.Multiline);

        for (int i = 0; i < lines.Length; i++)
        {
            var lineMatches = regex.Matches(lines[i]);
            foreach (System.Text.RegularExpressions.Match match in lineMatches)
            {
                matches.Add(new
                {
                    line = i + 1,
                    match = match.Value,
                    context = lines[i].Trim()
                });
            }
        }

        return Task.FromResult<object>(new
        {
            path,
            pattern = regexPattern,
            totalMatches = matches.Count,
            matches
        });
    }

    public Task<object> GetCodeMetricsAsync(string path, string? language = null)
    {
        _logger.LogInformation("Getting code metrics for {Path}", path);

        var content = File.ReadAllText(path);
        var lines = content.Split('\n');

        // Simple line-based metrics (no parsing needed)
        var codeLines = lines.Where(l => !string.IsNullOrWhiteSpace(l) && !l.Trim().StartsWith("//")).Count();
        var commentLines = lines.Where(l => l.Trim().StartsWith("//")).Count();
        var blankLines = lines.Length - codeLines - commentLines;

        return Task.FromResult<object>(new
        {
            path,
            fileName = Path.GetFileName(path),
            totalLines = lines.Length,
            codeLines,
            commentLines,
            blankLines,
            sizeBytes = new FileInfo(path).Length,
            lastModified = File.GetLastWriteTime(path)
        });
    }

    public Task<object> ExtractClassDefinitionsAsync(string path, string? language = null)
    {
        _logger.LogInformation("Extracting class definitions from {Path} using ANTLR", path);

        List<AntlrDelphiCodeAnalyzer.ClassDefinition> classes;

        if (File.Exists(path))
        {
            classes = _codeAnalyzer.ExtractClassDefinitions(path);
        }
        else if (Directory.Exists(path))
        {
            classes = ExtractClassDefinitionsFromDirectory(path, "*.pas");
        }
        else
        {
            throw new FileNotFoundException($"Path not found: {path}");
        }

        var formatted = classes.Select(c => new
        {
            className = c.Name,
            baseClass = c.BaseClass,
            lineNumber = c.LineNumber,
            properties = c.Properties,
            methods = c.Methods,
            fields = c.Fields,
            visibility = c.Visibility
        }).ToList();

        return Task.FromResult<object>(new
        {
            path,
            totalClasses = classes.Count,
            classes = formatted
        });
    }

    public Task<object> ExtractMethodSignaturesAsync(string path, string? language = null)
    {
        _logger.LogInformation("Extracting method signatures from {Path} using ANTLR", path);

        List<AntlrDelphiCodeAnalyzer.MethodSignature> methods;

        if (File.Exists(path))
        {
            methods = _codeAnalyzer.ExtractMethodSignatures(path);
        }
        else if (Directory.Exists(path))
        {
            methods = ExtractMethodSignaturesFromDirectory(path, "*.pas");
        }
        else
        {
            throw new FileNotFoundException($"Path not found: {path}");
        }

        var formatted = methods.Select(m => new
        {
            name = m.Name,
            fullName = m.FullName,
            returnType = m.ReturnType,
            parameters = m.Parameters.Select(p => new
            {
                name = p.Name,
                type = p.DataType,
                parameterType = p.ParameterType
            }).ToList(),
            isFunction = m.IsFunction,
            isProcedure = m.IsProcedure,
            className = m.ClassName,
            lineNumber = m.LineNumber,
            visibility = m.Visibility
        }).ToList();

        return Task.FromResult<object>(new
        {
            path,
            totalMethods = methods.Count,
            methods = formatted
        });
    }

    public Task<object> MapDataStructuresAsync(string path, string? language = null)
    {
        _logger.LogInformation("Mapping data structures from {Path} using ANTLR", path);

        List<AntlrDelphiCodeAnalyzer.DataStructure> structures;

        if (File.Exists(path))
        {
            structures = _codeAnalyzer.MapDataStructures(path);
        }
        else if (Directory.Exists(path))
        {
            structures = MapDataStructuresFromDirectory(path, "*.pas");
        }
        else
        {
            throw new FileNotFoundException($"Path not found: {path}");
        }

        var formatted = structures.Select(s => new
        {
            name = s.Name,
            type = s.Type,
            fields = s.Fields.Select(f => new
            {
                name = f.Name,
                type = f.DataType,
                defaultValue = f.DefaultValue
            }).ToList(),
            lineNumber = s.LineNumber
        }).ToList();

        return Task.FromResult<object>(new
        {
            path,
            totalStructures = structures.Count,
            structures = formatted
        });
    }

    #endregion

    #region Helper Methods

    private List<AntlrDelphiCodeAnalyzer.DatabaseCall> FindDatabaseCallsInDirectory(string directory, string searchPattern)
    {
        var results = new List<AntlrDelphiCodeAnalyzer.DatabaseCall>();
        var files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var calls = _codeAnalyzer.FindDatabaseCalls(file);
                results.AddRange(calls);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing {file}", file);
            }
        }

        return results;
    }

    private List<AntlrDelphiCodeAnalyzer.ProcedureCall> AnalyzeProcedureCallsInDirectory(string directory, string searchPattern)
    {
        var results = new List<AntlrDelphiCodeAnalyzer.ProcedureCall>();
        var files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var calls = _codeAnalyzer.AnalyzeProcedureCalls(file);
                results.AddRange(calls);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing {file}", file);
            }
        }

        return results;
    }

    private List<AntlrDelphiCodeAnalyzer.ClassDefinition> ExtractClassDefinitionsFromDirectory(string directory, string searchPattern)
    {
        var results = new List<AntlrDelphiCodeAnalyzer.ClassDefinition>();
        var files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var classes = _codeAnalyzer.ExtractClassDefinitions(file);
                results.AddRange(classes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing {file}", file);
            }
        }

        return results;
    }

    private List<AntlrDelphiCodeAnalyzer.MethodSignature> ExtractMethodSignaturesFromDirectory(string directory, string searchPattern)
    {
        var results = new List<AntlrDelphiCodeAnalyzer.MethodSignature>();
        var files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var methods = _codeAnalyzer.ExtractMethodSignatures(file);
                results.AddRange(methods);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing {file}", file);
            }
        }

        return results;
    }

    private List<AntlrDelphiCodeAnalyzer.DataStructure> MapDataStructuresFromDirectory(string directory, string searchPattern)
    {
        var results = new List<AntlrDelphiCodeAnalyzer.DataStructure>();
        var files = Directory.GetFiles(directory, searchPattern, SearchOption.AllDirectories);

        foreach (var file in files)
        {
            try
            {
                var structures = _codeAnalyzer.MapDataStructures(file);
                results.AddRange(structures);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing {file}", file);
            }
        }

        return results;
    }

    private static string DetermineSqlType(string sql)
    {
        var upperSql = sql.TrimStart().ToUpperInvariant();

        if (upperSql.StartsWith("SELECT")) return "SELECT";
        if (upperSql.StartsWith("INSERT")) return "INSERT";
        if (upperSql.StartsWith("UPDATE")) return "UPDATE";
        if (upperSql.StartsWith("DELETE")) return "DELETE";
        if (upperSql.StartsWith("EXECUTE")) return "EXECUTE";
        if (upperSql.StartsWith("WITH")) return "CTE";

        return "UNKNOWN";
    }

    #endregion
}