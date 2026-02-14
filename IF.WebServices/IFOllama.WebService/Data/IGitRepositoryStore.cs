using IFOllama.Classes.Models;

namespace IFOllama.WebService.Data;

/// <summary>
/// Persistence contract for user git repository configurations and conversation-repo links.
/// </summary>
public interface IGitRepositoryStore
{
    /// <summary>Returns all repositories registered by a user.</summary>
    Task<List<GitRepositoryConfig>> ListAsync(string userId);

    /// <summary>Gets a single repository by id, verifying ownership.</summary>
    Task<GitRepositoryConfig?> GetAsync(string repoId, string userId);

    /// <summary>Saves a new or updated repository configuration.</summary>
    Task SaveAsync(GitRepositoryConfig config);

    /// <summary>Removes a repository configuration and its data.</summary>
    Task RemoveAsync(string repoId, string userId);

    /// <summary>Gets all conversation-repo links for a conversation.</summary>
    Task<List<ConversationRepository>> GetConversationReposAsync(string conversationId);

    /// <summary>Links a repository to a conversation with its working branch.</summary>
    Task LinkRepoToConversationAsync(ConversationRepository link);

    /// <summary>Updates the enabled state of a repo for a conversation.</summary>
    Task SetRepoEnabledAsync(string conversationId, string repoId, bool enabled);

    /// <summary>Removes a conversation-repo link.</summary>
    Task UnlinkRepoFromConversationAsync(string conversationId, string repoId);
}
