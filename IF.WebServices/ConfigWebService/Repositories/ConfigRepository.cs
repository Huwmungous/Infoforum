using Microsoft.EntityFrameworkCore;
using ConfigWebService.Data;
using ConfigWebService.Entities;

namespace ConfigWebService.Repositories;

public class ConfigRepository
{
    private readonly ConfigDbContext _db;

    public ConfigRepository(ConfigDbContext db)
    {
        _db = db;
    }

    // -----------------------------
    // EXISTING METHODS (UNCHANGED)
    // -----------------------------

    public async Task<List<ConfigEntry>> GetBatchAsync(int offset, int limit)
    {
        limit = Math.Min(limit, 100);

        return await _db.ConfigEntries
            .AsNoTracking()
            .OrderBy(x => x.Idx)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<ConfigEntry?> GetByRealmClientAsync(string realm, string client)
    {
        return await _db.ConfigEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(x =>
                x.Realm == realm &&
                x.Client == client);
    }

    public async Task<ConfigEntry> CreateAsync(ConfigEntry entry)
    {
        _db.ConfigEntries.Add(entry);
        await _db.SaveChangesAsync();
        return entry;
    }

    public async Task<bool> UpdateAsync(string realm, string client, ConfigEntry updated)
    {
        var existing = await _db.ConfigEntries
            .SingleOrDefaultAsync(x =>
                x.Realm == realm &&
                x.Client == client);

        if (existing is null)
            return false;

        existing.Realm = updated.Realm;
        existing.Client = updated.Client;
        existing.ServiceConfig = updated.ServiceConfig;
        existing.UserConfig = updated.UserConfig;
        existing.PatientConfig = updated.PatientConfig;
        existing.JsonB = updated.JsonB;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(string realm, string client)
    {
        var existing = await _db.ConfigEntries
            .SingleOrDefaultAsync(x =>
                x.Realm == realm &&
                x.Client == client);

        if (existing is null)
            return false;

        _db.ConfigEntries.Remove(existing);
        await _db.SaveChangesAsync();
        return true;
    }
}
