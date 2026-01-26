using System.Data.Common;
using System.Text.Json;
using ConfigWebService.Entities;
using IFGlobal.DataAccess;
using IFGlobal.Models;
using Microsoft.Extensions.Logging;
using NpgsqlTypes;

namespace ConfigWebService.Repositories;

/// <summary>
/// Repository for accessing configuration entries using simple SQL queries.
/// Extends BaseRepository from IFGlobal for consistent data access patterns.
/// </summary>
public class ConfigRepository : BaseRepository
{
    private const string TableName = "public.usr_svc_settings";

    public ConfigRepository(PGConnectionConfig pgConnection, ILogger<ConfigRepository> logger)
        : base(pgConnection, logger)
    {
    }

    /// <summary>
    /// Maps a database row to a ConfigEntry object.
    /// </summary>
    private static ConfigEntry? MapConfigEntry(DbDataReader reader)
    {
        return new ConfigEntry
        {
            Idx = reader.GetInt32(reader.GetOrdinal("idx")),
            AppDomain = reader.GetString(reader.GetOrdinal("app_domain")),
            UserConfig = reader.IsDBNull(reader.GetOrdinal("user_config"))
                ? null
                : JsonDocument.Parse(reader.GetString(reader.GetOrdinal("user_config"))),
            ServiceConfig = reader.IsDBNull(reader.GetOrdinal("service_config"))
                ? null
                : JsonDocument.Parse(reader.GetString(reader.GetOrdinal("service_config"))),
            BootstrapConfig = reader.IsDBNull(reader.GetOrdinal("bootstrap_config"))
                ? null
                : JsonDocument.Parse(reader.GetString(reader.GetOrdinal("bootstrap_config")))
        };
    }

    /// <summary>
    /// Get a batch of configuration entries with pagination (includes disabled for admin)
    /// </summary>
    public async Task<List<ConfigEntry>> GetBatchAsync(int offset, int limit, bool includeDisabled = true)
    {
        limit = Math.Min(limit, 100);

        // For includeDisabled filtering, we check the JSONB field in SQL
        // disabled entries have bootstrap_config->>'disabled' = 'true'
        string sql;
        if (includeDisabled)
        {
            sql = $@"
                SELECT idx, app_domain, user_config, service_config, bootstrap_config
                FROM {TableName}
                ORDER BY app_domain
                LIMIT @limit OFFSET @offset";
        }
        else
        {
            sql = $@"
                SELECT idx, app_domain, user_config, service_config, bootstrap_config
                FROM {TableName}
                WHERE COALESCE((bootstrap_config->>'disabled')::boolean, false) = false
                ORDER BY app_domain
                LIMIT @limit OFFSET @offset";
        }

        return await ExecuteQueryAsync(
            sql,
            p =>
            {
                p.AddWithValue("@limit", limit);
                p.AddWithValue("@offset", offset);
            },
            MapConfigEntry);
    }

    /// <summary>
    /// Get total count of entries
    /// </summary>
    public async Task<int> GetCountAsync(bool includeDisabled = true)
    {
        string sql;
        if (includeDisabled)
        {
            sql = $"SELECT COUNT(*) FROM {TableName}";
            return await ExecuteScalarAsync<int>(sql);
        }
        else
        {
            sql = $@"
                SELECT COUNT(*) FROM {TableName}
                WHERE COALESCE((bootstrap_config->>'disabled')::boolean, false) = false";
            return await ExecuteScalarAsync<int>(sql);
        }
    }

    /// <summary>
    /// Get a configuration entry by app domain (only enabled by default)
    /// Case-insensitive lookup.
    /// </summary>
    public async Task<ConfigEntry?> GetByAppDomainAsync(string appDomain, bool enabledOnly = true)
    {
        string sql;
        if (enabledOnly)
        {
            sql = $@"
                SELECT idx, app_domain, user_config, service_config, bootstrap_config
                FROM {TableName}
                WHERE LOWER(app_domain) = LOWER(@appDomain)
                  AND COALESCE((bootstrap_config->>'disabled')::boolean, false) = false";
        }
        else
        {
            sql = $@"
                SELECT idx, app_domain, user_config, service_config, bootstrap_config
                FROM {TableName}
                WHERE LOWER(app_domain) = LOWER(@appDomain)";
        }

        return await ExecuteQueryFirstOrDefaultAsync(
            sql,
            p => p.AddWithValue("@appDomain", appDomain),
            MapConfigEntry);
    }

    /// <summary>
    /// Get a configuration entry by idx (for admin editing)
    /// </summary>
    public async Task<ConfigEntry?> GetByIdxAsync(int idx)
    {
        const string sql = $@"
            SELECT idx, app_domain, user_config, service_config, bootstrap_config
            FROM {TableName}
            WHERE idx = @idx";

        return await ExecuteQueryFirstOrDefaultAsync(
            sql,
            p => p.AddWithValue("@idx", idx),
            MapConfigEntry);
    }

