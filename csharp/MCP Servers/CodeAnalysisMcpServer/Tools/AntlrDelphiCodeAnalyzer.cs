using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using CodeAnalysisMcpServer.Generated;
using SfD.Global.Logging;

namespace CodeAnalysisMcpServer.Tools;

/// <summary>
/// HIGH-PERFORMANCE PARALLEL code analyzer with memory management
/// Optimized for 48+ core systems
/// </summary>
public class AntlrDelphiCodeAnalyzer : IDisposable
{
    private const int LARGE_FILE_THRESHOLD = 1024 * 1024; // 1MB
    private bool _disposed = false;
    private SfdLogger _logger;

    // Progress callback
    public Action<int, int, string>? ProgressCallback { get; set; }

    public AntlrDelphiCodeAnalyzer(SfdLogger logger) { _logger = logger; }

    public record DatabaseCall
    {
        public int LineNumber { get; init; }
        public string Component { get; init; } = "";
        public string Method { get; init; } = "";
        public string Context { get; init; } = "";
        public string MethodContainer { get; init; } = "";
    }

    public record ClassDefinition
    {
        public string Name { get; init; } = "";
        public string BaseClass { get; init; } = "";
        public int LineNumber { get; init; }
        public List<string> Properties { get; init; } = [];
        public List<string> Methods { get; init; } = [];
        public List<string> Fields { get; init; } = [];
        public string Visibility { get; init; } = "";
    }

    public record MethodSignature
    {
        public string Name { get; init; } = "";
        public string FullName { get; init; } = "";
        public string ReturnType { get; init; } = "";
        public List<ParameterInfo> Parameters { get; init; } = [];
        public string Visibility { get; init; } = "";
        public int LineNumber { get; init; }
        public bool IsFunction { get; init; }
        public bool IsProcedure { get; init; }
        public string ClassName { get; init; } = "";
    }

    public record ParameterInfo
    {
        public string Name { get; init; } = "";
        public string DataType { get; init; } = "";
        public string ParameterType { get; init; } = "";
    }

    public record DataStructure
    {
        public string Name { get; init; } = "";
        public string Type { get; init; } = "";
        public List<FieldInfo> Fields { get; init; } = [];
        public int LineNumber { get; init; }
    }

    public record FieldInfo
    {
        public string Name { get; init; } = "";
        public string DataType { get; init; } = "";
        public string DefaultValue { get; init; } = "";
    }

    public record DatabaseComponent
    {
        public string ComponentName { get; init; } = "";
        public string ComponentType { get; init; } = "";
        public string SourceFile { get; init; } = "";
        public int LineNumber { get; init; }
    }

    public record ProcedureCall
    {
        public string Name { get; init; } = "";
        public int LineNumber { get; init; }
        public List<string> Parameters { get; init; } = [];
        public string Context { get; init; } = "";
    }

    #region Single File Methods (Memory Optimized)

