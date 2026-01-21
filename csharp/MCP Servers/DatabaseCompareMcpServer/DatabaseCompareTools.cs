using FirebirdSql.Data.FirebirdClient;
using System.Data;
using System.Text;
using System.Text.Json;

namespace DatabaseCompareMcpServer;

public static class DatabaseCompareTools
{
    private static readonly ILogger _logger = LoggerFactory.Create(builder =>
    {
        builder.AddConsole();
    }).CreateLogger("DatabaseCompareTools");

    public static async Task<object> CompareSchemas(JsonElement args)
    {
        try
        {
            var sourceConnString = args.GetProperty("sourceConnectionString").GetString()!;
            var targetConnString = args.GetProperty("targetConnectionString").GetString()!;

            _logger.LogInformation("Opening source database connection...");
            var sourceTables = await GetTableList(sourceConnString, "source");

            _logger.LogInformation("Opening target database connection...");
            var targetTables = await GetTableList(targetConnString, "target");

            var onlyInSource = sourceTables.Except(targetTables).ToList();
            var onlyInTarget = targetTables.Except(sourceTables).ToList();
            var inBoth = sourceTables.Intersect(targetTables).ToList();

            var differences = new List<object>();

            foreach (var table in inBoth)
            {
                var sourceColumns = await GetTableColumns(sourceConnString, table, "source");
                var targetColumns = await GetTableColumns(targetConnString, table, "target");

                if (!sourceColumns.SequenceEqual(targetColumns))
                {
                    differences.Add(new { table, type = "ColumnMismatch", sourceColumns, targetColumns });
                }
            }

            return new
            {
                success = true,
                tablesOnlyInSource = onlyInSource,
                tablesOnlyInTarget = onlyInTarget,
                tablesInBoth = inBoth.Count,
                differences
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CompareSchemas failed");
            return new { success = false, error = ex.Message };
        }
    }

    public static async Task<object> GenerateMigrationScript(JsonElement args)
    {
        try
        {
            var sourceConnString = args.GetProperty("sourceConnectionString").GetString()!;
            var targetConnString = args.GetProperty("targetConnectionString").GetString()!;

            var sourceTables = await GetTableList(sourceConnString, "source");
            var targetTables = await GetTableList(targetConnString, "target");

            var script = new StringBuilder();
            script.AppendLine("-- Migration Script Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            script.AppendLine();

            var missingTables = sourceTables.Except(targetTables).ToList();
            foreach (var table in missingTables)
            {
                _ = await GetTableColumns(sourceConnString, table, "source");
                script.AppendLine($"-- Create table: {table}");
                script.AppendLine($"CREATE TABLE {table} (");
                script.AppendLine("    -- Add column definitions here");
                script.AppendLine(");");
                script.AppendLine();
            }

            var extraTables = targetTables.Except(sourceTables).ToList();
            foreach (var table in extraTables)
            {
                script.AppendLine($"-- DROP TABLE {table};");
                script.AppendLine();
            }

            return new
            {
                success = true,
                script = script.ToString(),
                tablesAdded = missingTables.Count,
                tablesRemoved = extraTables.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GenerateMigrationScript failed");
            return new { success = false, error = ex.Message };
        }
    }

    public static async Task<object> ValidateForeignKeys(JsonElement args)
    {
        try
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;
            using var conn = new FbConnection(connectionString);

            _logger.LogInformation("Opening database connection for foreign key validation...");
            await conn.OpenAsync();

            var sql = @"
                SELECT 
                    rc.RDB$CONSTRAINT_NAME as CONSTRAINT_NAME,
                    rc.RDB$RELATION_NAME as TABLE_NAME,
                    d1.RDB$FIELD_NAME as COLUMN_NAME,
                    d2.RDB$DEPENDED_ON_NAME as REFERENCED_TABLE,
                    d3.RDB$FIELD_NAME as REFERENCED_COLUMN
                FROM RDB$RELATION_CONSTRAINTS rc
                LEFT JOIN RDB$REF_CONSTRAINTS refc ON rc.RDB$CONSTRAINT_NAME = refc.RDB$CONSTRAINT_NAME
                LEFT JOIN RDB$RELATION_CONSTRAINTS rc2 ON refc.RDB$CONST_NAME_UQ = rc2.RDB$CONSTRAINT_NAME
                LEFT JOIN RDB$DEPENDENCIES d1 ON d1.RDB$DEPENDED_ON_NAME = rc.RDB$RELATION_NAME
                LEFT JOIN RDB$DEPENDENCIES d2 ON d2.RDB$DEPENDENT_NAME = rc.RDB$RELATION_NAME
                LEFT JOIN RDB$DEPENDENCIES d3 ON d3.RDB$DEPENDED_ON_NAME = d2.RDB$DEPENDED_ON_NAME
                WHERE rc.RDB$CONSTRAINT_TYPE = 'FOREIGN KEY'";

            using var cmd = new FbCommand(sql, conn);
            var foreignKeys = new List<object>();

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                foreignKeys.Add(new
                {
                    constraintName = reader["CONSTRAINT_NAME"].ToString()?.Trim(),
                    tableName = reader["TABLE_NAME"].ToString()?.Trim(),
                    columnName = reader["COLUMN_NAME"].ToString()?.Trim(),
                    referencedTable = reader["REFERENCED_TABLE"].ToString()?.Trim(),
                    referencedColumn = reader["REFERENCED_COLUMN"].ToString()?.Trim()
                });
            }

            return new
            {
                success = true,
                foreignKeyCount = foreignKeys.Count,
                foreignKeys
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ValidateForeignKeys failed");
            return new { success = false, error = ex.Message };
        }
    }

    public static async Task<object> CompareTableData(JsonElement args)
    {
        try
        {
            var sourceConnString = args.GetProperty("sourceConnectionString").GetString()!;
            var targetConnString = args.GetProperty("targetConnectionString").GetString()!;
            var tableName = args.GetProperty("tableName").GetString()!;

            using var sourceConn = new FbConnection(sourceConnString);
            using var targetConn = new FbConnection(targetConnString);

            _logger.LogInformation("Opening source and target DB for CompareTableData...");
            await sourceConn.OpenAsync();
            await targetConn.OpenAsync();

            var sourceCount = await GetRowCount(sourceConn, tableName);
            var targetCount = await GetRowCount(targetConn, tableName);

            return new
            {
                success = true,
                tableName,
                sourceRowCount = sourceCount,
                targetRowCount = targetCount,
                difference = sourceCount - targetCount,
                match = sourceCount == targetCount
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CompareTableData failed");
            return new { success = false, error = ex.Message };
        }
    }

    public static async Task<object> FindOrphanedRecords(JsonElement args)
    {
        try
        {
            var connectionString = args.GetProperty("connectionString").GetString()!;
            var tableName = args.GetProperty("tableName").GetString()!;
            var foreignKeyColumn = args.GetProperty("foreignKeyColumn").GetString()!;
            var referencedTable = args.GetProperty("referencedTable").GetString()!;
            var referencedColumn = args.GetProperty("referencedColumn").GetString()!;

            using var conn = new FbConnection(connectionString);
            _logger.LogInformation("Opening DB for FindOrphanedRecords...");
            await conn.OpenAsync();

            var sql = $@"
                SELECT COUNT(*) 
                FROM {tableName} t
                WHERE NOT EXISTS (
                    SELECT 1 FROM {referencedTable} r 
                    WHERE r.{referencedColumn} = t.{foreignKeyColumn}
                )";

            using var cmd = new FbCommand(sql, conn);
            var orphanedCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());

            return new
            {
                success = true,
                tableName,
                foreignKeyColumn,
                referencedTable,
                orphanedRecords = orphanedCount,
                hasOrphans = orphanedCount > 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FindOrphanedRecords failed");
            return new { success = false, error = ex.Message };
        }
    }

    // ──────────────────────────────────────────────
    // Helper methods
    // ──────────────────────────────────────────────
    private static async Task<List<string>> GetTableList(string connectionString, string sourceOrTarget)
    {
        try
        {
            using var conn = new FbConnection(connectionString);
            _logger.LogInformation("Opening {DB} connection...", sourceOrTarget);
            await conn.OpenAsync();
            _logger.LogInformation("{DB} connection opened.", sourceOrTarget);

            var tables = new List<string>();
            var schema = await conn.GetSchemaAsync("Tables");

            foreach (DataRow row in schema.Rows)
            {
                var tableName = row["TABLE_NAME"].ToString()!;
                if (!tableName.StartsWith("RDB$") && !tableName.StartsWith("MON$"))
                    tables.Add(tableName);
            }

            _logger.LogInformation("{DB} tables found: {Count}", sourceOrTarget, tables.Count);
            return tables;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTableList failed for {DB}", sourceOrTarget);
            return new List<string>();
        }
    }

    private static async Task<List<string>> GetTableColumns(string connectionString, string tableName, string sourceOrTarget)
    {
        try
        {
            using var conn = new FbConnection(connectionString);
            await conn.OpenAsync();

            var columns = new List<string>();
            var sql = @"
                SELECT RDB$FIELD_NAME 
                FROM RDB$RELATION_FIELDS 
                WHERE RDB$RELATION_NAME = @TableName 
                ORDER BY RDB$FIELD_POSITION";

            using var cmd = new FbCommand(sql, conn);
            cmd.Parameters.AddWithValue("@TableName", tableName);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader["RDB$FIELD_NAME"].ToString()!.Trim());
            }

            _logger.LogInformation("{DB} table {Table} columns found: {Count}", sourceOrTarget, tableName, columns.Count);
            return columns;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTableColumns failed for {DB} table {Table}", sourceOrTarget, tableName);
            return new List<string>();
        }
    }

    private static async Task<int> GetRowCount(FbConnection conn, string tableName)
    {
        try
        {
            var sql = $"SELECT COUNT(*) FROM {tableName}";
            using var cmd = new FbCommand(sql, conn);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetRowCount failed for table {Table}", tableName);
            return -1;
        }
    }
}
