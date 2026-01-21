using System.Text;
using System.Text.RegularExpressions;
using DelphiAnalysisMcpServer.Models;

namespace DelphiAnalysisMcpServer.Services;

/// <summary>
/// ENHANCED SQL extraction service that handles both SQL.Add and SQL.Text patterns.
/// Uses line-by-line parsing for maximum reliability.
/// </summary>
public partial class SqlExtractionService
{
    #region Regex Patterns

    [GeneratedRegex(@"'([^']*(?:''[^']*)*)'", RegexOptions.None)]
    private static partial Regex StringLiteralRegex();

    [GeneratedRegex(@"^\s*(SELECT|INSERT|UPDATE|DELETE|EXECUTE|EXEC|CALL|CREATE|ALTER|DROP|SET\s+GENERATOR|UPDATE\s+OR\s+INSERT)",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex SqlKeywordStartRegex();

    #endregion

    /// <summary>
    /// Extracts all SQL queries from a method body.
    /// Returns a list of SQL statements with their positions and whether they're dynamic.
    /// </summary>
    public static List<ExtractedQuery> ExtractQueriesFromMethod(string methodBody, int methodStartLine)
    {
        var queries = new List<ExtractedQuery>();

        // Strip comments first
        var cleanBody = StripComments(methodBody);
        var lines = cleanBody.Split('\n');

        // Find all SQL patterns
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            // Pattern 1: SQL.Clear + SQL.Add sequence
            var clearMatch = Regex.Match(line, @"(\w+)\.(SQL|SelectSQL)\.Clear", RegexOptions.IgnoreCase);
            if (clearMatch.Success)
            {
                var query = ExtractSqlAddSequence(lines, i, clearMatch.Groups[1].Value,
                    clearMatch.Groups[2].Value, methodStartLine);
                if (query != null)
                    queries.Add(query);
                continue;
            }

            // Pattern 2: SQL.Text := or SelectSQL.Text :=
            var textMatch = Regex.Match(line, @"(\w+)\.(SQL|SelectSQL)\.Text\s*:=", RegexOptions.IgnoreCase);
            if (textMatch.Success)
            {
                var query = ExtractSqlTextAssignment(lines, i, textMatch.Groups[1].Value,
                    textMatch.Groups[2].Value, methodStartLine);
                if (query != null)
                    queries.Add(query);
                continue;
            }
        }

        return queries;
    }

    /// <summary>
    /// Extracts SQL from SQL.Clear + SQL.Add sequence.
    /// </summary>
    private static ExtractedQuery? ExtractSqlAddSequence(string[] lines, int startIndex,
        string queryVariable, string sqlProperty, int methodStartLine)
    {
        var sqlParts = new List<string>();
        var hasDynamicParts = false;

        // Collect all subsequent SQL.Add or SelectSQL.Add calls
        for (int j = startIndex + 1; j < lines.Length; j++)
        {
            var nextLine = lines[j].Trim();

            // Match: qr.SQL.Add('text') or qr.SelectSQL.Add('text' + variable)
            var pattern = $@"{Regex.Escape(queryVariable)}\.{Regex.Escape(sqlProperty)}\.Add\s*\(\s*(.+)\s*\)";
            var addMatch = Regex.Match(nextLine, pattern, RegexOptions.IgnoreCase);

            if (addMatch.Success)
            {
                var expression = addMatch.Groups[1].Value.Trim();
                if (expression.EndsWith(';'))
                    expression = expression[..^1].Trim();

                // Try to extract as a simple string literal first
                var literalMatch = StringLiteralRegex().Match(expression);
                if (literalMatch.Success && literalMatch.Value == expression)
                {
                    // It's a pure string literal like 'SELECT'
                    var content = literalMatch.Groups[1].Value.Replace("''", "'");
                    sqlParts.Add(content);
                }
                else if (expression.Contains('+'))
                {
                    // It has concatenation - handle it
                    var (sql, isDynamic) = ProcessConcatenation(expression);
                    sqlParts.Add(sql);
                    if (isDynamic)
                        hasDynamicParts = true;
                }
                else
                {
                    // Malformed or complex expression - mark as dynamic
                    sqlParts.Add($"{{{expression}}}");
                    hasDynamicParts = true;
                }
            }
            else
            {
                // No more SQL.Add calls - end of this SQL block
                break;
            }
        }

        // Create the query if we collected SQL parts
        if (sqlParts.Count > 0)
        {
            var fullSql = string.Join(" ", sqlParts).Trim();

            // Only add if it looks like valid SQL
            if (IsValidSql(fullSql))
            {
                return new ExtractedQuery
                {
                    SqlText = hasDynamicParts ? "Dynamic SQL" : fullSql,
                    IsDynamic = hasDynamicParts,
                    LineNumber = methodStartLine + startIndex,
                    BlockType = SqlBlockType.SqlAddSequence
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts SQL from SQL.Text := assignment.
    /// Handles both single-line and multi-line concatenated assignments.
    /// </summary>
    private static ExtractedQuery? ExtractSqlTextAssignment(string[] lines, int startIndex,
        string queryVariable, string sqlProperty, int methodStartLine)
    {
        // Start from the line with := and collect until semicolon
        var currentLine = lines[startIndex].Trim();
        var assignment = new StringBuilder();

        // Find the := and get everything after it
        var assignMatch = Regex.Match(currentLine, @":=\s*(.+)", RegexOptions.IgnoreCase);
        if (assignMatch.Success)
        {
            assignment.Append(assignMatch.Groups[1].Value);
        }

        // If line doesn't end with semicolon, continue to next lines
        int j = startIndex + 1;
        while (j < lines.Length && !assignment.ToString().TrimEnd().EndsWith(';'))
        {
            var nextLine = lines[j].Trim();
            if (string.IsNullOrEmpty(nextLine))
            {
                j++;
                continue;
            }
            assignment.Append(' ').Append(nextLine);
            j++;
        }

        // Remove trailing semicolon
        var expression = assignment.ToString().Trim();
        if (expression.EndsWith(';'))
            expression = expression[..^1].Trim();

        // Now parse the expression
        var (sql, isDynamic) = ProcessConcatenation(expression);

        if (!string.IsNullOrWhiteSpace(sql) && IsValidSql(sql))
        {
            return new ExtractedQuery
            {
                SqlText = isDynamic ? "Dynamic SQL" : sql,
                IsDynamic = isDynamic,
                LineNumber = methodStartLine + startIndex,
                BlockType = SqlBlockType.SqlTextAssignment
            };
        }

        return null;
    }

    /// <summary>
    /// Processes concatenated expressions like 'SELECT ' + AType + '_SMS_ID'
    /// Returns the SQL text with placeholders and whether it's dynamic.
    /// </summary>
    private static (string sql, bool isDynamic) ProcessConcatenation(string expression)
    {
        var result = new StringBuilder();
        var isDynamic = false;

        // Check if it's a simple string literal first (no concatenation)
        var simpleLiteral = StringLiteralRegex().Match(expression);
        if (simpleLiteral.Success && simpleLiteral.Value == expression)
        {
            return (simpleLiteral.Groups[1].Value.Replace("''", "'"), false);
        }

        // Check for Format() function
        if (expression.StartsWith("Format(", StringComparison.OrdinalIgnoreCase))
        {
            // Format calls are dynamic
            return ("Dynamic SQL", true);
        }

        // Split by + and process parts
        var parts = expression.Split('+');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();

            // Check if it's a string literal
            var literalMatch = StringLiteralRegex().Match(trimmed);
            if (literalMatch.Success)
            {
                // Extract the content and unescape doubled quotes
                var content = literalMatch.Groups[1].Value.Replace("''", "'");
                result.Append(content);
            }
            else
            {
                // It's a variable or expression
                var varName = ExtractVariableName(trimmed);
                if (!string.IsNullOrEmpty(varName))
                {
                    result.Append($":{varName}");
                    isDynamic = true;
                }
                else
                {
                    result.Append($"{{{trimmed}}}");
                    isDynamic = true;
                }
            }
        }

        return (result.ToString(), isDynamic);
    }

    /// <summary>
    /// Extracts the core variable name from expressions like IntToStr(ID), QuotedStr(Name), etc.
    /// </summary>
    private static string ExtractVariableName(string expression)
    {
        expression = expression.Trim();

        // Check for function wrappers like IntToStr(VarName)
        var funcMatch = Regex.Match(expression, @"^\w+\s*\(\s*(\w+)\s*\)$");
        if (funcMatch.Success)
        {
            return funcMatch.Groups[1].Value;
        }

        // Check for property access like MyObject.PropertyName
        var dotMatch = Regex.Match(expression, @"^(\w+)\.(\w+)$");
        if (dotMatch.Success)
        {
            return dotMatch.Groups[2].Value;
        }

        // Simple variable name
        if (Regex.IsMatch(expression, @"^\w+$"))
        {
            return expression;
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks if a string looks like valid SQL.
    /// </summary>
    private static bool IsValidSql(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql) || sql.Length < 6)
        {
            return false;
        }

        return SqlKeywordStartRegex().IsMatch(sql);
    }

    /// <summary>
    /// Strips Delphi comments from source code.
    /// </summary>
    private static string StripComments(string source)
    {
        var result = new StringBuilder(source.Length);
        int i = 0;
        bool inString = false;

        while (i < source.Length)
        {
            // Handle string literals
            if (source[i] == '\'' && (i == 0 || source[i - 1] != '\\'))
            {
                if (!inString)
                {
                    inString = true;
                    result.Append(source[i]);
                    i++;
                    continue;
                }

                // Check for escaped quote ''
                if (i + 1 < source.Length && source[i + 1] == '\'')
                {
                    result.Append("''");
                    i += 2;
                    continue;
                }

                inString = false;
                result.Append(source[i]);
                i++;
                continue;
            }

            if (inString)
            {
                result.Append(source[i]);
                i++;
                continue;
            }

            // Handle // comments
            if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }
                if (i < source.Length)
                {
                    result.Append(source[i]); // Keep newline
                    i++;
                }
                continue;
            }

            // Handle { } comments
            if (source[i] == '{')
            {
                while (i < source.Length && source[i] != '}')
                {
                    i++;
                }
                if (i < source.Length)
                {
                    i++; // Skip }
                }
                continue;
            }

            // Handle (* *) comments
            if (i + 1 < source.Length && source[i] == '(' && source[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == ')'))
                {
                    i++;
                }
                if (i + 1 < source.Length)
                {
                    i += 2; // Skip *)
                }
                continue;
            }

            result.Append(source[i]);
            i++;
        }

        return result.ToString();
    }
}

/// <summary>
/// Represents a block of SQL code in the source.
/// </summary>
public class SqlBlock
{
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public SqlBlockType BlockType { get; set; }
    public List<string> Lines { get; set; } = [];
}

/// <summary>
/// Type of SQL block.
/// </summary>
public enum SqlBlockType
{
    SqlAddSequence,      // Query.SQL.Add sequence
    SqlTextAssignment,   // Query.SQL.Text := 
    DirectExecute,       // ExecuteQuery()
    QueryValue          // QueryValueAsXXX()
}

/// <summary>
/// Represents an extracted SQL query.
/// </summary>
public class ExtractedQuery
{
    public string SqlText { get; set; } = string.Empty;
    public bool IsDynamic { get; set; }
    public int LineNumber { get; set; }
    public SqlBlockType BlockType { get; set; }
}