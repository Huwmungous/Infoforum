using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using DelphiAnalysisMcpServer.Models;
using SfD.Mcp.Protocol.Models;

namespace DelphiAnalysisMcpServer.Services;

/// <summary>
/// Resolves SELECT * queries by fetching actual column information from Firebird database schema.
/// This is used for validation when field accesses are detected from code.
/// </summary>
public partial class SchemaBasedSelectStarResolver
{
    private readonly ILogger<SchemaBasedSelectStarResolver> _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _firebirdMcpUrl;

    [GeneratedRegex(@"SELECT\s+\*\s+FROM\s+(?:""?(\w+)""?\.)?""?(\w+)""?",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SelectStarTableRegex();

    public SchemaBasedSelectStarResolver(
        ILogger<SchemaBasedSelectStarResolver> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _firebirdMcpUrl = configuration["FirebirdMcpServerUrl"];
    }

    /// <summary>
    /// Attempts to resolve a SELECT * query by fetching column information from the database schema.
    /// Falls back to code-based field access if schema lookup fails.
    /// </summary>
    /// <param name="sql">The SQL query containing SELECT *</param>
    /// <param name="fieldAccesses">Field accesses extracted from code (fallback)</param>
    /// <param name="databasePath">Path to the Firebird database file (optional)</param>
    /// <returns>Rewritten SQL with specific columns, or original if resolution fails</returns>
    public async Task<string> ResolveSelectStarAsync(
        string sql,
        List<FieldAccess> fieldAccesses,
        string? databasePath = null)
    {
        if (string.IsNullOrWhiteSpace(sql) || string.IsNullOrWhiteSpace(_firebirdMcpUrl))
        {
            return FieldAccessAnalyser.RewriteSelectStar(sql, fieldAccesses);
        }

        // Extract table name from SELECT * query
        var match = SelectStarTableRegex().Match(sql);
        if (!match.Success)
        {
            return sql; // Not a SELECT * query
        }

        var tableName = match.Groups[2].Value.ToUpperInvariant();
        _logger.LogInformation("Attempting to resolve SELECT * from table {TableName} using schema", tableName);

        try
        {
            // Fetch column information from FirebirdMcpServer
            var columns = await GetTableColumnsAsync(tableName, databasePath);

            if (columns == null || columns.Count == 0)
            {
                _logger.LogWarning("No columns found for table {TableName}, falling back to field accesses", tableName);
                return FieldAccessAnalyser.RewriteSelectStar(sql, fieldAccesses);
            }

            // Only rewrite if we have field accesses detected from code
            if (fieldAccesses.Count == 0)
            {
                _logger.LogWarning("No field accesses detected in code for SELECT * from {TableName}. " +
                    "Query will remain as SELECT * - manual review recommended.", tableName);
                return sql; // Leave as SELECT * for manual review
            }

            // Intersect field accesses with actual schema columns (for validation)
            var accessedColumnNames = new HashSet<string>(
                fieldAccesses.Select(f => f.FieldName.ToUpperInvariant()));

            var columnsToSelect = columns
                .Where(c => accessedColumnNames.Contains(c.ColumnName.ToUpperInvariant()))
                .Select(c => FormatColumnName(c.ColumnName))
                .ToList();

            if (columnsToSelect.Count == 0)
            {
                _logger.LogWarning("Field accesses detected for {TableName} but columns don't match schema. " +
                    "Possible column name mismatch. Query will remain as SELECT *", tableName);
                return sql; // Leave as SELECT * if validation fails
            }

            // Warn if some accessed fields aren't in schema (possible typos or renamed columns)
            var missingFields = accessedColumnNames.Except(
                columns.Select(c => c.ColumnName.ToUpperInvariant())).ToList();
            if (missingFields.Count > 0)
            {
                _logger.LogWarning("Field accesses for {TableName} reference non-existent columns: {MissingFields}",
                    tableName, string.Join(", ", missingFields));
            }

            // Rewrite the SQL
            var columnList = string.Join(", ", columnsToSelect);
            var rewritten = SelectStarTableRegex().Replace(sql,
                $"SELECT {columnList} FROM {FormatTableName(tableName)}");

            _logger.LogInformation("Rewrote SELECT * for {TableName}: {Count} columns",
                tableName, columnsToSelect.Count);

            return rewritten;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve SELECT * from schema for table {TableName}, falling back to field accesses", tableName);
            return FieldAccessAnalyser.RewriteSelectStar(sql, fieldAccesses);
        }
    }

    /// <summary>
    /// Fetches column information for a table from the FirebirdMcpServer.
    /// Uses the shared SfD.Mcp.Protocol library for MCP communication.
    /// </summary>
    private async Task<List<TableColumnInfo>?> GetTableColumnsAsync(string tableName, string? databasePath)
    {
        if (string.IsNullOrWhiteSpace(_firebirdMcpUrl))
            return null;

        try
        {
            // Create MCP request using shared protocol library
            var request = new McpRequest
            {
                Jsonrpc = "2.0",
                Id = 1,
                Method = "tools/call",
                Params = new McpParams
                {
                    Name = "get_table_columns",
                    Arguments = JsonSerializer.SerializeToElement(new
                    {
                        table_name = tableName,
                        database_path = databasePath
                    })
                }
            };

            var response = await _httpClient.PostAsJsonAsync(_firebirdMcpUrl, request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("FirebirdMcpServer returned {StatusCode} for table {TableName}",
                    response.StatusCode, tableName);
                return null;
            }

            // Deserialize using shared protocol library
            var mcpResponse = await response.Content.ReadFromJsonAsync<McpResponse>();

            if (mcpResponse?.Error != null)
            {
                _logger.LogWarning("FirebirdMcpServer returned error for table {TableName}: {Error}",
                    tableName, mcpResponse.Error.Message);
                return null;
            }

            // Parse the result - it should contain content array with columns
            if (mcpResponse?.Result != null)
            {
                var resultJson = JsonSerializer.Serialize(mcpResponse.Result);
                var resultData = JsonSerializer.Deserialize<McpToolResult>(resultJson);

                if (resultData?.Content != null && resultData.Content.Count > 0)
                {
                    var firstContent = resultData.Content[0];
                    if (firstContent.Type == "text" && !string.IsNullOrEmpty(firstContent.Text))
                    {
                        // Parse the text content as JSON containing columns
                        var columnResponse = JsonSerializer.Deserialize<TableColumnsResponse>(firstContent.Text);
                        return columnResponse?.Columns;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call FirebirdMcpServer for table {TableName}", tableName);
            return null;
        }
    }

    /// <summary>
    /// Formats a column name with appropriate quoting.
    /// Quotes reserved words, leaves others unquoted.
    /// </summary>
    private static string FormatColumnName(string columnName)
    {
        // Check if it's a reserved word
        var reservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "DATE", "ORDER", "TEXT", "ACTION", "ADMIN", "TIMESTAMP", "USER", "VALUE",
            "ROLE", "TYPE", "POSITION", "SIZE", "COUNT", "YEAR", "MONTH", "DAY",
            "HOUR", "MINUTE", "SECOND", "LEVEL", "KEY", "INDEX", "SCHEMA", "TABLE",
            "VIEW", "TRIGGER", "PROCEDURE", "FUNCTION", "GRANT", "REVOKE", "CREATE",
            "ALTER", "DROP", "SELECT", "INSERT", "UPDATE", "DELETE", "FROM", "WHERE",
            "GROUP", "HAVING", "ORDER", "BY", "AS", "JOIN", "INNER", "LEFT", "RIGHT",
            "FULL", "CROSS", "ON", "UNION", "INTERSECT", "EXCEPT", "DISTINCT", "ALL",
            "NULL", "TRUE", "FALSE", "NOT", "AND", "OR", "IN", "BETWEEN", "LIKE",
            "IS", "EXISTS", "CASE", "WHEN", "THEN", "ELSE", "END", "CAST", "EXTRACT"
        };

        if (reservedWords.Contains(columnName))
        {
            return $"\"{columnName}\"";
        }

        return columnName;
    }

    /// <summary>
    /// Formats a table name without quotes (unless it's a reserved word).
    /// </summary>
    private static string FormatTableName(string tableName)
    {
        return tableName; // Tables are typically not quoted unless necessary
    }
}

/// <summary>
/// Represents a column in a database table.
/// </summary>
public class TableColumnInfo
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public int? MaxLength { get; set; }
}

/// <summary>
/// Response structure for table columns from FirebirdMcpServer.
/// </summary>
public class TableColumnsResponse
{
    public List<TableColumnInfo> Columns { get; set; } = [];
}

/// <summary>
/// MCP tool result structure (parses the Result property from McpResponse).
/// </summary>
public class McpToolResult
{
    [JsonPropertyName("content")]
    public List<McpToolContent>? Content { get; set; }
}

/// <summary>
/// MCP tool content item.
/// </summary>
public class McpToolContent
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}