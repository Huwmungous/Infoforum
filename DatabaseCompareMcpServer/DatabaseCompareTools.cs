using System.Data;
using System.Text;
using System.Text.Json;
using FirebirdSql.Data.FirebirdClient;

namespace DatabaseCompareMcpServer;

public static class DatabaseCompareTools
{
    public static async Task<object> CompareSchemas(JsonElement args)
    {
        var sourceConnString = args.GetProperty("sourceConnectionString").GetString()!;
        var targetConnString = args.GetProperty("targetConnectionString").GetString()!;
        
        var sourceTables = await GetTableList(sourceConnString);
        var targetTables = await GetTableList(targetConnString);
        
        var onlyInSource = sourceTables.Except(targetTables).ToList();
        var onlyInTarget = targetTables.Except(sourceTables).ToList();
        var inBoth = sourceTables.Intersect(targetTables).ToList();
        
        var differences = new List<object>();
        
        foreach (var table in inBoth)
        {
            var sourceColumns = await GetTableColumns(sourceConnString, table);
            var targetColumns = await GetTableColumns(targetConnString, table);
            
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

    public static async Task<object> GenerateMigrationScript(JsonElement args)
    {
        var sourceConnString = args.GetProperty("sourceConnectionString").GetString()!;
        var targetConnString = args.GetProperty("targetConnectionString").GetString()!;
        
        var sourceTables = await GetTableList(sourceConnString);
        var targetTables = await GetTableList(targetConnString);
        
        var script = new StringBuilder();
        script.AppendLine("-- Migration Script Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        script.AppendLine();
        
        var missingTables = sourceTables.Except(targetTables).ToList();
        foreach (var table in missingTables)
        {
            var columns = await GetTableColumns(sourceConnString, table);
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

    public static async Task<object> ValidateForeignKeys(JsonElement args)
    {
        var connectionString = args.GetProperty("connectionString").GetString()!;
        
        using var conn = new FbConnection(connectionString);
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

    public static async Task<object> CompareTableData(JsonElement args)
    {
        var sourceConnString = args.GetProperty("sourceConnectionString").GetString()!;
        var targetConnString = args.GetProperty("targetConnectionString").GetString()!;
        var tableName = args.GetProperty("tableName").GetString()!;
        
        using var sourceConn = new FbConnection(sourceConnString);
        using var targetConn = new FbConnection(targetConnString);
        
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

    public static async Task<object> FindOrphanedRecords(JsonElement args)
    {
        var connectionString = args.GetProperty("connectionString").GetString()!;
        var tableName = args.GetProperty("tableName").GetString()!;
        var foreignKeyColumn = args.GetProperty("foreignKeyColumn").GetString()!;
        var referencedTable = args.GetProperty("referencedTable").GetString()!;
        var referencedColumn = args.GetProperty("referencedColumn").GetString()!;
        
        using var conn = new FbConnection(connectionString);
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

    private static async Task<List<string>> GetTableList(string connectionString)
    {
        using var conn = new FbConnection(connectionString);
        await conn.OpenAsync();
        
        var tables = new List<string>();
        var schema = await conn.GetSchemaAsync("Tables");
        
        foreach (DataRow row in schema.Rows)
        {
            var tableName = row["TABLE_NAME"].ToString()!;
            if (!tableName.StartsWith("RDB$") && !tableName.StartsWith("MON$"))
                tables.Add(tableName);
        }
        
        return tables;
    }

    private static async Task<List<string>> GetTableColumns(string connectionString, string tableName)
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
        
        return columns;
    }

    private static async Task<int> GetRowCount(FbConnection conn, string tableName)
    {
        var sql = $"SELECT COUNT(*) FROM {tableName}";
        using var cmd = new FbCommand(sql, conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
}
