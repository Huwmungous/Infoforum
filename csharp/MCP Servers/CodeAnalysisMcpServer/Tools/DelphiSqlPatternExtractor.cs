using System.Text;
using System.Text.RegularExpressions;
using SfD.Global.Logging;

namespace CodeAnalysisMcpServer.Tools
{

    /// <summary>
    /// Extracts SQL statements that are assembled at runtime using SQL.Add() patterns
    /// </summary>
    public partial class DelphiSqlPatternExtractor
    {
        private SfdLogger _logger;

        public DelphiSqlPatternExtractor(SfdLogger logger) { _logger = logger; }

        // FIXED: Now matches both .SQL.Clear; and .SQL.Clear();
        [GeneratedRegex(@"\.SQL\.Clear\s*(?:\(\s*\))?\s*;", RegexOptions.IgnoreCase)]
        private static partial Regex SqlClearRegex();

        [GeneratedRegex(@"\.SQL\.Add\s*\(\s*'([^']*(?:''[^']*)*)'\s*\)", RegexOptions.IgnoreCase)]
        private static partial Regex SqlAddRegex();

        [GeneratedRegex(@"\.(ExecQuery|Open|OpenCursor)\s*;", RegexOptions.IgnoreCase)]
        private static partial Regex SqlExecuteRegex();

        [GeneratedRegex(@"(qr|query|qry|Query|FIBQuery|IBQuery|ADOQuery)\.SQL", RegexOptions.IgnoreCase)]
        private static partial Regex QueryVariableRegex();

        public record ExtractedSql
        {
            public string FileName { get; init; } = "";
            public string MethodName { get; init; } = "";
            public int LineNumber { get; init; }
            public string SqlStatement { get; init; } = "";
            public List<string> Parameters { get; init; } = [];
            public string QueryVariable { get; init; } = "";
        }

        public List<ExtractedSql> ExtractFromFile(string filePath)
        {
            var results = new List<ExtractedSql>();

            if (!File.Exists(filePath))
                return results;

            var content = File.ReadAllText(filePath);
            var fileName = Path.GetFileName(filePath);

            // Find all SQL.Clear -> SQL.Add sequences
            var sqlBlocks = FindSqlBlocks(content);

            foreach (var block in sqlBlocks)
            {
                var sql = ReconstructSql(block);
                var parameters = ExtractParameters(block.Content);
                var methodName = FindContainingMethod(content, block.StartPosition);

                results.Add(new ExtractedSql
                {
                    FileName = fileName,
                    MethodName = methodName,
                    LineNumber = GetLineNumber(content, block.StartPosition),
                    SqlStatement = sql,
                    Parameters = parameters,
                    QueryVariable = block.QueryVariable
                });
            }

            return results;
        }

        public List<ExtractedSql> ExtractFromDirectory(string directoryPath, string searchPattern = "*.pas")
        {
            var results = new List<ExtractedSql>();

            if (!Directory.Exists(directoryPath))
                return results;

            var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.AllDirectories);

            foreach (var file in files)
            {
                try
                {
                    var extracted = ExtractFromFile(file);
                    results.AddRange(extracted);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing {file}: {ex.Message}");
                }
            }

            return results;
        }

        private record SqlBlock
        {
            public int StartPosition { get; init; }
            public int EndPosition { get; init; }
            public string Content { get; init; } = "";
            public string QueryVariable { get; init; } = "";
        }

        private List<SqlBlock> FindSqlBlocks(string content)
        {
            var blocks = new List<SqlBlock>();
            var clearMatches = SqlClearRegex().Matches(content);

            foreach (Match clearMatch in clearMatches)
            {
                var startPos = clearMatch.Index;

                // Find the query variable name (qr, query, etc.)
                var queryVar = ExtractQueryVariable(content, startPos);
                if (string.IsNullOrEmpty(queryVar))
                    continue;

                // Find all SQL.Add calls for this query variable
                var addMatches = new List<Match>();
                var currentPos = startPos;
                var pattern = new Regex($@"{Regex.Escape(queryVar)}\.SQL\.Add\s*\(\s*'([^']*(?:''[^']*)*)'\s*\)", RegexOptions.IgnoreCase);

                // Scan forward to find consecutive SQL.Add calls
                var scanPos = clearMatch.Index + clearMatch.Length;
                var endPos = scanPos;

                while (scanPos < content.Length)
                {
                    // Skip whitespace and comments
                    scanPos = SkipWhitespaceAndComments(content, scanPos);
                    if (scanPos >= content.Length)
                        break;

                    // Check for SQL.Add
                    var addMatch = pattern.Match(content, scanPos);
                    if (addMatch.Success && addMatch.Index == scanPos)
                    {
                        addMatches.Add(addMatch);
                        endPos = addMatch.Index + addMatch.Length;
                        scanPos = endPos;
                    }
                    else
                    {
                        // Check if we hit an execute statement
                        var executeMatch = SqlExecuteRegex().Match(content, scanPos);
                        if (executeMatch.Success && executeMatch.Index < scanPos + 100)
                        {
                            endPos = executeMatch.Index + executeMatch.Length;
                            break;
                        }

                        // If we don't find SQL.Add or Execute in the next ~200 chars, stop
                        if (scanPos - endPos > 200)
                            break;

                        scanPos++;
                    }
                }

                if (addMatches.Count > 0)
                {
                    blocks.Add(new SqlBlock
                    {
                        StartPosition = startPos,
                        EndPosition = endPos,
                        Content = content.Substring(startPos, endPos - startPos),
                        QueryVariable = queryVar
                    });
                }
            }

            return blocks;
        }

