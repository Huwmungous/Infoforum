using Microsoft.Data.Sqlite;
using SqliteMcpServer.Models;
using Microsoft.Extensions.Logging;

namespace SqliteMcpServer.Services;

public class SqliteService
{
    private readonly ILogger<SqliteService> _logger;
    private readonly string _connectionString;

    public SqliteService(ILogger<SqliteService> logger, string dbPath)
    {
        _logger = logger;
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        
        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS migration_metadata (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                key TEXT NOT NULL UNIQUE,
                value TEXT,
                created_at TEXT DEFAULT CURRENT_TIMESTAMP
            );";
        
        using var command = new SqliteCommand(createTableSql, connection);
        command.ExecuteNonQuery();
    }

    public async Task<QueryResult> ReadQueryAsync(string sql, Dictionary<string, object>? parameters = null)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqliteCommand(sql, connection);
            
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue($"@{param.Key}", param.Value ?? DBNull.Value);
                }
            }

            var rows = new List<Dictionary<string, object?>>();
            await using var reader = await command.ExecuteReaderAsync();
            
            while (await reader.ReadAsync())
            {
                var row = new Dictionary<string, object?>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }

            return new QueryResult
            {
                Success = true,
                Rows = rows,
                RowCount = rows.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Query error");
            return new QueryResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<ExecuteResult> WriteQueryAsync(string sql, Dictionary<string, object>? parameters = null)
    {
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            await using var command = new SqliteCommand(sql, connection);
            
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue($"@{param.Key}", param.Value ?? DBNull.Value);
                }
            }

            var rowsAffected = await command.ExecuteNonQueryAsync();

            return new ExecuteResult
            {
                Success = true,
                RowsAffected = rowsAffected
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Execute error");
            return new ExecuteResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<ExecuteResult> CreateTableAsync(string tableName, Dictionary<string, string> columns)
    {
        var columnDefs = string.Join(", ", columns.Select(kvp => $"{kvp.Key} {kvp.Value}"));
        var sql = $"CREATE TABLE IF NOT EXISTS {tableName} ({columnDefs})";
        
        return await WriteQueryAsync(sql);
    }

    public async Task<QueryResult> ListTablesAsync()
    {
        var sql = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        return await ReadQueryAsync(sql);
    }

    public async Task<QueryResult> GetTableSchemaAsync(string tableName)
    {
        var sql = $"PRAGMA table_info({tableName})";
        return await ReadQueryAsync(sql);
    }
}