using ConfigWebService.Entities;
using ConfigWebService.Repositories;

namespace ConfigWebService.Services;

public class ConfigService
{
    private readonly ConfigRepository _repo;

    public ConfigService(ConfigRepository repo)
    {
        _repo = repo;
    }

    public Task<List<ConfigEntry>> GetBatchAsync(int offset, int limit)
        => _repo.GetBatchAsync(offset, limit);

    public Task<ConfigEntry?> GetAsync(string realm, string client)
        => _repo.GetByRealmClientAsync(realm, client);

    public Task<ConfigEntry> CreateAsync(ConfigEntry entry)
        => _repo.CreateAsync(entry);

    public Task<bool> UpdateAsync(string realm, string client, ConfigEntry entry)
        => _repo.UpdateAsync(realm, client, entry);

    public Task<bool> DeleteAsync(string realm, string client)
        => _repo.DeleteAsync(realm, client);
}
