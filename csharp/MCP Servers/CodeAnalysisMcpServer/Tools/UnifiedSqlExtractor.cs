using System.Text;
using System.Text.RegularExpressions;
using SfD.Global.Logging;

namespace CodeAnalysisMcpServer.Tools;

/// <summary>
/// Unified SQL extractor that handles both .pas and .dfm files.
/// Prevents duplicates by prioritizing runtime-assembled patterns over regex fragments.
/// </summary>
public partial class UnifiedSqlExtractor
{
    private readonly SfdLogger _logger;
    private readonly DelphiSqlPatternExtractor _patternExtractor;

    public UnifiedSqlExtractor(SfdLogger logger, DelphiSqlPatternExtractor patternExtractor)
    {
        _logger = logger;
        _patternExtractor = patternExtractor;
    }

    public record ExtractedSqlStatement
    {
        public string FileName { get; init; } = "";
        public string SourceFile { get; init; } = "";
        public string SourceType { get; init; } = ""; // "pas" or "dfm"
        public string Method { get; init; } = "";
        public int LineNumber { get; init; }
        public string SqlStatement { get; init; } = "";
        public List<string> Parameters { get; init; } = [];
        public List<string> Tables { get; init; } = [];
        public List<string> Columns { get; init; } = [];
        public string SqlType { get; init; } = "";
        public string ExtractionMethod { get; init; } = ""; // "RuntimePattern", "SingleLineString", "DfmComponent"
        public string? ComponentName { get; init; }
        public string? ComponentType { get; init; }
        public string? QueryVariable { get; init; }
    }

    /// <summary>
    /// Extract SQL from a single .pas file using multi-phase approach
    /// </summary>
    public List<ExtractedSqlStatement> ExtractFromPasFile(string filePath)
    {
        var results = new List<ExtractedSqlStatement>();

        if (!File.Exists(filePath))
            return results;

        var content = File.ReadAllText(filePath);
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // PHASE 1: Extract runtime-assembled SQL patterns (SQL.Clear + multiple SQL.Add)
        var runtimeSql = _patternExtractor.ExtractFromFile(filePath);
        var excludedRanges = new List<(int start, int end)>();

        foreach (var sql in runtimeSql)
        {
            // Record the range that was processed by the pattern extractor
            var startPos = FindPatternStartPosition(content, sql);
            var endPos = FindPatternEndPosition(content, sql, startPos);

            if (startPos >= 0 && endPos > startPos)
            {
                excludedRanges.Add((startPos, endPos));
            }

            var tables = ExtractTableNamesFromSql(sql.SqlStatement);
            var columns = ExtractSelectedColumns(sql.SqlStatement);

            results.Add(new ExtractedSqlStatement
            {
                FileName = fileName,
                SourceFile = sql.FileName,
                SourceType = "pas",
                Method = sql.MethodName,
                LineNumber = sql.LineNumber,
                SqlStatement = sql.SqlStatement,
                Parameters = sql.Parameters,
                Tables = tables,
                Columns = columns,
                SqlType = DetermineSqlType(sql.SqlStatement),
                ExtractionMethod = "RuntimePattern",
                QueryVariable = sql.QueryVariable
            });
        }

        // PHASE 2: Extract single-line SQL strings that are NOT part of runtime patterns
        // Only look for standalone SQL assignments like: SQL.Text := 'SELECT...'
        var singleLineSql = ExtractSingleLineSqlStatements(content, excludedRanges);

        foreach (var sql in singleLineSql)
        {
            var tables = ExtractTableNamesFromSql(sql.SqlStatement);
            var columns = ExtractSelectedColumns(sql.SqlStatement);

            results.Add(new ExtractedSqlStatement
            {
                FileName = fileName,
                SourceFile = Path.GetFileName(filePath),
                SourceType = "pas",
                Method = sql.Method,
                LineNumber = sql.LineNumber,
                SqlStatement = sql.SqlStatement,
                Parameters = [],
                Tables = tables,
                Columns = columns,
                SqlType = DetermineSqlType(sql.SqlStatement),
                ExtractionMethod = "SingleLineString"
            });
        }

        return results;
    }

