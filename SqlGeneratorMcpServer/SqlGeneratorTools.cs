using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SqlGeneratorMcpServer;

public static class SqlGeneratorTools
{
    public static Task<object> GenerateSelect(JsonElement args)
    {
        var tableName = args.GetProperty("tableName").GetString()!;
        var columns = args.TryGetProperty("columns", out var c) ? c.EnumerateArray().Select(x => x.GetString()!).ToArray() : new[] { "*" };
        var whereClause = args.TryGetProperty("whereClause", out var w) ? w.GetString() : null;
        
        var sql = new StringBuilder($"SELECT {string.Join(", ", columns)} FROM {tableName}");
        if (!string.IsNullOrEmpty(whereClause)) sql.Append($" WHERE {whereClause}");
        
        return Task.FromResult<object>(new { success = true, sql = sql.ToString(), type = "SELECT" });
    }

    public static Task<object> GenerateInsert(JsonElement args)
    {
        var tableName = args.GetProperty("tableName").GetString()!;
        var columns = args.GetProperty("columns").EnumerateArray().Select(x => x.GetString()!).ToArray();
        
        var sql = $"INSERT INTO {tableName} ({string.Join(", ", columns)}) VALUES ({string.Join(", ", columns.Select(c => $"@{c}"))})";
        
        return Task.FromResult<object>(new { success = true, sql, type = "INSERT", parameters = columns.Select(c => $"@{c}").ToArray() });
    }

    public static Task<object> GenerateUpdate(JsonElement args)
    {
        var tableName = args.GetProperty("tableName").GetString()!;
        var columns = args.GetProperty("columns").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var whereClause = args.GetProperty("whereClause").GetString()!;
        
        var setClauses = columns.Select(c => $"{c} = @{c}");
        var sql = $"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {whereClause}";
        
        return Task.FromResult<object>(new { success = true, sql, type = "UPDATE", parameters = columns.Select(c => $"@{c}").ToArray() });
    }

    public static Task<object> GenerateDelete(JsonElement args)
    {
        var tableName = args.GetProperty("tableName").GetString()!;
        var whereClause = args.GetProperty("whereClause").GetString()!;
        
        var sql = $"DELETE FROM {tableName} WHERE {whereClause}";
        
        return Task.FromResult<object>(new { success = true, sql, type = "DELETE" });
    }

    public static Task<object> TranslateSql(JsonElement args)
    {
        var sourceSql = args.GetProperty("sourceSql").GetString()!;
        var sourceDialect = args.GetProperty("sourceDialect").GetString()!;
        var targetDialect = args.GetProperty("targetDialect").GetString()!;
        
        var translated = sourceSql;
        
        // Basic translations (extend as needed)
        if (sourceDialect.ToLower() == "firebird" && targetDialect.ToLower() == "sqlserver")
        {
            translated = translated.Replace("||", "+");
            translated = Regex.Replace(translated, @"SUBSTRING\(([^,]+),([^,]+),([^)]+)\)", "SUBSTRING($1,$2,$3)");
        }
        
        return Task.FromResult<object>(new { success = true, originalSql = sourceSql, translatedSql = translated, sourceDialect, targetDialect });
    }

    public static Task<object> ParameterizeSql(JsonElement args)
    {
        var sql = args.GetProperty("sql").GetString()!;
        
        var parameters = new List<string>();
        var parameterized = Regex.Replace(sql, @"'([^']*)'", match =>
        {
            var paramName = $"@param{parameters.Count + 1}";
            parameters.Add(paramName);
            return paramName;
        });
        
        return Task.FromResult<object>(new { success = true, originalSql = sql, parameterizedSql = parameterized, parameters });
    }

    public static Task<object> GenerateStoredProcCall(JsonElement args)
    {
        var procedureName = args.GetProperty("procedureName").GetString()!;
        var parameters = args.TryGetProperty("parameters", out var p) ? p.EnumerateArray().Select(x => x.GetString()!).ToArray() : Array.Empty<string>();
        
        var sql = parameters.Length > 0 
            ? $"EXECUTE PROCEDURE {procedureName} ({string.Join(", ", parameters.Select(p => $"@{p}"))})"
            : $"EXECUTE PROCEDURE {procedureName}";
        
        return Task.FromResult<object>(new { success = true, sql, procedureName, parameters });
    }

    public static Task<object> GenerateCSharpEntity(JsonElement args)
    {
        var tableName = args.GetProperty("tableName").GetString()!;
        var columns = args.GetProperty("columns").EnumerateArray().Select(col => new
        {
            Name = col.GetProperty("name").GetString()!,
            Type = col.GetProperty("type").GetString()!,
            IsNullable = col.TryGetProperty("isNullable", out var n) && n.GetBoolean()
        }).ToArray();
        
        var className = ToPascalCase(tableName);
        var sb = new StringBuilder();
        sb.AppendLine($"public class {className}");
        sb.AppendLine("{");
        
        foreach (var col in columns)
        {
            var csharpType = MapSqlTypeToCSharp(col.Type);
            var nullable = col.IsNullable ? "?" : "";
            var propName = ToPascalCase(col.Name);
            sb.AppendLine($"    public {csharpType}{nullable} {propName} {{ get; set; }}");
        }
        
        sb.AppendLine("}");
        
        return Task.FromResult<object>(new { success = true, tableName, className, code = sb.ToString() });
    }

    public static Task<object> GenerateRepositoryInterface(JsonElement args)
    {
        var entityName = args.GetProperty("entityName").GetString()!;
        var operations = args.TryGetProperty("operations", out var ops) 
            ? ops.EnumerateArray().Select(x => x.GetString()!).ToArray() 
            : new[] { "GetAll", "GetById", "Add", "Update", "Delete" };
        
        var sb = new StringBuilder();
        sb.AppendLine($"public interface I{entityName}Repository");
        sb.AppendLine("{");
        
        foreach (var op in operations)
        {
            var method = op switch
            {
                "GetAll" => $"    Task<IEnumerable<{entityName}>> GetAllAsync();",
                "GetById" => $"    Task<{entityName}?> GetByIdAsync(int id);",
                "Add" => $"    Task<int> AddAsync({entityName} entity);",
                "Update" => $"    Task<bool> UpdateAsync({entityName} entity);",
                "Delete" => $"    Task<bool> DeleteAsync(int id);",
                _ => $"    Task {op}Async();"
            };
            sb.AppendLine(method);
        }
        
        sb.AppendLine("}");
        
        return Task.FromResult<object>(new { success = true, entityName, interfaceName = $"I{entityName}Repository", code = sb.ToString() });
    }

    private static string ToPascalCase(string input)
    {
        var words = input.Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join("", words.Select(w => char.ToUpper(w[0]) + w.Substring(1).ToLower()));
    }

    private static string MapSqlTypeToCSharp(string sqlType)
    {
        return sqlType.ToUpper() switch
        {
            "INTEGER" or "INT" or "SMALLINT" => "int",
            "BIGINT" => "long",
            "VARCHAR" or "CHAR" or "TEXT" or "BLOB SUB_TYPE TEXT" => "string",
            "DECIMAL" or "NUMERIC" => "decimal",
            "DOUBLE" or "FLOAT" => "double",
            "DATE" or "TIMESTAMP" => "DateTime",
            "BOOLEAN" => "bool",
            _ => "object"
        };
    }
}
