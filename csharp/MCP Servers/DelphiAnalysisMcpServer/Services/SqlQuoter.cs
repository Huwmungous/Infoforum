using System.Text;
using System.Text.RegularExpressions;

namespace DelphiAnalysisMcpServer.Services;

/// <summary>
/// Utility for quoting SQL reserved words in identifiers.
/// Firebird/Interbase requires double-quotes around reserved words when used as column/table names.
/// </summary>
public static partial class SqlQuoter
{
    /// <summary>
    /// SQL reserved words that must be quoted when used as identifiers.
    /// This is a minimal list of words that will cause syntax errors in Firebird/Interbase.
    /// </summary>
    private static readonly HashSet<string> ReservedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // SQL Standard reserved words that cause syntax errors
        "ADD", "ALL", "ALTER", "AND", "ANY", "AS", "ASC", "AT",
        "BEGIN", "BETWEEN", "BLOB", "BY",
        "CASE", "CAST", "CHAR", "CHARACTER", "CHECK", "CLOSE", "COLLATE", "COLUMN", "COMMIT",
        "CONNECT", "CONSTRAINT", "COUNT", "CREATE", "CROSS", "CURRENT", "CURRENT_DATE",
        "CURRENT_TIME", "CURRENT_TIMESTAMP", "CURRENT_USER", "CURSOR",
        "DATE", "DAY", "DEC", "DECIMAL", "DECLARE", "DEFAULT", "DELETE", "DESC", "DISTINCT",
        "DOUBLE", "DROP",
        "ELSE", "END", "ESCAPE", "EXECUTE", "EXISTS", "EXTERNAL", "EXTRACT",
        "FALSE", "FETCH", "FLOAT", "FOR", "FOREIGN", "FROM", "FULL", "FUNCTION",
        "GRANT", "GROUP",
        "HAVING", "HOUR",
        "IN", "INDEX", "INNER", "INSERT", "INT", "INTEGER", "INTO", "IS",
        "JOIN",
        "KEY",
        "LEFT", "LIKE",
        "MAX", "MIN", "MINUTE", "MONTH",
        "NATURAL", "NO", "NOT", "NULL", "NUMERIC",
        "OF", "ON", "ONLY", "OPEN", "OR", "ORDER", "OUTER",
        "POSITION", "PRECISION", "PRIMARY", "PROCEDURE", "PUBLIC",
        "REAL", "REFERENCES", "RELEASE", "RETURN", "RETURNS", "REVOKE", "RIGHT", "ROLLBACK", "ROW", "ROWS",
        "SECOND", "SELECT", "SET", "SMALLINT", "SOME", "SUM",
        "TABLE", "THEN", "TIME", "TIMESTAMP", "TO", "TRIGGER", "TRIM", "TRUE",
        "UNION", "UNIQUE", "UPDATE", "UPPER", "USER", "USING",
        "VALUE", "VALUES", "VARCHAR", "VARYING", "VIEW",
        "WHEN", "WHERE", "WITH",
        "YEAR",
        
