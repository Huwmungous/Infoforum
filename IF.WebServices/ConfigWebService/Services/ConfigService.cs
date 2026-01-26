using ConfigWebService.Entities;
using ConfigWebService.Repositories;

namespace ConfigWebService.Services;

public class ConfigService(ConfigRepository repo)
{
    public Task<List<ConfigEntry>> GetBatchAsync(int offset, int limit, bool includeDisabled = true)
        => repo.GetBatchAsync(offset, limit, includeDisabled);

    public Task<int> GetCountAsync(bool includeDisabled = true)
        => repo.GetCountAsync(includeDisabled);

    public Task<ConfigEntry?> GetByAppDomainAsync(string appDomain, bool enabledOnly = true)
        => repo.GetByAppDomainAsync(appDomain, enabledOnly);

    public Task<ConfigEntry?> GetByIdxAsync(int idx)
        => repo.GetByIdxAsync(idx);

    public Task<ConfigEntry> CreateAsync(ConfigEntry entry)
        => repo.CreateAsync(entry);

    public Task<bool> UpdateByIdxAsync(int idx, ConfigEntry entry)
        => repo.UpdateByIdxAsync(idx, entry);

    public Task<bool> UpdateByAppDomainAsync(string appDomain, ConfigEntry entry)
        => repo.UpdateByAppDomainAsync(appDomain, entry);

    /// <summary>
    /// Set enabled/disabled status by modifying JSONB
    /// Note: enabled=true means disabled=false, enabled=false means disabled=true
    /// </summary>
    public Task<bool> SetEnabledAsync(int idx, bool enabled)
        => repo.SetDisabledAsync(idx, !enabled);

    public Task<bool> DeleteByAppDomainAsync(string appDomain)
        => repo.DeleteByAppDomainAsync(appDomain);

    public Task<bool> DeleteByIdxAsync(int idx)
        => repo.DeleteByIdxAsync(idx);
}
