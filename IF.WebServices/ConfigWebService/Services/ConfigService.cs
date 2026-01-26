using ConfigWebService.Entities;
using ConfigWebService.Repositories;

namespace ConfigWebService.Services;

public class ConfigService(ConfigRepository repo)
{
    public Task<ConfigEntry?> GetByAppDomainAndTypeAsync(string appDomain, string type, bool enabledOnly = true)
        => repo.GetByAppDomainAndTypeAsync(appDomain, type, enabledOnly);

    public Task<ConfigEntry?> GetByIdxAsync(int idx)
        => repo.GetByIdxAsync(idx);
}
