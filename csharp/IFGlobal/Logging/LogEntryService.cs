using Microsoft.Extensions.Logging;
using Npgsql;
using IFGlobal.Models;
using System.Text.Json;

namespace IFGlobal.Logging;

public partial class LogEntryService(PGConnectionConfig dbConfig, ILogger<LogEntryService> logger)
{
    private readonly string _connectionString = dbConfig.ToString();
    private readonly ILogger<LogEntryService> _logger = logger;

    public async Task<int> AddLogEntryAsync(LogEntryRequest logEntry)
    {

        if (logEntry.Realm is null || logEntry.Client is null || logEntry.LogData is null)
        {
            throw new ArgumentNullException(nameof(logEntry), "Realm, Client and LogData cannot be null");
        }

        if (string.IsNullOrEmpty(logEntry.Environment))
            logEntry.Environment ??= TryGetString(logEntry.LogData, "environment");

        if (string.IsNullOrEmpty(logEntry.Application))
            logEntry.Application ??= TryGetString(logEntry.LogData, "application");

        if (string.IsNullOrEmpty(logEntry.LogLevel))
            logEntry.LogLevel ??= TryGetString(logEntry.LogData, "level");

        try
        {
            LogInsertingEntry(_logger);
#pragma warning disable CA1873 // Unnecessary evaluation is guarded by IsEnabled check
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogJsonData(_logger, logEntry.LogData.RootElement.GetRawText());
            }
#pragma warning restore CA1873

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            LogConnectionOpened(_logger);

            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO log_entries (realm, client, log_data, environment, application, log_level) 
              VALUES (@realm, @client, @log_data::jsonb, @environment, @application, @log_level) 
              RETURNING idx",
                connection);

            cmd.Parameters.AddWithValue("realm", logEntry.Realm);
            cmd.Parameters.AddWithValue("client", logEntry.Client);
            cmd.Parameters.AddWithValue("log_data", logEntry.LogData.RootElement.GetRawText());
            cmd.Parameters.AddWithValue("environment", (object?)logEntry.Environment ?? DBNull.Value);
            cmd.Parameters.AddWithValue("application", (object?)logEntry.Application ?? DBNull.Value);
            cmd.Parameters.AddWithValue("log_level", (object?)logEntry.LogLevel ?? DBNull.Value);

            LogExecutingQuery(_logger);

            var result = await cmd.ExecuteScalarAsync();

#pragma warning disable CA1873 // Unnecessary evaluation is guarded by IsEnabled check
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                LogQueryResult(_logger, result?.ToString() ?? "null");
            }
