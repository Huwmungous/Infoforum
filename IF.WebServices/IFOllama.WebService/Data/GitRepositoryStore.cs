using IFOllama.Classes.Models;
using System.Text.Json;

namespace IFOllama.WebService.Data;

public class GitRepositoryStore : IGitRepositoryStore
{
    private readonly string _reposPath;
    private readonly string _linksPath;
    private readonly ILogger<GitRepositoryStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public GitRepositoryStore(IConfiguration configuration, ILogger<GitRepositoryStore> logger)
    {
        _logger = logger;
        var basePath = configuration["Storage:ConversationsPath"] ?? "Data/Conversations";
        _reposPath = Path.Combine(Path.GetDirectoryName(basePath) ?? "Data", "Repositories");
        _linksPath = Path.Combine(Path.GetDirectoryName(basePath) ?? "Data", "RepoLinks");
        Directory.CreateDirectory(_reposPath);
        Directory.CreateDirectory(_linksPath);
    }

    public async Task<List<GitRepositoryConfig>> ListAsync(string userId)
    {
        var result = new List<GitRepositoryConfig>();
        var userDir = Path.Combine(_reposPath, SanitisePath(userId));
        if (!Directory.Exists(userDir)) return result;

        foreach (var file in Directory.GetFiles(userDir, "*.json"))
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var config = await JsonSerializer.DeserializeAsync<GitRepositoryConfig>(stream);
                if (config != null)
                    result.Add(config);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read repo config: {File}", file);
            }
        }

        return result.OrderBy(r => r.Name).ToList();
    }

    public async Task<GitRepositoryConfig?> GetAsync(string repoId, string userId)
    {
        var filePath = GetRepoConfigPath(userId, repoId);
        if (!File.Exists(filePath)) return null;

        await using var stream = File.OpenRead(filePath);
        var config = await JsonSerializer.DeserializeAsync<GitRepositoryConfig>(stream);

        if (config?.UserId != userId) return null;
        return config;
    }

    public async Task SaveAsync(GitRepositoryConfig config)
    {
        var userDir = Path.Combine(_reposPath, SanitisePath(config.UserId));
        Directory.CreateDirectory(userDir);

        var filePath = Path.Combine(userDir, $"{config.Id}.json");
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(config, JsonOptions));

        _logger.LogInformation("Saved repo config {Id} ({Name}) for user {UserId}",
            config.Id, config.Name, config.UserId);
    }

    public async Task RemoveAsync(string repoId, string userId)
    {
        var filePath = GetRepoConfigPath(userId, repoId);
        if (!File.Exists(filePath)) return;

        // Verify ownership
        await using var stream = File.OpenRead(filePath);
        var config = await JsonSerializer.DeserializeAsync<GitRepositoryConfig>(stream);
        stream.Close();

        if (config?.UserId != userId)
            throw new UnauthorizedAccessException();

        File.Delete(filePath);
        _logger.LogInformation("Removed repo config {Id} for user {UserId}", repoId, userId);
    }

    public async Task<List<ConversationRepository>> GetConversationReposAsync(string conversationId)
    {
        var filePath = GetLinksFilePath(conversationId);
        if (!File.Exists(filePath)) return [];

        await using var stream = File.OpenRead(filePath);
        var links = await JsonSerializer.DeserializeAsync<List<ConversationRepository>>(stream);
        return links ?? [];
    }

    public async Task LinkRepoToConversationAsync(ConversationRepository link)
    {
        var filePath = GetLinksFilePath(link.ConversationId);
        var links = await GetConversationReposAsync(link.ConversationId);

        // Remove existing link for this repo if any
        links.RemoveAll(l => l.RepositoryId == link.RepositoryId);
        links.Add(link);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(links, JsonOptions));

        _logger.LogInformation("Linked repo {RepoId} to conversation {ConversationId} on branch {Branch}",
            link.RepositoryId, link.ConversationId, link.BranchName);
    }

    public async Task SetRepoEnabledAsync(string conversationId, string repoId, bool enabled)
    {
        var links = await GetConversationReposAsync(conversationId);
        var link = links.Find(l => l.RepositoryId == repoId);
        if (link == null) return;

        link.Enabled = enabled;

        var filePath = GetLinksFilePath(conversationId);
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(links, JsonOptions));

        _logger.LogInformation("Set repo {RepoId} enabled={Enabled} for conversation {ConversationId}",
            repoId, enabled, conversationId);
    }

    public async Task UnlinkRepoFromConversationAsync(string conversationId, string repoId)
    {
        var links = await GetConversationReposAsync(conversationId);
        links.RemoveAll(l => l.RepositoryId == repoId);

        var filePath = GetLinksFilePath(conversationId);
        await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(links, JsonOptions));

        _logger.LogInformation("Unlinked repo {RepoId} from conversation {ConversationId}",
            repoId, conversationId);
    }

    private string GetRepoConfigPath(string userId, string repoId) =>
        Path.Combine(_reposPath, SanitisePath(userId), $"{repoId}.json");

    private string GetLinksFilePath(string conversationId) =>
        Path.Combine(_linksPath, $"{conversationId}.json");

    private static string SanitisePath(string input) =>
        string.Join("_", input.Split(Path.GetInvalidFileNameChars()));
}
