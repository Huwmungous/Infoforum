using Microsoft.EntityFrameworkCore;
using ConfigWebService.Data;
using ConfigWebService.Entities;

namespace ConfigWebService.Repositories;

public class ConfigRepository(ConfigDbContext db)
{
    /// <summary>
    /// Get a batch of configuration entries with pagination (includes disabled for admin)
    /// </summary>
    public async Task<List<ConfigEntry>> GetBatchAsync(int offset, int limit, bool includeDisabled = true)
    {
        limit = Math.Min(limit, 100);

        var query = db.ConfigEntries.AsNoTracking();

        if (!includeDisabled)
            query = query.Where(x => x.Enabled);

        return await query
            .OrderBy(x => x.Realm)
            .ThenBy(x => x.Client)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Get total count of entries
    /// </summary>
    public async Task<int> GetCountAsync(bool includeDisabled = true)
    {
        var query = db.ConfigEntries.AsQueryable();

        if (!includeDisabled)
            query = query.Where(x => x.Enabled);

        return await query.CountAsync();
    }

    /// <summary>
    /// Get a configuration entry by realm and client (only enabled by default)
    /// </summary>
    public async Task<ConfigEntry?> GetByRealmClientAsync(string realm, string client, bool enabledOnly = true)
    {
        var query = db.ConfigEntries.AsNoTracking()
            .Where(x => x.Realm == realm && x.Client == client);

        if (enabledOnly)
            query = query.Where(x => x.Enabled);

        return await query.SingleOrDefaultAsync();
    }

    /// <summary>
    /// Get a configuration entry by idx (for admin editing)
    /// </summary>
    public async Task<ConfigEntry?> GetByIdxAsync(int idx)
    {
        return await db.ConfigEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Idx == idx);
    }

    /// <summary>
    /// Create a new configuration entry
    /// </summary>
    public async Task<ConfigEntry> CreateAsync(ConfigEntry entry)
    {
        db.ConfigEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    /// <summary>
    /// Update an existing configuration entry by idx
    /// </summary>
    public async Task<bool> UpdateByIdxAsync(int idx, ConfigEntry updated)
    {
        var existing = await db.ConfigEntries
            .SingleOrDefaultAsync(x => x.Idx == idx);

        if (existing is null)
            return false;

        existing.Realm = updated.Realm;
        existing.Client = updated.Client;
        existing.UserConfig = updated.UserConfig;
        existing.ServiceConfig = updated.ServiceConfig;
        existing.BootstrapConfig = updated.BootstrapConfig;
        existing.Enabled = updated.Enabled;

        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Update an existing configuration entry by realm and client
    /// </summary>
    public async Task<bool> UpdateAsync(string realm, string client, ConfigEntry updated)
    {
        var existing = await db.ConfigEntries
            .SingleOrDefaultAsync(x => x.Realm == realm && x.Client == client);

        if (existing is null)
            return false;

        existing.Realm = updated.Realm;
        existing.Client = updated.Client;
        existing.UserConfig = updated.UserConfig;
        existing.ServiceConfig = updated.ServiceConfig;
        existing.BootstrapConfig = updated.BootstrapConfig;
        existing.Enabled = updated.Enabled;

        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Toggle the enabled status of an entry
    /// </summary>
    public async Task<bool> SetEnabledAsync(int idx, bool enabled)
    {
        var existing = await db.ConfigEntries
            .SingleOrDefaultAsync(x => x.Idx == idx);

        if (existing is null)
            return false;

        existing.Enabled = enabled;
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Delete a configuration entry
    /// </summary>
    public async Task<bool> DeleteAsync(string realm, string client)
    {
        var existing = await db.ConfigEntries
            .SingleOrDefaultAsync(x => x.Realm == realm && x.Client == client);

        if (existing is null)
            return false;

        db.ConfigEntries.Remove(existing);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Delete a configuration entry by idx
    /// </summary>
    public async Task<bool> DeleteByIdxAsync(int idx)
    {
        var existing = await db.ConfigEntries
            .SingleOrDefaultAsync(x => x.Idx == idx);

        if (existing is null)
            return false;

        db.ConfigEntries.Remove(existing);
        await db.SaveChangesAsync();
        return true;
    }
}