    /// <summary>
    /// Extract SQL from a single .dfm file (design-time components)
    /// </summary>
    public async Task<List<ExtractedSqlStatement>> ExtractFromDfmFileAsync(string dfmFilePath)
    {
        var results = new List<ExtractedSqlStatement>();

        if (!File.Exists(dfmFilePath))
            return results;

        var content = await File.ReadAllTextAsync(dfmFilePath);
        var fileName = Path.GetFileNameWithoutExtension(dfmFilePath);

        // Pattern to find SQL.Text properties in .dfm files
        // Format in .dfm: SQL.Strings = ( 'SELECT...' 'FROM...' )
        var componentPattern = @"object\s+(\w+)\s*:\s*(T\w*Query|T\w*Table|T\w*StoredProc)";
        var componentMatches = Regex.Matches(content, componentPattern, RegexOptions.IgnoreCase);

        foreach (Match componentMatch in componentMatches)
        {
            var componentName = componentMatch.Groups[1].Value;
            var componentType = componentMatch.Groups[2].Value;

            // Find the SQL.Strings block for this component
            var sqlBlock = ExtractSqlFromDfmComponent(content, componentMatch.Index);

            if (!string.IsNullOrWhiteSpace(sqlBlock))
            {
                var tables = ExtractTableNamesFromSql(sqlBlock);
                var columns = ExtractSelectedColumns(sqlBlock);

                results.Add(new ExtractedSqlStatement
                {
                    FileName = fileName,
                    SourceFile = Path.GetFileName(dfmFilePath),
                    SourceType = "dfm",
                    Method = "N/A (design-time)",
                    LineNumber = GetLineNumber(content, componentMatch.Index),
                    SqlStatement = sqlBlock,
                    Parameters = [],
                    Tables = tables,
                    Columns = columns,
                    SqlType = DetermineSqlType(sqlBlock),
                    ExtractionMethod = "DfmComponent",
                    ComponentName = componentName,
                    ComponentType = componentType
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Extract SQL from all .pas and .dfm files in a directory
    /// </summary>
    public async Task<List<ExtractedSqlStatement>> ExtractFromDirectoryAsync(
        string directoryPath,
        bool includePas = true,
        bool includeDfm = true)
    {
        var results = new List<ExtractedSqlStatement>();

        if (!Directory.Exists(directoryPath))
            return results;

        // Process .pas files
        if (includePas)
        {
            var pasFiles = Directory.GetFiles(directoryPath, "*.pas", SearchOption.AllDirectories);

            foreach (var file in pasFiles)
            {
                try
                {
                    var extracted = ExtractFromPasFile(file);
                    results.AddRange(extracted);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing {file}: {ex.Message}");
                }
            }
        }

        // Process .dfm files
        if (includeDfm)
        {
            var dfmFiles = Directory.GetFiles(directoryPath, "*.dfm", SearchOption.AllDirectories);

            foreach (var file in dfmFiles)
            {
                try
                {
                    var extracted = await ExtractFromDfmFileAsync(file);
                    results.AddRange(extracted);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error processing {file}: {ex.Message}");
                }
            }
        }

        return results;
    }

    // ========== PRIVATE HELPER METHODS ==========

    private int FindPatternStartPosition(string content, DelphiSqlPatternExtractor.ExtractedSql sql)
    {
        // Find the SQL.Clear statement that starts this pattern
        var searchText = $"{sql.QueryVariable}.SQL.Clear";
        var pos = content.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);

        if (pos < 0)
        {
            // Fallback: search around the reported line number
            var lines = content.Split('\n');
            var lineStart = 0;
            for (int i = 0; i < Math.Min(sql.LineNumber - 1, lines.Length); i++)
            {
                lineStart += lines[i].Length + 1;
            }
            pos = lineStart;
        }

        return pos;
    }

    private int FindPatternEndPosition(string content, DelphiSqlPatternExtractor.ExtractedSql sql, int startPos)
    {
        // Find the execute statement or the last SQL.Add for this query
        var pattern = $@"{Regex.Escape(sql.QueryVariable)}\.(?:SQL\.Add|ExecQuery|Open|OpenCursor)";
        var matches = Regex.Matches(content.Substring(startPos), pattern, RegexOptions.IgnoreCase);

        if (matches.Count > 0)
        {
            var lastMatch = matches[matches.Count - 1];
            return startPos + lastMatch.Index + lastMatch.Length;
        }

        // Fallback: use a fixed range
        return startPos + 500;
    }

    private record SingleLineSqlInfo
    {
        public string SqlStatement { get; init; } = "";
        public string Method { get; init; } = "";
        public int LineNumber { get; init; }
    }

    private List<SingleLineSqlInfo> ExtractSingleLineSqlStatements(string content, List<(int start, int end)> excludedRanges)
    {
        var results = new List<SingleLineSqlInfo>();

        // Pattern for single-line SQL assignments (not SQL.Add patterns)
        // SQL.Text := 'SELECT...'
        // CommandText := 'UPDATE...'
        var patterns = new[]
        {
            @"\.(?:SQL\.Text|CommandText|Text)\s*:=\s*'([^']+(?:''[^']+)*)'",
            @"ExecSQL\s*\(\s*'([^']+(?:''[^']+)*)'\s*\)"
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                var matchPos = match.Index;

                // Check if this position is in an excluded range (already processed by pattern extractor)
                if (IsInExcludedRange(matchPos, excludedRanges))
                    continue;

                var sqlFragment = match.Groups[1].Value;
                sqlFragment = sqlFragment.Replace("''", "'"); // Handle Delphi escaped quotes

                // Only include if it looks like a complete SQL statement
                if (IsCompleteSqlStatement(sqlFragment))
                {
                    var method = FindContainingMethod(content, matchPos);
                    var lineNumber = GetLineNumber(content, matchPos);

                    results.Add(new SingleLineSqlInfo
                    {
                        SqlStatement = sqlFragment,
                        Method = method,
                        LineNumber = lineNumber
                    });
                }
            }
        }

        return results;
    }

    private bool IsInExcludedRange(int position, List<(int start, int end)> excludedRanges)
    {
        return excludedRanges.Any(range => position >= range.start && position <= range.end);
    }

    private bool IsCompleteSqlStatement(string sql)
    {
        var upperSql = sql.TrimStart().ToUpperInvariant();
        return upperSql.StartsWith("SELECT") ||
               upperSql.StartsWith("INSERT") ||
               upperSql.StartsWith("UPDATE") ||
               upperSql.StartsWith("DELETE") ||
               upperSql.StartsWith("EXECUTE") ||
               upperSql.StartsWith("EXEC ");
    }

    private string ExtractSqlFromDfmComponent(string content, int componentStartPos)
    {
        // Find the SQL.Strings property block
        // Format: SQL.Strings = ( 'line1' 'line2' 'line3' )
        var sqlStringsPattern = @"SQL\.Strings\s*=\s*\(\s*((?:'[^']*(?:''[^']*)*'\s*)+)\)";
        var match = Regex.Match(content.Substring(componentStartPos, Math.Min(5000, content.Length - componentStartPos)),
                               sqlStringsPattern,
                               RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (!match.Success)
            return "";

        // Extract all string literals and concatenate them
        var stringLiteralsPattern = @"'([^']*(?:''[^']*)*)'";
        var stringMatches = Regex.Matches(match.Groups[1].Value, stringLiteralsPattern);

        var sql = new StringBuilder();
        foreach (Match stringMatch in stringMatches)
        {
            var fragment = stringMatch.Groups[1].Value.Replace("''", "'");

            if (sql.Length > 0)
            {
                var lastChar = sql[sql.Length - 1];
                var firstChar = fragment.Length > 0 ? fragment[0] : ' ';

                if (lastChar != ' ' && firstChar != ' ')
                    sql.Append(' ');
            }

            sql.Append(fragment);
        }

        return sql.ToString().Trim();
    }

    private string FindContainingMethod(string content, int position)
    {
        var methodRegex = new Regex(@"(?:procedure|function)\s+(\w+(?:\.\w+)?)\s*(?:\(|;)", RegexOptions.IgnoreCase);
        var searchStart = Math.Max(0, position - 5000);
        var searchText = content.Substring(searchStart, position - searchStart);
        var matches = methodRegex.Matches(searchText);

        if (matches.Count > 0)
        {
            var lastMatch = matches[matches.Count - 1];
            return lastMatch.Groups[1].Value;
        }

        return "Unknown";
    }

    private int GetLineNumber(string content, int position)
    {
        var lineNumber = 1;
        for (int i = 0; i < position && i < content.Length; i++)
        {
            if (content[i] == '\n')
                lineNumber++;
        }
        return lineNumber;
    }

    [GeneratedRegex(@"\b(?:FROM|JOIN)\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex FromJoinRegex();

    [GeneratedRegex(@"\bINTO\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex IntoRegex();

    [GeneratedRegex(@"\bUPDATE\s+(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex UpdateRegex();

    private List<string> ExtractTableNamesFromSql(string sql)
    {
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // FROM and JOIN clauses
        var fromJoinMatches = FromJoinRegex().Matches(sql);
        foreach (Match match in fromJoinMatches)
        {
            tables.Add(match.Groups[1].Value);
        }

        // INTO clause (INSERT statements)
        var intoMatches = IntoRegex().Matches(sql);
        foreach (Match match in intoMatches)
        {
            tables.Add(match.Groups[1].Value);
        }

        // UPDATE statements
        var updateMatches = UpdateRegex().Matches(sql);
        foreach (Match match in updateMatches)
        {
            tables.Add(match.Groups[1].Value);
        }

        return tables.ToList();
    }

    [GeneratedRegex(@"SELECT\s+(.*?)\s+FROM", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SelectColumnsRegex();

    private List<string> ExtractSelectedColumns(string sql)
    {
        var columns = new List<string>();

        if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return columns;

        var match = SelectColumnsRegex().Match(sql);
        if (!match.Success)
            return columns;

        var columnList = match.Groups[1].Value;

        if (columnList.Trim() == "*")
            return ["*"];

        var parts = columnList.Split(',');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();

            // Handle "column AS alias" or "table.column"
            var asIndex = trimmed.IndexOf(" AS ", StringComparison.OrdinalIgnoreCase);
            if (asIndex > 0)
            {
                trimmed = trimmed.Substring(0, asIndex).Trim();
            }

            var dotIndex = trimmed.LastIndexOf('.');
            if (dotIndex > 0)
            {
                trimmed = trimmed.Substring(dotIndex + 1);
            }

            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                columns.Add(trimmed);
            }
        }

        return columns;
    }

    private string DetermineSqlType(string sql)
    {
        var upperSql = sql.TrimStart().ToUpperInvariant();

        if (upperSql.StartsWith("SELECT")) return "SELECT";
        if (upperSql.StartsWith("INSERT")) return "INSERT";
        if (upperSql.StartsWith("UPDATE")) return "UPDATE";
        if (upperSql.StartsWith("DELETE")) return "DELETE";
        if (upperSql.StartsWith("EXECUTE") || upperSql.StartsWith("EXEC ")) return "EXECUTE";

        return "UNKNOWN";
    }

    /// <summary>
    /// Generate a summary report grouping by extraction method, SQL type, and file
    /// </summary>
    public string GenerateSummaryReport(List<ExtractedSqlStatement> statements)
    {
        var report = new StringBuilder();

        report.AppendLine("# Unified SQL Extraction Report");
        report.AppendLine();
        report.AppendLine($"**Total SQL Statements:** {statements.Count}");
        report.AppendLine();

        // Group by extraction method
        var byMethod = statements.GroupBy(s => s.ExtractionMethod);
        report.AppendLine("## By Extraction Method");
        report.AppendLine();
        foreach (var group in byMethod)
        {
            report.AppendLine($"- **{group.Key}:** {group.Count()} statements");
        }
        report.AppendLine();

        // Group by SQL type
        var byType = statements.GroupBy(s => s.SqlType);
        report.AppendLine("## By SQL Type");
        report.AppendLine();
        foreach (var group in byType)
        {
            report.AppendLine($"- **{group.Key}:** {group.Count()} statements");
        }
        report.AppendLine();

        // Top tables
        var tableCounts = statements
            .SelectMany(s => s.Tables)
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .Take(10);

        report.AppendLine("## Top 10 Most Referenced Tables");
        report.AppendLine();
        foreach (var table in tableCounts)
        {
            report.AppendLine($"- **{table.Key}:** {table.Count()} references");
        }
        report.AppendLine();

        // Files with most SQL
        var fileGroups = statements
            .GroupBy(s => s.SourceFile)
            .OrderByDescending(g => g.Count())
            .Take(10);

        report.AppendLine("## Top 10 Files with Most SQL");
        report.AppendLine();
        foreach (var fileGroup in fileGroups)
        {
            report.AppendLine($"- **{fileGroup.Key}:** {fileGroup.Count()} statements");
        }

        return report.ToString();
    }
}