        // Firebird/Interbase specific reserved words
        "ACTION", "ACTIVE", "AFTER", "ASCENDING", "AVG",
        "BEFORE", "BREAK",
        "CASCADE", "COALESCE", "COMPUTED", "CONTAINING",
        "DATABASE", "DESCENDING", "DO", "DOMAIN",
        "ENTRY_POINT", "EXCEPTION", "EXIT",
        "FILE", "FILTER", "FIRST", "FREE",
        "GEN_ID", "GENERATOR", "GLOBAL",
        "IF", "IIF", "INACTIVE", "INPUT",
        "LAST", "LENGTH", "LEVEL", "LOCK", "LONG",
        "MANUAL", "MERGE", "MODULE_NAME",
        "NAMES", "NEXT", "NULLIF", "NULLS",
        "OPTION", "OUTPUT", "OVER", "OVERFLOW",
        "PAGE", "PAGES", "PARAMETER", "PASSWORD", "PLAN", "POST_EVENT", "PRIVILEGES",
        "RECREATE", "RESERVING", "RESTRICT", "RETAIN", "RETURNING", "RETURNING_VALUES", "ROLE",
        "SCHEMA", "SEGMENT", "SEQUENCE", "SHADOW", "SHARED", "SINGULAR", "SKIP", "SNAPSHOT", "SORT",
        "SQLCODE", "STABILITY", "STARTING", "STATISTICS", "SUB_TYPE", "SUSPEND",
        "TRANSACTION", "TYPE",
        "UNCOMMITTED",
        "WAIT", "WEEKDAY", "WORK",
        "YEARDAY"
    };

    // Regex patterns for SQL parsing
    [GeneratedRegex(@"\bSELECT\s+(.+?)\s+FROM\b", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SelectColumnsRegex();

    [GeneratedRegex(@"(\bFROM\s+)([A-Za-z_]\w*)(\s|$|\)|,)", RegexOptions.IgnoreCase)]
    private static partial Regex TableFromRegex();

    [GeneratedRegex(@"(\bJOIN\s+)([A-Za-z_]\w*)(\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex TableJoinRegex();

    [GeneratedRegex(@"(\bINTO\s+)([A-Za-z_]\w*)(\s|\(|$)", RegexOptions.IgnoreCase)]
    private static partial Regex TableIntoRegex();

    [GeneratedRegex(@"(\bUPDATE\s+(?:OR\s+INSERT\s+INTO\s+)?)([A-Za-z_]\w*)(\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex TableUpdateRegex();

    [GeneratedRegex(@"(\bDELETE\s+FROM\s+)([A-Za-z_]\w*)(\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex TableDeleteRegex();

    /// <summary>
    /// Quotes a single identifier if it's a reserved word.
    /// </summary>
    public static string QuoteIfReserved(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
            return identifier;
            
        // Already quoted
        if (identifier.StartsWith('"') && identifier.EndsWith('"'))
            return identifier;
            
        // Check if it's a reserved word
        if (ReservedWords.Contains(identifier))
            return $"\"{identifier}\"";
            
        return identifier;
    }

    /// <summary>
    /// Processes a SQL statement and quotes reserved words used as identifiers.
    /// </summary>
    public static string QuoteReservedWords(string sql)
    {
        if (string.IsNullOrEmpty(sql))
            return sql;

        var result = sql;
        
        // Quote columns in SELECT clause
        result = QuoteSelectColumns(result);
        
        // Quote table names
        result = QuoteTableNames(result);
        
        // Quote columns in WHERE, SET, ORDER BY clauses
        result = QuoteWhereAndSetColumns(result);
        
        return result;
    }

    private static string QuoteSelectColumns(string sql)
    {
        var match = SelectColumnsRegex().Match(sql);
        if (!match.Success)
            return sql;

        var columnsPart = match.Groups[1].Value;
        var quotedColumns = QuoteColumnList(columnsPart);
        
        return sql[..match.Groups[1].Index] + 
               quotedColumns + 
               sql[(match.Groups[1].Index + match.Groups[1].Length)..];
    }

    private static string QuoteColumnList(string columnList)
    {
        // Split by comma, but be careful of functions like COALESCE(a, b)
        var columns = new List<string>();
        var current = new StringBuilder();
        var parenDepth = 0;
        
        foreach (var c in columnList)
        {
            if (c == '(') parenDepth++;
            else if (c == ')') parenDepth--;
            else if (c == ',' && parenDepth == 0)
            {
                columns.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }
            current.Append(c);
        }
        if (current.Length > 0)
            columns.Add(current.ToString().Trim());

        return string.Join(", ", columns.Select(QuoteSingleColumn));
    }

    private static string QuoteSingleColumn(string column)
    {
        var trimmed = column.Trim();
        
        // Handle * 
        if (trimmed == "*")
            return trimmed;
            
        // Handle aliases: COLUMN AS ALIAS or just COLUMN ALIAS
        var asMatch = Regex.Match(trimmed, @"^(.+?)\s+(?:AS\s+)?([A-Za-z_]\w*)$", RegexOptions.IgnoreCase);
        if (asMatch.Success && !trimmed.Contains('('))
        {
            var colPart = QuoteIdentifierPart(asMatch.Groups[1].Value.Trim());
            var aliasPart = QuoteIfReserved(asMatch.Groups[2].Value.Trim());
            return $"{colPart} AS {aliasPart}";
        }
        
        // Handle table.column (but not already quoted)
        if (trimmed.Contains('.') && !trimmed.StartsWith('"'))
        {
            var parts = trimmed.Split('.');
            return string.Join(".", parts.Select(p => QuoteIdentifierPart(p.Trim())));
        }
        
        // Simple column name
        return QuoteIdentifierPart(trimmed);
    }

    private static string QuoteIdentifierPart(string part)
    {
        // Already quoted
        if (part.StartsWith('"') && part.EndsWith('"'))
            return part;
            
        // Is it a function call, expression, or *?
        if (part.Contains('(') || part.Contains(' ') || part == "*")
            return part;
            
        return QuoteIfReserved(part);
    }

    private static string QuoteTableNames(string sql)
    {
        var result = sql;
        
        // Quote table after FROM
        result = TableFromRegex().Replace(result, m => 
            m.Groups[1].Value + QuoteIfReserved(m.Groups[2].Value) + m.Groups[3].Value);
        
        // Quote table after JOIN
        result = TableJoinRegex().Replace(result, m => 
            m.Groups[1].Value + QuoteIfReserved(m.Groups[2].Value) + m.Groups[3].Value);
        
        // Quote table after INTO
        result = TableIntoRegex().Replace(result, m => 
            m.Groups[1].Value + QuoteIfReserved(m.Groups[2].Value) + m.Groups[3].Value);
        
        // Quote table after UPDATE
        result = TableUpdateRegex().Replace(result, m => 
            m.Groups[1].Value + QuoteIfReserved(m.Groups[2].Value) + m.Groups[3].Value);
        
        // Quote table after DELETE FROM
        result = TableDeleteRegex().Replace(result, m => 
            m.Groups[1].Value + QuoteIfReserved(m.Groups[2].Value) + m.Groups[3].Value);
        
        return result;
    }

    private static string QuoteWhereAndSetColumns(string sql)
    {
        // Quote reserved words when used as column names in WHERE, SET, ORDER BY
        foreach (var reserved in ReservedWords)
        {
            // Match reserved word used as column (not already quoted, followed by operator)
            var pattern = $@"(\s)({Regex.Escape(reserved)})(\s*[=<>!]|\s+(?:ASC|DESC|NULLS|IS\s|IN\s|LIKE\s|BETWEEN\s))";
            sql = Regex.Replace(sql, pattern, 
                m => m.Groups[1].Value + $"\"{m.Groups[2].Value}\"" + m.Groups[3].Value,
                RegexOptions.IgnoreCase);
        }
        
        return sql;
    }
}
