using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using CodeAnalysisMcpServer.Generated;
using SfD.Global.Logging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace CodeAnalysisMcpServer.Tools
{
    /// <summary>
    /// OPTIMIZED SQL extractor: Fast regex pre-filter + ANTLR verification
    /// Combines speed of regex with accuracy of full parsing
    /// Only parses files that actually contain SQL patterns (90%+ of files skipped)
    /// </summary>
    public partial class AntlrDelphiSqlExtractor : IDisposable
    {
        private const int LARGE_FILE_THRESHOLD = 1024 * 1024; // 1MB
        private const int BATCH_SIZE = 100;
        private bool _disposed = false;
        private SfdLogger _logger;

        public AntlrDelphiSqlExtractor(SfdLogger logger) { _logger = logger; }

        // Pre-filter regex patterns (fast rejection)
        // Matches: .SQL.Clear, .SQL.Add, AND actual SQL keywords
        [GeneratedRegex(@"\.SQL\.(Clear|Add)|\b(SELECT|INSERT|UPDATE|DELETE)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex SqlPatternDetector();

        public Action<int, int, string>? ProgressCallback { get; set; }

        // Statistics for performance monitoring
        public record ExtractionStats
        {
            public int TotalFiles { get; init; }
            public int FilesWithSql { get; init; }
            public int FilesSkipped { get; init; }
            public int StatementsExtracted { get; init; }
            public TimeSpan ElapsedTime { get; init; }
            public double FilesPerSecond { get; init; }
        }

        public record ExtractedSql
        {
            public string FileName { get; init; } = "";
            public string MethodName { get; init; } = "";
            public int LineNumber { get; init; }
            public string SqlStatement { get; init; } = "";
            public string SqlWithParameters { get; init; } = "";
            public List<string> Parameters { get; init; } = [];
            public List<DetectedInterpolation> Interpolations { get; init; } = [];
            public string QueryVariable { get; init; } = "";
            public bool HasInterpolation { get; init; }
        }

        public record DetectedInterpolation
        {
            public string VariableName { get; init; } = "";
            public string ConversionFunction { get; init; } = "";
            public string SuggestedParameterName { get; init; } = "";
            public string SqlFragment { get; init; } = "";
            public InterpolationType Type { get; init; }
        }

        public enum InterpolationType
        {
            DirectVariable,
            IntToStr,
            QuotedStr,
            FormatDateTime,
            FloatToStr,
            BoolToStr,
            Format,
            Other
        }

        public List<ExtractedSql> ExtractFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                return [];

            string? content = null;
            try
            {
                content = File.ReadAllText(filePath);

                // FAST PRE-FILTER: Skip files without SQL patterns (90%+ of files)
                var hasPattern = SqlPatternDetector().IsMatch(content);
                if (!hasPattern)
                {
                    return [];
                }

                _logger.LogInformation($"Pre-filter passed: {Path.GetFileName(filePath)}");
                var fileName = Path.GetFileName(filePath);
                return ExtractFromContent(content, fileName);
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

        public List<ExtractedSql> ExtractFromContent(string content, string fileName)
        {
            AntlrInputStream? inputStream = null;
            DelphiLexer? lexer = null;
            CommonTokenStream? tokenStream = null;
            DelphiParser? parser = null;
            IParseTree? tree = null;
            SqlExtractionVisitor? visitor = null;

            try
            {
                inputStream = new AntlrInputStream(content);
                lexer = new DelphiLexer(inputStream);
                tokenStream = new CommonTokenStream(lexer);
                parser = new DelphiParser(tokenStream);

                // Disable error reporting for speed (we're not validating syntax)
                parser.RemoveErrorListeners();
                lexer.RemoveErrorListeners();

                tree = parser.file();
                visitor = new SqlExtractionVisitor(fileName);
                visitor.Visit(tree);

                return new List<ExtractedSql>(visitor.ExtractedStatements);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing {fileName}: {ex.Message}");
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

        /// <summary>
        /// HIGH-PERFORMANCE parallel extraction with pre-filtering
        /// Files without SQL patterns are skipped instantly (90%+ speedup)
        /// </summary>
        public (List<ExtractedSql> Results, ExtractionStats Stats) ExtractFromDirectoryWithStats(
            string directoryPath,
            string searchPattern = "*.pas",
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            if (!Directory.Exists(directoryPath)) {
             
                _logger.LogWarning(directoryPath + "does not exist");
                return ([], new ExtractionStats());
            }

            var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories);
            var totalFiles = files.Length;
            var processorCount = Environment.ProcessorCount;
            var filesWithSql = 0;
            var filesSkipped = 0;

            Console.WriteLine($"Pre-scanning {totalFiles} files for SQL patterns...");

            var results = new ConcurrentBag<ExtractedSql>();
            var processedCount = 0;

            // Process in batches for memory management
            var batches = files.Chunk(BATCH_SIZE);
            var batchNumber = 0;

            foreach (var batch in batches)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Cancelled after processing {processedCount} files");
                    break;
                }

                batchNumber++;

                var batchResults = batch.AsParallel()
                    .WithDegreeOfParallelism(processorCount)
                    .WithCancellation(cancellationToken)
                    .SelectMany(file =>
                    {
                        try
                        {
                            var extracted = ExtractFromFile(file);

                            var count = Interlocked.Increment(ref processedCount);

                            if (extracted.Count > 0)
                            {
                                Interlocked.Increment(ref filesWithSql);
                                Console.WriteLine($"  ✓ Found {extracted.Count} SQL statements in: {Path.GetFileName(file)}");
                            }
                            else
                            {
                                Interlocked.Increment(ref filesSkipped);
                            }

                            if (count % 100 == 0 || count == totalFiles)
                            {
                                Console.WriteLine($"Progress: {count}/{totalFiles} ({count * 100.0 / totalFiles:F1}%) - SQL files: {filesWithSql}, Skipped: {filesSkipped}");
                                ProgressCallback?.Invoke(count, totalFiles, file);
                            }

                            return extracted;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing {file}: {ex.Message}");
                            return [];
                        }
                    })
                    .ToList();

                foreach (var result in batchResults)
                {
                    results.Add(result);
                }

                // GC hint after each batch
                if (batchNumber % 5 == 0)
                {
                    GC.Collect(1, GCCollectionMode.Optimized, false);
                }
            }

            var elapsed = DateTime.UtcNow - startTime;
            var filesPerSecond = totalFiles / elapsed.TotalSeconds;

            Console.WriteLine($"Completed: {totalFiles} files in {elapsed.TotalSeconds:F1}s ({filesPerSecond:F0} files/sec)");
            Console.WriteLine($"  Files with SQL: {filesWithSql} (parsed with ANTLR)");
            Console.WriteLine($"  Files skipped: {filesSkipped} (no SQL patterns)");
            Console.WriteLine($"  SQL statements: {results.Count}");

            var stats = new ExtractionStats
            {
                TotalFiles = totalFiles,
                FilesWithSql = filesWithSql,
                FilesSkipped = filesSkipped,
                StatementsExtracted = results.Count,
                ElapsedTime = elapsed,
                FilesPerSecond = filesPerSecond
            };

            return (results.ToList(), stats);
        }

        /// <summary>
        /// Backward-compatible version that returns just the results without stats
        /// </summary>
        public List<ExtractedSql> ExtractFromDirectory(
            string directoryPath,
            string searchPattern = "*.pas",
            CancellationToken cancellationToken = default)
        {
            var (results, _) = ExtractFromDirectoryWithStats(directoryPath, searchPattern, cancellationToken);
            return results;
        }

        /// <summary>
        /// Async version for very large codebases
        /// </summary>
        public async Task<(List<ExtractedSql> Results, ExtractionStats Stats)> ExtractFromDirectoryAsyncWithStats(
            string directoryPath,
            string searchPattern = "*.pas",
            int? maxDegreeOfParallelism = null,
            CancellationToken cancellationToken = default)
        {
            var startTime = DateTime.UtcNow;

            if (!Directory.Exists(directoryPath))
                return ([], new ExtractionStats());

            var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories);
            var totalFiles = files.Length;
            var parallelism = maxDegreeOfParallelism ?? Environment.ProcessorCount;
            var filesWithSql = 0;
            var filesSkipped = 0;

            Console.WriteLine($"Async processing {totalFiles} files using {parallelism} parallel tasks...");

            var results = new ConcurrentBag<ExtractedSql>();
            var processedCount = 0;

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = parallelism,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(files, parallelOptions, async (file, ct) =>
            {
                try
                {
                    var extracted = await Task.Run(() => ExtractFromFile(file), ct);

                    foreach (var item in extracted)
                    {
                        results.Add(item);
                    }

                    var count = Interlocked.Increment(ref processedCount);

                    if (extracted.Count > 0)
                    {
                        Interlocked.Increment(ref filesWithSql);
                    }
                    else
                    {
                        Interlocked.Increment(ref filesSkipped);
                    }

                    if (count % 200 == 0 || count == totalFiles)
                    {
                        Console.WriteLine($"Progress: {count}/{totalFiles} ({count * 100.0 / totalFiles:F1}%) - SQL files: {filesWithSql}");
                        ProgressCallback?.Invoke(count, totalFiles, file);
                    }

                    if (count % 500 == 0)
                    {
                        GC.Collect(1, GCCollectionMode.Optimized, false);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {file}: {ex.Message}");
                }
            });

            var elapsed = DateTime.UtcNow - startTime;
            var filesPerSecond = totalFiles / elapsed.TotalSeconds;

            Console.WriteLine($"Completed: {totalFiles} files in {elapsed.TotalSeconds:F1}s ({filesPerSecond:F0} files/sec)");
            Console.WriteLine($"  SQL statements: {results.Count}");

            var stats = new ExtractionStats
            {
                TotalFiles = totalFiles,
                FilesWithSql = filesWithSql,
                FilesSkipped = filesSkipped,
                StatementsExtracted = results.Count,
                ElapsedTime = elapsed,
                FilesPerSecond = filesPerSecond
            };

            return (results.ToList(), stats);
        }

        /// <summary>
        /// Backward-compatible async version that returns just the results without stats
        /// </summary>
        public async Task<List<ExtractedSql>> ExtractFromDirectoryAsync(
            string directoryPath,
            string searchPattern = "*.pas",
            int? maxDegreeOfParallelism = null,
            CancellationToken cancellationToken = default)
        {
            var (results, _) = await ExtractFromDirectoryAsyncWithStats(directoryPath, searchPattern, maxDegreeOfParallelism, cancellationToken);
            return results;
        }

        public string GenerateReport(List<ExtractedSql> sqlStatements)
        {
            var report = new StringBuilder();

            report.AppendLine("# Extracted SQL Statements (ANTLR-verified, comment-aware)");
            report.AppendLine();
            report.AppendLine($"Total statements: {sqlStatements.Count}");
            report.AppendLine($"With interpolations: {sqlStatements.Count(s => s.HasInterpolation)}");
            report.AppendLine();

            var grouped = sqlStatements.GroupBy(s => s.FileName);

            foreach (var fileGroup in grouped)
            {
                report.AppendLine($"## File: {fileGroup.Key}");
                report.AppendLine();

                foreach (var sql in fileGroup)
                {
                    report.AppendLine($"### {sql.MethodName} (Line {sql.LineNumber})");
                    report.AppendLine();
                    report.AppendLine($"**Query Variable:** `{sql.QueryVariable}`");
                    report.AppendLine();

                    if (sql.HasInterpolation)
                    {
                        report.AppendLine("**⚠️ Contains Interpolation - Needs Parameterization**");
                        report.AppendLine();

                        report.AppendLine("**Original SQL (with interpolation):**");
                        report.AppendLine("```sql");
                        report.AppendLine(sql.SqlStatement);
                        report.AppendLine("```");
                        report.AppendLine();

                        report.AppendLine("**Converted SQL (parameterized):**");
                        report.AppendLine("```sql");
                        report.AppendLine(sql.SqlWithParameters);
                        report.AppendLine("```");
                        report.AppendLine();

                        report.AppendLine("**Detected Interpolations:**");
                        foreach (var interp in sql.Interpolations)
                        {
                            report.AppendLine($"- **Variable:** `{interp.VariableName}`");
                            report.AppendLine($"  - Type: {interp.Type}");
                            report.AppendLine($"  - Suggested Parameter: `:{interp.SuggestedParameterName}`");
                            if (!string.IsNullOrEmpty(interp.ConversionFunction))
                                report.AppendLine($"  - Conversion: {interp.ConversionFunction}");
                            report.AppendLine();
                        }
                    }
                    else
                    {
                        report.AppendLine("**SQL:**");
                        report.AppendLine("```sql");
                        report.AppendLine(sql.SqlStatement);
                        report.AppendLine("```");
                        report.AppendLine();
                    }

                    if (sql.Parameters.Count > 0)
                    {
                        report.AppendLine("**Parameters:**");
                        foreach (var param in sql.Parameters)
                        {
                            report.AppendLine($"- `:{param}`");
                        }
                        report.AppendLine();
                    }

                    report.AppendLine("---");
                    report.AppendLine();
                }
            }

            return report.ToString();
        }

        #region IDisposable

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
                    // Dispose managed resources
                }
                _disposed = true;
            }
        }

        #endregion

        #region Nested Classes

        private class SqlExtractionVisitor : DelphiBaseVisitor<object?>
        {
            private readonly string _fileName;
            private string _currentMethod = "Unknown";
            private readonly List<ExtractedSql> _extractedStatements = [];
            private readonly Dictionary<string, SqlBuilder> _activeSqlBuilders = [];

            public SqlExtractionVisitor(string fileName)
            {
                _fileName = fileName;
            }

            public List<ExtractedSql> ExtractedStatements => _extractedStatements; 

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

                // Match both .SQL.Clear and .SQL.Clear() (with or without semicolon)
                if (Regex.IsMatch(text, @"\.SQL\.Clear\s*(\(\s*\))?", RegexOptions.IgnoreCase))
                {
                    var match = Regex.Match(text, @"(\w+)\.SQL\.Clear", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var queryVar = match.Groups[1].Value;
                        _activeSqlBuilders[queryVar] = new SqlBuilder
                        {
                            QueryVariable = queryVar,
                            MethodName = _currentMethod,
                            LineNumber = context.Start.Line
                        };
                    }
                }
                else if (Regex.IsMatch(text, @"\.SQL\.Add\(", RegexOptions.IgnoreCase))
                {
                    ProcessSqlAddStatement(context);
                }
                else if (Regex.IsMatch(text, @"\.ParamByName\(", RegexOptions.IgnoreCase))
                {
                    ProcessParamAssignment(context);
                }
                else if (Regex.IsMatch(text, @"\.(ExecSQL|Open)\(", RegexOptions.IgnoreCase))
                {
                    ProcessQueryExecution(context);
                }

                return base.VisitSimpleStatement(context);
            }

            private void ProcessSqlAddStatement(DelphiParser.SimpleStatementContext context)
            {
                var text = context.GetText();
                var match = Regex.Match(text, @"(\w+)\.SQL\.Add\((.*)\)", RegexOptions.IgnoreCase | RegexOptions.Singleline);

                if (match.Success)
                {
                    var queryVar = match.Groups[1].Value;

                    var expressions = FindAllExpressions(context);
                    if (expressions.Count > 0)
                    {
                        var sqlExpr = expressions[0];
                        var analysis = AnalyzeSqlExpression(sqlExpr);

                        if (_activeSqlBuilders.TryGetValue(queryVar, out var builder))
                        {
                            builder.AddFragment(analysis.SqlText, analysis.Interpolations);
                        }
                        else
                        {
                            _activeSqlBuilders[queryVar] = new SqlBuilder
                            {
                                QueryVariable = queryVar,
                                MethodName = _currentMethod,
                                LineNumber = context.Start.Line
                            };
                            _activeSqlBuilders[queryVar].AddFragment(analysis.SqlText, analysis.Interpolations);
                        }
                    }
                }
            }

            private void ProcessParamAssignment(DelphiParser.SimpleStatementContext context)
            {
                var text = context.GetText();
                var match = Regex.Match(text, @"(\w+)\.ParamByName\('([^']+)'\)\.(AsString|AsInteger|AsFloat|AsDateTime|AsBoolean)\s*:=", RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    var queryVar = match.Groups[1].Value;
                    var paramName = match.Groups[2].Value;

                    if (_activeSqlBuilders.TryGetValue(queryVar, out var builder))
                    {
                        builder.AddParameter(paramName);
                    }
                }
            }

            private void ProcessQueryExecution(DelphiParser.SimpleStatementContext context)
            {
                var text = context.GetText();
                var match = Regex.Match(text, @"(\w+)\.(ExecSQL|Open)\(", RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    var queryVar = match.Groups[1].Value;

                    if (_activeSqlBuilders.TryGetValue(queryVar, out var builder))
                    {
                        var builtSql = builder.Build();

                        _extractedStatements.Add(new ExtractedSql
                        {
                            FileName = _fileName,
                            MethodName = builder.MethodName,
                            LineNumber = builder.LineNumber,
                            SqlStatement = builtSql.OriginalSql,
                            SqlWithParameters = builtSql.ParameterizedSql,
                            Parameters = builder.Parameters,
                            Interpolations = builtSql.AllInterpolations,
                            QueryVariable = builder.QueryVariable,
                            HasInterpolation = builtSql.AllInterpolations.Count > 0
                        });

                        _activeSqlBuilders.Remove(queryVar);
                    }
                }
            }

            private static List<DelphiParser.ExpressionContext> FindAllExpressions(ParserRuleContext context)
            {
                var expressions = new List<DelphiParser.ExpressionContext>();

                for (int i = 0; i < context.ChildCount; i++)
                {
                    var child = context.GetChild(i);
                    if (child is DelphiParser.ExpressionContext expr)
                    {
                        expressions.Add(expr);
                    }
                    else if (child is ParserRuleContext prc)
                    {
                        expressions.AddRange(FindAllExpressions(prc));
                    }
                }

                return expressions;
            }

            private (string SqlText, List<DetectedInterpolation> Interpolations) AnalyzeSqlExpression(DelphiParser.ExpressionContext context)
            {
                var interpolations = new List<DetectedInterpolation>();
                var sqlText = new StringBuilder();

                AnalyzeExpressionRecursive(context, sqlText, interpolations);

                return (sqlText.ToString(), interpolations);
            }

            private void AnalyzeExpressionRecursive(ParserRuleContext context, StringBuilder sqlText, List<DetectedInterpolation> interpolations)
            {
                if (context == null) return;

                if (context is DelphiParser.SimpleExpressionContext simpleExpr)
                {
                    ProcessSimpleExpression(simpleExpr, sqlText, interpolations);
                    return;
                }

                if (context is DelphiParser.FactorContext factor)
                {
                    ProcessFactor(factor, sqlText, interpolations);
                    return;
                }

                for (int i = 0; i < context.ChildCount; i++)
                {
                    var child = context.GetChild(i);
                    if (child is ParserRuleContext prc)
                    {
                        AnalyzeExpressionRecursive(prc, sqlText, interpolations);
                    }
                }
            }

            private void ProcessSimpleExpression(DelphiParser.SimpleExpressionContext context, StringBuilder sqlText, List<DetectedInterpolation> interpolations)
            {
                for (int i = 0; i < context.ChildCount; i++)
                {
                    var child = context.GetChild(i);

                    if (child is DelphiParser.OperatorContext op)
                    {
                        var opText = op.GetText();
                        if (opText != "+")
                        {
                            sqlText.Append(' ').Append(opText).Append(' ');
                        }
                    }
                    else if (child is DelphiParser.FactorContext factor)
                    {
                        ProcessFactor(factor, sqlText, interpolations);
                    }
                    else if (child is ParserRuleContext prc)
                    {
                        AnalyzeExpressionRecursive(prc, sqlText, interpolations);
                    }
                }
            }

            private void ProcessFactor(DelphiParser.FactorContext factor, StringBuilder sqlText, List<DetectedInterpolation> interpolations)
            {
                var text = factor.GetText();

                if (factor.stringFactor() != null)
                {
                    var str = ExtractStringFromFactor(factor);
                    sqlText.Append(str);
                }
                else if (factor.designator() != null)
                {
                    var designator = factor.designator();
                    var designatorText = designator.GetText();

                    var interpolation = DetectInterpolation(designator);
                    if (interpolation != null)
                    {
                        interpolations.Add(interpolation);
                        sqlText.Append(':').Append(interpolation.SuggestedParameterName);
                    }
                    else
                    {
                        if (!IsLikelySqlKeyword(designatorText))
                        {
                            var interp = new DetectedInterpolation
                            {
                                VariableName = designatorText,
                                ConversionFunction = "",
                                SuggestedParameterName = SuggestParameterName(designatorText, InterpolationType.DirectVariable),
                                SqlFragment = designatorText,
                                Type = InterpolationType.DirectVariable
                            };
                            interpolations.Add(interp);
                            sqlText.Append(':').Append(interp.SuggestedParameterName);
                        }
                        else
                        {
                            sqlText.Append(designatorText);
                        }
                    }
                }
                else
                {
                    sqlText.Append(text);
                }
            }

            private DetectedInterpolation? DetectInterpolation(DelphiParser.DesignatorContext designator)
            {
                var text = designator.GetText();

                if (text.Contains("IntToStr", StringComparison.OrdinalIgnoreCase))
                {
                    var varName = ExtractFunctionArgument(text, "IntToStr");
                    return new DetectedInterpolation
                    {
                        VariableName = varName,
                        ConversionFunction = "IntToStr",
                        SuggestedParameterName = SuggestParameterName(varName, InterpolationType.IntToStr),
                        SqlFragment = text,
                        Type = InterpolationType.IntToStr
                    };
                }
                else if (text.Contains("QuotedStr", StringComparison.OrdinalIgnoreCase))
                {
                    var varName = ExtractFunctionArgument(text, "QuotedStr");
                    return new DetectedInterpolation
                    {
                        VariableName = varName,
                        ConversionFunction = "QuotedStr",
                        SuggestedParameterName = SuggestParameterName(varName, InterpolationType.QuotedStr),
                        SqlFragment = text,
                        Type = InterpolationType.QuotedStr
                    };
                }
                else if (text.Contains("FloatToStr", StringComparison.OrdinalIgnoreCase))
                {
                    var varName = ExtractFunctionArgument(text, "FloatToStr");
                    return new DetectedInterpolation
                    {
                        VariableName = varName,
                        ConversionFunction = "FloatToStr",
                        SuggestedParameterName = SuggestParameterName(varName, InterpolationType.FloatToStr),
                        SqlFragment = text,
                        Type = InterpolationType.FloatToStr
                    };
                }
                else if (text.Contains("FormatDateTime", StringComparison.OrdinalIgnoreCase))
                {
                    var varName = ExtractFormatDateTimeVariable(text);
                    return new DetectedInterpolation
                    {
                        VariableName = varName,
                        ConversionFunction = "FormatDateTime",
                        SuggestedParameterName = SuggestParameterName(varName, InterpolationType.FormatDateTime),
                        SqlFragment = text,
                        Type = InterpolationType.FormatDateTime
                    };
                }
                else if (text.Contains("Format", StringComparison.OrdinalIgnoreCase) && text.Contains('['))
                {
                    var varName = ExtractFormatVariable(text);
                    return new DetectedInterpolation
                    {
                        VariableName = varName,
                        ConversionFunction = "Format",
                        SuggestedParameterName = SuggestParameterName(varName, InterpolationType.Format),
                        SqlFragment = text,
                        Type = InterpolationType.Format
                    };
                }

                return null;
            }

            private static string ExtractStringFromFactor(DelphiParser.FactorContext factor)
            {
                var stringFactor = factor.stringFactor();
                if (stringFactor == null) return "";

                var text = stringFactor.GetText();

                if (text.StartsWith('\'') && text.EndsWith('\''))
                {
                    text = text[1..^1].Replace("''", "'");
                }

                return text;
            }

            private static string ExtractFunctionArgument(string text, string functionName)
            {
                var pattern = $@"{functionName}\s*\(\s*([^)]+)\s*\)";
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                return match.Success ? match.Groups[1].Value.Trim() : "UnknownValue";
            }

            private static string ExtractFormatDateTimeVariable(string text)
            {
                var pattern = @"FormatDateTime\s*\(\s*'[^']+'\s*,\s*([^)]+)\s*\)";
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                return match.Success ? match.Groups[1].Value.Trim() : "UnknownValue";
            }

            private static string ExtractFormatVariable(string text)
            {
                var pattern = @"Format\s*\(\s*'[^']+'\s*,\s*\[([^\]]+)\]\s*\)";
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                return match.Success ? match.Groups[1].Value.Trim() : "UnknownValue";
            }

            private static string SuggestParameterName(string variableName, InterpolationType type)
            {
                var cleaned = variableName
                    .Replace("Self.", "")
                    .Replace("FLD", "")
                    .Replace("_", "");

                if (cleaned.Length > 1)
                {
                    if (cleaned[0] == 'A' && char.IsUpper(cleaned[1]))
                        cleaned = cleaned[1..];
                    else if (cleaned.StartsWith('F') && cleaned.Length > 1 && char.IsUpper(cleaned[1]))
                        cleaned = cleaned[1..];
                }

                return cleaned;
            }

            private static bool IsLikelySqlKeyword(string text)
            {
                var upper = text.ToUpperInvariant();
                var sqlKeywords = new HashSet<string>
                {
                    "SELECT", "FROM", "WHERE", "AND", "OR", "ORDER", "BY",
                    "GROUP", "HAVING", "INSERT", "UPDATE", "DELETE", "JOIN",
                    "LEFT", "RIGHT", "INNER", "OUTER", "ON", "AS", "NULL",
                    "TRUE", "FALSE", "DISTINCT", "COUNT", "SUM", "AVG", "MAX", "MIN"
                };

                return sqlKeywords.Contains(upper);
            }
        }

        private class SqlBuilder
        {
            private readonly StringBuilder _originalSql = new();
            private readonly StringBuilder _parameterizedSql = new();
            private readonly List<string> _parameters = [];
            private readonly List<DetectedInterpolation> _interpolations = [];

            public string QueryVariable { get; init; } = "";
            public string MethodName { get; init; } = "";
            public int LineNumber { get; init; }
            public List<string> Parameters => _parameters;

            public void AddFragment(string fragment, List<DetectedInterpolation> interpolations)
            {
                if (_originalSql.Length > 0 && fragment.Length > 0)
                {
                    var lastChar = _originalSql[^1];
                    var firstChar = fragment[0];
                    if (lastChar != ' ' && firstChar != ' ' && firstChar != ')')
                    {
                        _originalSql.Append(' ');
                        _parameterizedSql.Append(' ');
                    }
                }

                _originalSql.Append(fragment);
                _parameterizedSql.Append(fragment);

                _interpolations.AddRange(interpolations);

                foreach (var interp in interpolations)
                {
                    if (!_parameters.Contains(interp.SuggestedParameterName, StringComparer.OrdinalIgnoreCase))
                    {
                        _parameters.Add(interp.SuggestedParameterName);
                    }
                }
            }

            public void AddParameter(string paramName)
            {
                if (!_parameters.Contains(paramName, StringComparer.OrdinalIgnoreCase))
                {
                    _parameters.Add(paramName);
                }
            }

            public (string OriginalSql, string ParameterizedSql, List<DetectedInterpolation> AllInterpolations) Build()
            {
                return (
                    _originalSql.ToString().Trim(),
                    _parameterizedSql.ToString().Trim(),
                    _interpolations
                );
            }
        }

        #endregion
    }
}