    public List<DatabaseCall> FindDatabaseCalls(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        string? content = null;
        try
        {
            content = File.ReadAllText(filePath);
            return FindDatabaseCallsInContent(content);
        }
        finally
        {
            content = null;

            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > LARGE_FILE_THRESHOLD)
            {
                GC.Collect(1, GCCollectionMode.Optimized, false);
            }
        }
    }

    public List<DatabaseCall> FindDatabaseCallsInContent(string content)
    {
        AntlrInputStream? inputStream = null;
        DelphiLexer? lexer = null;
        CommonTokenStream? tokenStream = null;
        DelphiParser? parser = null;
        IParseTree? tree = null;
        DatabaseCallVisitor? visitor = null;

        try
        {
            inputStream = new AntlrInputStream(content);
            lexer = new DelphiLexer(inputStream);
            tokenStream = new CommonTokenStream(lexer);
            parser = new DelphiParser(tokenStream);

            parser.RemoveErrorListeners();
            lexer.RemoveErrorListeners();

            tree = parser.file();
            visitor = new DatabaseCallVisitor();
            visitor.Visit(tree);

            var results = new List<DatabaseCall>(visitor.DatabaseCalls);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError("Error finding database calls: , {Message}", ex.Message);
            return [];
        }
        finally
        {
            visitor = null;
            tree = null;
            parser = null;
            tokenStream = null;
            lexer = null;
            inputStream = null;
        }
    }

    #endregion

    #region Parallel Directory Methods

    /// <summary>
    /// HIGH-PERFORMANCE parallel database call extraction
    /// </summary>
    public List<DatabaseCall> FindDatabaseCallsInDirectory(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
            return [];

        var files = Directory.GetFiles(directoryPath, "*.pas", SearchOption.AllDirectories);
        var totalFiles = files.Length;

        Console.WriteLine($"Scanning {totalFiles} files for database calls using {Environment.ProcessorCount} cores...");

        var results = new ConcurrentBag<DatabaseCall>();
        var processedCount = 0;

        var parallelResults = files.AsParallel()
            .WithDegreeOfParallelism(Environment.ProcessorCount)
            .WithCancellation(cancellationToken)
            .SelectMany(file =>
            {
                try
                {
                    var calls = FindDatabaseCalls(file);

                    var count = Interlocked.Increment(ref processedCount);
                    if (count % 100 == 0)
                    {
                        Console.WriteLine($"Progress: {count}/{totalFiles} files");
                        ProgressCallback?.Invoke(count, totalFiles, file);
                    }

                    return calls;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing {file}: {ex.Message}");
                    return [];
                }
            })
            .ToList();

        _logger.LogInformation($"Found {parallelResults.Count} database calls in {processedCount} files");
        return parallelResults;
    }

    #endregion

    #region Other Analysis Methods (keeping simple for brevity)

    public List<ClassDefinition> ExtractClassDefinitions(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        string? content = null;
        try
        {
            content = File.ReadAllText(filePath);
            return ExtractClassDefinitionsFromContent(content);
        }
        finally
        {
            content = null;
        }
    }

    public List<ClassDefinition> ExtractClassDefinitionsFromContent(string content)
    {
        var classes = new List<ClassDefinition>();
        var classPattern = new Regex(@"(\w+)\s*=\s*class\s*\((\w+)\)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var matches = classPattern.Matches(content);

        int lineNum = 1;
        foreach (Match match in matches)
        {
            var pos = match.Index;
            lineNum = content.Substring(0, pos).Count(c => c == '\n') + 1;

            classes.Add(new ClassDefinition
            {
                Name = match.Groups[1].Value,
                BaseClass = match.Groups[2].Value,
                LineNumber = lineNum,
                Properties = [],
                Methods = [],
                Fields = [],
                Visibility = "public"
            });
        }

        return classes;
    }

    public List<MethodSignature> ExtractMethodSignatures(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        string? content = null;
        try
        {
            content = File.ReadAllText(filePath);
            return ExtractMethodSignaturesFromContent(content);
        }
        finally
        {
            content = null;
        }
    }

    public List<MethodSignature> ExtractMethodSignaturesFromContent(string content)
    {
        var methods = new List<MethodSignature>();
        var methodPatterns = new[]
        {
            new Regex(@"(procedure|function)\s+(\w+)\.(\w+)\s*(\([^)]*\))?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"(procedure|function)\s+(\w+)\s*(\([^)]*\))?",
                RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };

        int lineNum = 1;
        foreach (var pattern in methodPatterns)
        {
            var matches = pattern.Matches(content);
            foreach (Match match in matches)
            {
                var pos = match.Index;
                lineNum = content.Substring(0, pos).Count(c => c == '\n') + 1;

                var isFunction = match.Groups[1].Value.Equals("function", StringComparison.OrdinalIgnoreCase);
                var name = match.Groups.Count > 3 ? match.Groups[3].Value : match.Groups[2].Value;

                methods.Add(new MethodSignature
                {
                    Name = name,
                    FullName = match.Value.Trim(),
                    ReturnType = "",
                    Parameters = [],
                    Visibility = "public",
                    LineNumber = lineNum,
                    IsFunction = isFunction,
                    IsProcedure = !isFunction,
                    ClassName = ""
                });
            }
        }

        return methods;
    }

    public List<DataStructure> MapDataStructures(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        string? content = null;
        try
        {
            content = File.ReadAllText(filePath);
            return MapDataStructuresFromContent(content);
        }
        finally
        {
            content = null;
        }
    }

    public List<DataStructure> MapDataStructuresFromContent(string content)
    {
        var structures = new List<DataStructure>();
        var recordPattern = new Regex(@"(\w+)\s*=\s*record\s+(.*?)\s+end;",
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var matches = recordPattern.Matches(content);

        foreach (Match match in matches)
        {
            var pos = match.Index;
            var lineNum = content.Substring(0, pos).Count(c => c == '\n') + 1;

            var fieldPattern = new Regex(@"(\w+)\s*:\s*(\w+);", RegexOptions.Compiled);
            var fields = fieldPattern.Matches(match.Groups[2].Value)
                .Cast<Match>()
                .Select(m => new FieldInfo
                {
                    Name = m.Groups[1].Value,
                    DataType = m.Groups[2].Value,
                    DefaultValue = ""
                })
                .ToList();

            structures.Add(new DataStructure
            {
                Name = match.Groups[1].Value,
                Type = "record",
                Fields = fields,
                LineNumber = lineNum
            });
        }

        return structures;
    }

    public List<DatabaseComponent> FindDatabaseComponents(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        string? content = null;
        try
        {
            content = File.ReadAllText(filePath);
            var fileName = Path.GetFileName(filePath);
            return FindDatabaseComponentsInContent(content, fileName);
        }
        finally
        {
            content = null;
        }
    }

    public List<DatabaseComponent> FindDatabaseComponentsInContent(string content, string fileName)
    {
        var components = new List<DatabaseComponent>();
        var dbComponentPatterns = new[]
        {
            (@"([\w]+)\s*:\s*TpFIBDatabase", "Database"),
            (@"([\w]+)\s*:\s*TpFIBTransaction", "Transaction"),
            (@"([\w]+)\s*:\s*TpFIBQuery", "Query"),
            (@"([\w]+)\s*:\s*TpFIBDataSet", "DataSet"),
            (@"([\w]+)\s*:\s*TpFIBStoredProc", "StoredProc"),
            (@"([\w]+)\s*:\s*TADOQuery", "Query"),
            (@"([\w]+)\s*:\s*TADOConnection", "Database"),
            (@"([\w]+)\s*:\s*TADOTable", "DataSet")
        };

        foreach (var (pattern, componentType) in dbComponentPatterns)
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            var matches = regex.Matches(content);

            foreach (Match match in matches)
            {
                var pos = match.Index;
                var lineNum = content.Substring(0, pos).Count(c => c == '\n') + 1;

                components.Add(new DatabaseComponent
                {
                    ComponentName = match.Groups[1].Value,
                    ComponentType = componentType,
                    SourceFile = fileName,
                    LineNumber = lineNum
                });
            }
        }

        return components;
    }

    public List<ProcedureCall> AnalyzeProcedureCalls(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        string? content = null;
        try
        {
            content = File.ReadAllText(filePath);
            return AnalyzeProcedureCallsInContent(content);
        }
        finally
        {
            content = null;
        }
    }

    public List<ProcedureCall> AnalyzeProcedureCallsInContent(string content)
    {
        var procedures = new List<ProcedureCall>();
        var pattern = new Regex(@"\bEXECUTE\s+PROCEDURE\s+([a-z_][a-z0-9_]*)|EXEC\s+([a-z_][a-z0-9_]*)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        var matches = pattern.Matches(content);

        foreach (Match match in matches)
        {
            var pos = match.Index;
            var lineNum = content.Substring(0, pos).Count(c => c == '\n') + 1;
            var procName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

            procedures.Add(new ProcedureCall
            {
                Name = procName,
                LineNumber = lineNum,
                Parameters = [],
                Context = match.Value
            });
        }

        return procedures;
    }

    #endregion

    #region Visitor Classes

    private class DatabaseCallVisitor : DelphiBaseVisitor<object?>
    {
        private readonly List<DatabaseCall> _databaseCalls = [];
        private string _currentMethod = "Unknown";

        public List<DatabaseCall> DatabaseCalls => _databaseCalls;

        public override object? VisitProcDecl(DelphiParser.ProcDeclContext context)
        {
            var oldMethod = _currentMethod;
            var heading = context.procDeclHeading();
            if (heading?.ident() != null)
            {
                _currentMethod = heading.ident().GetText();
            }
            var result = base.VisitProcDecl(context);
            _currentMethod = oldMethod;
            return result;
        }

        public override object? VisitMethodDecl(DelphiParser.MethodDeclContext context)
        {
            var oldMethod = _currentMethod;
            var heading = context.methodDeclHeading();
            if (heading?.methodName() != null)
            {
                _currentMethod = heading.methodName().GetText();
            }
            var result = base.VisitMethodDecl(context);
            _currentMethod = oldMethod;
            return result;
        }

        public override object? VisitSimpleStatement(DelphiParser.SimpleStatementContext context)
        {
            var text = context.GetText();

            if (text.Contains(".ExecSQL(") || text.Contains(".Open(") ||
                text.Contains(".Execute(") || text.Contains(".Query("))
            {
                var match = Regex.Match(
                    text,
                    @"(\w+)\.(ExecSQL|Open|Execute|Query)\(",
                    RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    _databaseCalls.Add(new DatabaseCall
                    {
                        LineNumber = context.Start.Line,
                        Component = match.Groups[1].Value,
                        Method = match.Groups[2].Value,
                        Context = text.Length > 100 ? text.Substring(0, 100) + "..." : text,
                        MethodContainer = _currentMethod
                    });
                }
            }

            return base.VisitSimpleStatement(context);
        }
    }

    #endregion

    #region IDisposable Implementation

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Dispose managed resources if any
            }

            _disposed = true;
        }
    }

    #endregion
}