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

        var entries = await db.ConfigEntries.AsNoTracking()
            .OrderBy(x => x.Realm)
            .ThenBy(x => x.Client)
            .ToListAsync();

        if (!includeDisabled)
            entries = entries.Where(x => !x.IsDisabled).ToList();

        return entries
            .Skip(offset)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Get total count of entries
    /// </summary>
    public async Task<int> GetCountAsync(bool includeDisabled = true)
    {
        var entries = await db.ConfigEntries.AsNoTracking().ToListAsync();

        if (!includeDisabled)
            return entries.Count(x => !x.IsDisabled);

        return entries.Count;
    }

    /// <summary>
    /// Get a configuration entry by realm and client (only enabled by default)
    /// </summary>
    public async Task<ConfigEntry?> GetByRealmClientAsync(string realm, string client, bool enabledOnly = true)
    {
        var entry = await db.ConfigEntries.AsNoTracking()
            .Where(x => x.Realm == realm && x.Client == client)
            .SingleOrDefaultAsync();

        if (entry is null)
            return null;

        if (enabledOnly && entry.IsDisabled)
            return null;

        return entry;
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

    /// <summary>
    /// Set the disabled status of an entry by modifying the bootstrap_config JSONB
    /// </summary>
    public async Task<bool> SetDisabledAsync(int idx, bool disabled)
    {
        var existing = await db.ConfigEntries
            .SingleOrDefaultAsync(x => x.Idx == idx);

        if (existing is null)
            return false;

        // Parse existing bootstrap config or create new
        var bootstrapDict = new Dictionary<string, object>();
        
        if (existing.BootstrapConfig is not null)
        {
            foreach (var prop in existing.BootstrapConfig.RootElement.EnumerateObject())
            {
                bootstrapDict[prop.Name] = prop.Value.Clone();
            }
        }

        // Set or remove disabled property
        if (disabled)
            bootstrapDict["disabled"] = true;
        else
            bootstrapDict.Remove("disabled");

        // Serialize back to JsonDocument
        var json = System.Text.Json.JsonSerializer.Serialize(bootstrapDict);
        existing.BootstrapConfig = System.Text.Json.JsonDocument.Parse(json);

        await db.SaveChangesAsync();
        return true;
    }
}