        private string ExtractQueryVariable(string content, int position)
        {
            // Look backwards from .SQL.Clear to find the query variable name
            var searchStart = Math.Max(0, position - 50);
            var searchText = content.Substring(searchStart, position - searchStart + 11); // +11 for ".SQL.Clear"

            var match = QueryVariableRegex().Match(searchText);
            if (match.Success)
            {
                var varName = match.Groups[1].Value;
                return varName;
            }

            return "";
        }

        private string ReconstructSql(SqlBlock block)
        {
            var sql = new StringBuilder();
            var addMatches = SqlAddRegex().Matches(block.Content);

            foreach (Match match in addMatches)
            {
                var sqlFragment = match.Groups[1].Value;

                // Handle escaped single quotes ('' in Delphi)
                sqlFragment = sqlFragment.Replace("''", "'");

                // Add the fragment with appropriate spacing
                if (sql.Length > 0)
                {
                    // Add space if the previous line didn't end with space and current doesn't start with space
                    var lastChar = sql.Length > 0 ? sql[sql.Length - 1] : ' ';
                    var firstChar = sqlFragment.Length > 0 ? sqlFragment[0] : ' ';

                    if (lastChar != ' ' && firstChar != ' ' && firstChar != ')')
                    {
                        sql.Append(' ');
                    }
                }

                sql.Append(sqlFragment);
            }

            return sql.ToString().Trim();
        }

        private List<string> ExtractParameters(string content)
        {
            var parameters = new List<string>();
            var paramRegex = new Regex(@"ParamByName\s*\(\s*'([^']+)'\s*\)", RegexOptions.IgnoreCase);
            var matches = paramRegex.Matches(content);

            foreach (Match match in matches)
            {
                var paramName = match.Groups[1].Value;
                if (!parameters.Contains(paramName, StringComparer.OrdinalIgnoreCase))
                {
                    parameters.Add(paramName);
                }
            }

            return parameters;
        }

        private string FindContainingMethod(string content, int position)
        {
            // Look backwards for procedure/function declaration
            var methodRegex = new Regex(@"(?:procedure|function)\s+(\w+(?:\.\w+)?)\s*(?:\(|;)", RegexOptions.IgnoreCase);

            var searchStart = Math.Max(0, position - 5000); // Look back up to 5000 chars
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

        private int SkipWhitespaceAndComments(string content, int position)
        {
            while (position < content.Length)
            {
                var ch = content[position];

                // Skip whitespace
                if (char.IsWhiteSpace(ch))
                {
                    position++;
                    continue;
                }

                // Skip single-line comments //
                if (position + 1 < content.Length && content[position] == '/' && content[position + 1] == '/')
                {
                    while (position < content.Length && content[position] != '\n')
                        position++;
                    continue;
                }

                // Skip block comments { }
                if (ch == '{')
                {
                    while (position < content.Length && content[position] != '}')
                        position++;
                    if (position < content.Length)
                        position++; // Skip the closing }
                    continue;
                }

                // Skip block comments (* *)
                if (position + 1 < content.Length && content[position] == '(' && content[position + 1] == '*')
                {
                    position += 2;
                    while (position + 1 < content.Length)
                    {
                        if (content[position] == '*' && content[position + 1] == ')')
                        {
                            position += 2;
                            break;
                        }
                        position++;
                    }
                    continue;
                }

                // Not whitespace or comment
                break;
            }

            return position;
        }

        public string GenerateReport(List<ExtractedSql> sqlStatements)
        {
            var report = new StringBuilder();

            report.AppendLine("# Extracted SQL Statements from Runtime Assembly");
            report.AppendLine();
            report.AppendLine($"Total statements found: {sqlStatements.Count}");
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
                    report.AppendLine("**SQL:**");
                    report.AppendLine("```sql");
                    report.AppendLine(sql.SqlStatement);
                    report.AppendLine("```");
                    report.AppendLine();

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

        public string GenerateJsonOutput(List<ExtractedSql> sqlStatements)
        {
            var output = sqlStatements.Select(s => new
            {
                file = s.FileName,
                method = s.MethodName,
                line = s.LineNumber,
                queryVariable = s.QueryVariable,
                sql = s.SqlStatement,
                parameters = s.Parameters,
                sqlType = DetermineSqlType(s.SqlStatement)
            });

            return System.Text.Json.JsonSerializer.Serialize(output, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private string DetermineSqlType(string sql)
        {
            var upperSql = sql.TrimStart().ToUpperInvariant();

            if (upperSql.StartsWith("SELECT"))
                return "SELECT";
            if (upperSql.StartsWith("INSERT"))
                return "INSERT";
            if (upperSql.StartsWith("UPDATE"))
                return "UPDATE";
            if (upperSql.StartsWith("DELETE"))
                return "DELETE";
            if (upperSql.StartsWith("EXECUTE"))
                return "EXECUTE";

            return "UNKNOWN";
        }
    }

}