#pragma warning restore CA1873

            var returnValue = result is not null ? Convert.ToInt32(result) : 0;

            LogReturningValue(_logger, returnValue);

            return returnValue;
        }
        catch (PostgresException pgEx)
        {
            LogPostgresError(_logger, pgEx, pgEx.SqlState ?? "", pgEx.Detail ?? "", pgEx.Hint ?? "");
            throw;
        }
        catch (NpgsqlException npgEx)
        {
            LogNpgsqlError(_logger, npgEx, npgEx.InnerException?.Message ?? "");
            throw;
        }
        catch (Exception ex)
        {
            LogGeneralError(_logger, ex);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Attempting to insert log entry")]
    private static partial void LogInsertingEntry(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "JSON data: {JsonData}")]
    private static partial void LogJsonData(ILogger logger, string jsonData);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Database connection opened")]
    private static partial void LogConnectionOpened(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Executing query")]
    private static partial void LogExecutingQuery(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Query executed, result: {Result}")]
    private static partial void LogQueryResult(ILogger logger, string result);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Returning: {Value}")]
    private static partial void LogReturningValue(ILogger logger, int value);

    [LoggerMessage(Level = LogLevel.Error, Message = "PostgreSQL error. SqlState: {SqlState}, Detail: {Detail}, Hint: {Hint}")]
    private static partial void LogPostgresError(ILogger logger, Exception ex, string sqlState, string detail, string hint);

    [LoggerMessage(Level = LogLevel.Error, Message = "Npgsql error. Inner: {InnerMessage}")]
    private static partial void LogNpgsqlError(ILogger logger, Exception ex, string innerMessage);

    [LoggerMessage(Level = LogLevel.Error, Message = "General error inserting log entry")]
    private static partial void LogGeneralError(ILogger logger, Exception ex);

    public async Task<LogEntryResponse?> GetLogEntryAsync(int idx)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            @"SELECT idx, realm, client, log_data, created_at, environment, application, log_level
              FROM log_entries 
              WHERE idx = @idx",
            connection);

        cmd.Parameters.AddWithValue("idx", idx);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var jsonString = reader.GetString(3);
            var jsonDoc = JsonDocument.Parse(jsonString);
            return new LogEntryResponse(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                jsonDoc,
                reader.GetDateTime(4)
            );
        }

        return null;
    }

    public async Task<List<LogEntryResponse>> GetLogEntriesAsync(int limit, int offset)
    {
        var results = new List<LogEntryResponse>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var cmd = new NpgsqlCommand(
            @"SELECT *
                FROM (
                  SELECT idx, realm, client, log_data, created_at, environment, application, log_level
                  FROM log_entries
                  ORDER BY created_at DESC
                  LIMIT @limit
                ) t
                ORDER BY created_at ASC;",
            connection);

        cmd.Parameters.AddWithValue("limit", limit);
        cmd.Parameters.AddWithValue("offset", offset);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var jsonString = reader.GetString(3);
            var jsonDoc = JsonDocument.Parse(jsonString);
            results.Add(new LogEntryResponse(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                jsonDoc,
                reader.GetDateTime(4)
            ));
        }

        return results;
    }

    // ============ SEARCH FILTERS ============

    public async Task<List<string>> GetEnvironmentFiltersAsync(CancellationToken ct = default)
    {
        var results = new List<string>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = @"
            SELECT DISTINCT environment
            FROM public.log_entries
            WHERE environment IS NOT NULL
              AND environment <> ''
            ORDER BY environment;";

        await using var cmd = new NpgsqlCommand(sql, connection);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (!reader.IsDBNull(0))
                results.Add(reader.GetString(0));
        }

        return results;
    }

    public async Task<List<string>> GetRealmFiltersAsync(string? environment = null, CancellationToken ct = default)
    {
        var results = new List<string>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = @"
            SELECT DISTINCT realm
            FROM public.log_entries
            WHERE realm IS NOT NULL
              AND realm <> ''";

        if (!string.IsNullOrEmpty(environment))
        {
            sql += " AND environment = @environment";
        }

        sql += " ORDER BY realm;";

        await using var cmd = new NpgsqlCommand(sql, connection);

        if (!string.IsNullOrEmpty(environment))
        {
            cmd.Parameters.AddWithValue("environment", environment);
        }

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (!reader.IsDBNull(0))
                results.Add(reader.GetString(0));
        }

        return results;
    }

    public async Task<List<string>> GetClientFiltersAsync(string? environment = null, string? realm = null, CancellationToken ct = default)
    {
        var results = new List<string>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = @"
            SELECT DISTINCT client
            FROM public.log_entries
            WHERE client IS NOT NULL
              AND client <> ''";

        if (!string.IsNullOrEmpty(environment))
        {
            sql += " AND environment = @environment";
        }

        if (!string.IsNullOrEmpty(realm))
        {
            sql += " AND realm = @realm";
        }

        sql += " ORDER BY client;";

        await using var cmd = new NpgsqlCommand(sql, connection);

        if (!string.IsNullOrEmpty(environment))
        {
            cmd.Parameters.AddWithValue("environment", environment);
        }

        if (!string.IsNullOrEmpty(realm))
        {
            cmd.Parameters.AddWithValue("realm", realm);
        }

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (!reader.IsDBNull(0))
                results.Add(reader.GetString(0));
        }

        return results;
    }

    public async Task<List<string>> GetApplicationFiltersAsync(
        string? environment = null,
        string? realm = null,
        string? client = null,
        CancellationToken ct = default)
    {
        var results = new List<string>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = @"
            SELECT DISTINCT application
            FROM public.log_entries
            WHERE application IS NOT NULL
              AND application <> ''";

        if (!string.IsNullOrEmpty(environment))
        {
            sql += " AND environment = @environment";
        }

        if (!string.IsNullOrEmpty(realm))
        {
            sql += " AND realm = @realm";
        }

        if (!string.IsNullOrEmpty(client))
        {
            sql += " AND client = @client";
        }

        sql += " ORDER BY application;";

        await using var cmd = new NpgsqlCommand(sql, connection);

        if (!string.IsNullOrEmpty(environment))
        {
            cmd.Parameters.AddWithValue("environment", environment);
        }

        if (!string.IsNullOrEmpty(realm))
        {
            cmd.Parameters.AddWithValue("realm", realm);
        }

        if (!string.IsNullOrEmpty(client))
        {
            cmd.Parameters.AddWithValue("client", client);
        }

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (!reader.IsDBNull(0))
                results.Add(reader.GetString(0));
        }

        return results;
    }

    public async Task<List<string>> GetLogLevelFiltersAsync(CancellationToken ct = default)
    {
        var results = new List<string>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        var sql = @"
            SELECT DISTINCT log_level
            FROM public.log_entries
            WHERE log_level IS NOT NULL
              AND log_level <> ''
            ORDER BY log_level;";

        await using var cmd = new NpgsqlCommand(sql, connection);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (!reader.IsDBNull(0))
                results.Add(reader.GetString(0));
        }

        return results;
    }

    // ============ SEARCH OPERATIONS ============

    public async Task<List<LogEntryResponse>> SearchLogEntriesAsync(LogSearchRequest request) 
    {
        var results = new List<LogEntryResponse>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Build dynamic query
        var whereClauses = new List<string>();
        var parameters = new List<NpgsqlParameter>();
        int paramIndex = 0;

        // Add JSON field filters
        if (request.Filters is { Count: > 0 })
        {
            foreach (var filter in request.Filters)
            {
                var clause = BuildFilterClause(filter, ref paramIndex, parameters);
                if (!string.IsNullOrEmpty(clause))
                {
                    whereClauses.Add(clause);
                }
            }
        }

        // Add date range filters
        if (request.CreatedAfter.HasValue)
        {
            var paramName = $"@p{paramIndex++}";
            whereClauses.Add($"created_at >= {paramName}");
            parameters.Add(new NpgsqlParameter(paramName, request.CreatedAfter.Value));
        }

        if (request.CreatedBefore.HasValue)
        {
            var paramName = $"@p{paramIndex++}";
            whereClauses.Add($"created_at <= {paramName}");
            parameters.Add(new NpgsqlParameter(paramName, request.CreatedBefore.Value));
        }

        // Build WHERE clause with AND/OR logic
        var logicalOperatorStr = request.FilterLogic == LogicalOperator.Or ? " OR " : " AND ";
        var whereClause = whereClauses.Count > 0
            ? "WHERE " + string.Join(logicalOperatorStr, whereClauses)
            : "";

        // Validate and sanitize ORDER BY to prevent SQL injection
        var validOrderByColumns = new[] { "created_at", "idx" };
        var orderBy = validOrderByColumns.Contains(request.OrderBy, StringComparer.OrdinalIgnoreCase)
            ? request.OrderBy
            : "created_at";

        var validOrderDirection = new[] { "ASC", "DESC" };
        var orderDirection = validOrderDirection.Contains(request.OrderDirection, StringComparer.OrdinalIgnoreCase)
            ? request.OrderDirection
            : "ASC";

        // Build complete query
        var sql = $@"SELECT *
            FROM (
                SELECT idx, realm, client, log_data, created_at, environment, application, log_level
                FROM log_entries 
                {whereClause}
                ORDER BY created_at DESC
                LIMIT @limit OFFSET @offset
            ) t
            ORDER BY {orderBy} {orderDirection}";

        await using var cmd = new NpgsqlCommand(sql, connection);

        // Add all parameters
        foreach (var param in parameters)
        {
            cmd.Parameters.Add(param);
        }

        cmd.Parameters.AddWithValue("limit", request.Limit);
        cmd.Parameters.AddWithValue("offset", request.Offset);

        // Execute query
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var jsonString = reader.GetString(3);
            var jsonDoc = JsonDocument.Parse(jsonString);
            results.Add(new LogEntryResponse(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                jsonDoc,
                reader.GetDateTime(4)
            ));
        }

        return results;
    }

    public async Task<List<LogEntryResponse>> AdvancedSearchLogEntriesAsync(AdvancedLogSearchRequest request)
    {
        var results = new List<LogEntryResponse>();

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        var groupClauses = new List<string>();
        var parameters = new List<NpgsqlParameter>();
        int paramIndex = 0;

        // Process filter groups
        if (request.FilterGroups is { Count: > 0 })
        {
            foreach (var group in request.FilterGroups)
            {
                if (group.Conditions.Count > 0)
                {
                    var conditionClauses = new List<string>();

                    foreach (var condition in group.Conditions)
                    {
                        var clause = BuildFilterClause(condition, ref paramIndex, parameters);
                        if (!string.IsNullOrEmpty(clause))
                        {
                            conditionClauses.Add(clause);
                        }
                    }

                    if (conditionClauses.Count > 0)
                    {
                        var logicalOp = group.Logic == LogicalOperator.Or ? " OR " : " AND ";
                        groupClauses.Add($"({string.Join(logicalOp, conditionClauses)})");
                    }
                }
            }
        }

        // Add date range filters
        if (request.CreatedAfter.HasValue)
        {
            var paramName = $"@p{paramIndex++}";
            groupClauses.Add($"created_at >= {paramName}");
            parameters.Add(new NpgsqlParameter(paramName, request.CreatedAfter.Value));
        }

        if (request.CreatedBefore.HasValue)
        {
            var paramName = $"@p{paramIndex++}";
            groupClauses.Add($"created_at <= {paramName}");
            parameters.Add(new NpgsqlParameter(paramName, request.CreatedBefore.Value));
        }

        // Build WHERE clause
        var groupLogicalOp = request.GroupLogic == LogicalOperator.Or ? " OR " : " AND ";
        var whereClause = groupClauses.Count > 0
            ? "WHERE " + string.Join(groupLogicalOp, groupClauses)
            : "";

        // Validate ORDER BY
        var validOrderByColumns = new[] { "created_at", "idx" };
        var orderBy = validOrderByColumns.Contains(request.OrderBy, StringComparer.OrdinalIgnoreCase)
            ? request.OrderBy
            : "created_at";

        var orderDirection = string.Equals(request.OrderDirection, "ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

        // Build query
        var sql = $@"
            SELECT idx, realm, client, log_data, created_at, environment, application, log_level 
            FROM log_entries 
            {whereClause}
            ORDER BY {orderBy} {orderDirection}
            LIMIT @limit OFFSET @offset";

        await using var cmd = new NpgsqlCommand(sql, connection);

        foreach (var param in parameters)
        {
            cmd.Parameters.Add(param);
        }

        cmd.Parameters.AddWithValue("limit", request.Limit);
        cmd.Parameters.AddWithValue("offset", request.Offset);

        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var jsonString = reader.GetString(3);
            var jsonDoc = JsonDocument.Parse(jsonString);
            results.Add(new LogEntryResponse(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                jsonDoc,
                reader.GetDateTime(4)
            ));
        }

        return results;
    }

    // ============ HELPER METHODS ============

    private static string BuildFilterClause(
        FilterCondition filter,
        ref int paramIndex,
        List<NpgsqlParameter> parameters)
    {
        var fieldAccessor = $"log_data->>'{filter.Field}'";

        switch (filter.Operator)
        {
            case FilterOperator.Equals:
                {
                    var paramName = $"@p{paramIndex++}";
                    parameters.Add(new NpgsqlParameter(paramName, filter.Value ?? string.Empty));
                    return $"{fieldAccessor} = {paramName}";
                }

            case FilterOperator.NotEquals:
                {
                    var paramName = $"@p{paramIndex++}";
                    parameters.Add(new NpgsqlParameter(paramName, filter.Value ?? string.Empty));
                    return $"{fieldAccessor} != {paramName}";
                }

            case FilterOperator.Contains:
                {
                    var paramName = $"@p{paramIndex++}";
                    parameters.Add(new NpgsqlParameter(paramName, $"%{filter.Value}%"));
                    return $"{fieldAccessor} ILIKE {paramName}";
                }

            case FilterOperator.NotContains:
                {
                    var paramName = $"@p{paramIndex++}";
                    parameters.Add(new NpgsqlParameter(paramName, $"%{filter.Value}%"));
                    return $"{fieldAccessor} NOT ILIKE {paramName}";
                }

            case FilterOperator.StartsWith:
                {
                    var paramName = $"@p{paramIndex++}";
                    parameters.Add(new NpgsqlParameter(paramName, $"{filter.Value}%"));
                    return $"{fieldAccessor} ILIKE {paramName}";
                }

            case FilterOperator.EndsWith:
                {
                    var paramName = $"@p{paramIndex++}";
                    parameters.Add(new NpgsqlParameter(paramName, $"%{filter.Value}"));
                    return $"{fieldAccessor} ILIKE {paramName}";
                }

            case FilterOperator.GreaterThan:
                {
                    var paramName = $"@p{paramIndex++}";
                    parameters.Add(new NpgsqlParameter(paramName, filter.Value ?? string.Empty));
                    return $"({fieldAccessor})::numeric > ({paramName})::numeric";
                }

            case FilterOperator.GreaterThanOrEqual:
                {
                    var paramName = $"@p{paramIndex++}";
                    parameters.Add(new NpgsqlParameter(paramName, filter.Value ?? string.Empty));
                    return $"({fieldAccessor})::numeric >= ({paramName})::numeric";
                }

            case FilterOperator.LessThan:
                {
                    var paramName = $"@p{paramIndex++}";
                    parameters.Add(new NpgsqlParameter(paramName, filter.Value ?? string.Empty));
                    return $"({fieldAccessor})::numeric < ({paramName})::numeric";
                }

            case FilterOperator.LessThanOrEqual:
                {
                    var paramName = $"@p{paramIndex++}";
                    parameters.Add(new NpgsqlParameter(paramName, filter.Value ?? string.Empty));
                    return $"({fieldAccessor})::numeric <= ({paramName})::numeric";
                }

            case FilterOperator.In:
                {
                    if (filter.Values is not { Count: > 0 })
                        return string.Empty;

                    var inParams = new List<string>();
                    foreach (var value in filter.Values)
                    {
                        var paramName = $"@p{paramIndex++}";
                        parameters.Add(new NpgsqlParameter(paramName, value));
                        inParams.Add(paramName);
                    }
                    return $"{fieldAccessor} IN ({string.Join(", ", inParams)})";
                }

            case FilterOperator.NotIn:
                {
                    if (filter.Values is not { Count: > 0 })
                        return string.Empty;

                    var inParams = new List<string>();
                    foreach (var value in filter.Values)
                    {
                        var paramName = $"@p{paramIndex++}";
                        parameters.Add(new NpgsqlParameter(paramName, value));
                        inParams.Add(paramName);
                    }
                    return $"{fieldAccessor} NOT IN ({string.Join(", ", inParams)})";
                }

            case FilterOperator.Between:
                {
                    if (string.IsNullOrEmpty(filter.Value) || string.IsNullOrEmpty(filter.ValueTo))
                        return string.Empty;

                    var paramFrom = $"@p{paramIndex++}";
                    var paramTo = $"@p{paramIndex++}";
                    parameters.Add(new NpgsqlParameter(paramFrom, filter.Value));
                    parameters.Add(new NpgsqlParameter(paramTo, filter.ValueTo));
                    return $"({fieldAccessor})::numeric BETWEEN ({paramFrom})::numeric AND ({paramTo})::numeric";
                }

            case FilterOperator.IsNull:
                return $"{fieldAccessor} IS NULL";

            case FilterOperator.IsNotNull:
                return $"{fieldAccessor} IS NOT NULL";

            default:
                return string.Empty;
        }
    }

    private static string? TryGetString(JsonDocument? doc, string propertyName)
    {
        if (doc is null) return null;

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;

        if (!root.TryGetProperty(propertyName, out var prop)) return null;
        if (prop.ValueKind != JsonValueKind.String) return null;

        var value = prop.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}