    /// <summary>
    /// Create a new configuration entry
    /// </summary>
    public async Task<ConfigEntry> CreateAsync(ConfigEntry entry)
    {
        const string sql = $@"
            INSERT INTO {TableName} (app_domain, user_config, service_config, bootstrap_config)
            VALUES (@appDomain, @userConfig, @serviceConfig, @bootstrapConfig)
            RETURNING idx";

        var idx = await ExecuteScalarAsync<int>(
            sql,
            p =>
            {
                p.AddWithValue("@appDomain", entry.AppDomain);
                p.AddWithValue("@userConfig", NpgsqlDbType.Jsonb,
                    entry.UserConfig is not null ? entry.UserConfig.RootElement.GetRawText() : DBNull.Value);
                p.AddWithValue("@serviceConfig", NpgsqlDbType.Jsonb,
                    entry.ServiceConfig is not null ? entry.ServiceConfig.RootElement.GetRawText() : DBNull.Value);
                p.AddWithValue("@bootstrapConfig", NpgsqlDbType.Jsonb,
                    entry.BootstrapConfig is not null ? entry.BootstrapConfig.RootElement.GetRawText() : DBNull.Value);
            });

        entry.Idx = idx;
        return entry;
    }

    /// <summary>
    /// Update an existing configuration entry by idx
    /// </summary>
    public async Task<bool> UpdateByIdxAsync(int idx, ConfigEntry updated)
    {
        const string sql = $@"
            UPDATE {TableName}
            SET app_domain = @appDomain,
                user_config = @userConfig,
                service_config = @serviceConfig,
                bootstrap_config = @bootstrapConfig
            WHERE idx = @idx";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            p =>
            {
                p.AddWithValue("@idx", idx);
                p.AddWithValue("@appDomain", updated.AppDomain);
                p.AddWithValue("@userConfig", NpgsqlDbType.Jsonb,
                    updated.UserConfig is not null ? updated.UserConfig.RootElement.GetRawText() : DBNull.Value);
                p.AddWithValue("@serviceConfig", NpgsqlDbType.Jsonb,
                    updated.ServiceConfig is not null ? updated.ServiceConfig.RootElement.GetRawText() : DBNull.Value);
                p.AddWithValue("@bootstrapConfig", NpgsqlDbType.Jsonb,
                    updated.BootstrapConfig is not null ? updated.BootstrapConfig.RootElement.GetRawText() : DBNull.Value);
            });

        return rowsAffected > 0;
    }

    /// <summary>
    /// Update an existing configuration entry by app domain (case-insensitive lookup)
    /// </summary>
    public async Task<bool> UpdateByAppDomainAsync(string appDomain, ConfigEntry updated)
    {
        const string sql = $@"
            UPDATE {TableName}
            SET app_domain = @newAppDomain,
                user_config = @userConfig,
                service_config = @serviceConfig,
                bootstrap_config = @bootstrapConfig
            WHERE LOWER(app_domain) = LOWER(@appDomain)";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            p =>
            {
                p.AddWithValue("@appDomain", appDomain);
                p.AddWithValue("@newAppDomain", updated.AppDomain);
                p.AddWithValue("@userConfig", NpgsqlDbType.Jsonb,
                    updated.UserConfig is not null ? updated.UserConfig.RootElement.GetRawText() : DBNull.Value);
                p.AddWithValue("@serviceConfig", NpgsqlDbType.Jsonb,
                    updated.ServiceConfig is not null ? updated.ServiceConfig.RootElement.GetRawText() : DBNull.Value);
                p.AddWithValue("@bootstrapConfig", NpgsqlDbType.Jsonb,
                    updated.BootstrapConfig is not null ? updated.BootstrapConfig.RootElement.GetRawText() : DBNull.Value);
            });

        return rowsAffected > 0;
    }

    /// <summary>
    /// Delete a configuration entry by app domain (case-insensitive lookup)
    /// </summary>
    public async Task<bool> DeleteByAppDomainAsync(string appDomain)
    {
        const string sql = $"DELETE FROM {TableName} WHERE LOWER(app_domain) = LOWER(@appDomain)";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            p => p.AddWithValue("@appDomain", appDomain));

        return rowsAffected > 0;
    }

    /// <summary>
    /// Delete a configuration entry by idx
    /// </summary>
    public async Task<bool> DeleteByIdxAsync(int idx)
    {
        const string sql = $"DELETE FROM {TableName} WHERE idx = @idx";

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            p => p.AddWithValue("@idx", idx));

        return rowsAffected > 0;
    }

    /// <summary>
    /// Set the disabled status of an entry by modifying the bootstrap_config JSONB.
    /// Uses PostgreSQL JSONB operators for atomic update.
    /// </summary>
    public async Task<bool> SetDisabledAsync(int idx, bool disabled)
    {
        string sql;
        if (disabled)
        {
            // Set disabled = true in bootstrap_config (creates the field if it doesn't exist)
            sql = $@"
                UPDATE {TableName}
                SET bootstrap_config = COALESCE(bootstrap_config, '{{}}'::jsonb) || '{{""disabled"": true}}'::jsonb
                WHERE idx = @idx";
        }
        else
        {
            // Remove the disabled key from bootstrap_config
            sql = $@"
                UPDATE {TableName}
                SET bootstrap_config = bootstrap_config - 'disabled'
                WHERE idx = @idx";
        }

        var rowsAffected = await ExecuteNonQueryAsync(
            sql,
            p => p.AddWithValue("@idx", idx));

        return rowsAffected > 0;
    }
}
