using System.Data.Common;
using System.Text.Json;
using ConfigWebService.Entities;
using IFGlobal.DataAccess;
using IFGlobal.Models;
using Microsoft.Extensions.Logging;

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
            Config = reader.IsDBNull(reader.GetOrdinal("config"))
                ? null
                : JsonDocument.Parse(reader.GetString(reader.GetOrdinal("config")))
        };
    }

    /// <summary>
    /// Get a configuration entry by app domain and type (only enabled by default)
    /// Case-insensitive lookup.
    /// </summary>
    public async Task<ConfigEntry?> GetByAppDomainAndTypeAsync(string appDomain, string type, bool enabledOnly = true)
    {
        string sql;
        if (enabledOnly)
        {
            sql = $@"
                SELECT idx, app_domain, type, config
                FROM {TableName}
                WHERE LOWER(app_domain) = LOWER(@appDomain)
                  AND LOWER(type) = LOWER(@type)
                  AND COALESCE((config->>'disabled')::boolean, false) = false";
        }
        else
        {
            sql = $@"
                SELECT idx, app_domain, type, config
                FROM {TableName}
                WHERE LOWER(app_domain) = LOWER(@appDomain)
                  AND LOWER(type) = LOWER(@type)";
        }

        return await ExecuteQueryFirstOrDefaultAsync(
            sql,
            p =>
            {
                p.AddWithValue("@appDomain", appDomain);
                p.AddWithValue("@type", type);
            },
            MapConfigEntry);
    }

    /// <summary>
    /// Get a configuration entry by idx
    /// </summary>
    public async Task<ConfigEntry?> GetByIdxAsync(int idx)
    {
        const string sql = $@"
            SELECT idx, app_domain, type, config
            FROM {TableName}
            WHERE idx = @idx";

        return await ExecuteQueryFirstOrDefaultAsync(
            sql,
            p => p.AddWithValue("@idx", idx),
            MapConfigEntry);
    }
}
