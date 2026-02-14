namespace IFOllama.Classes.Models;

/// <summary>
/// Links a conversation to a git repository, tracking the working branch and enabled state.
/// </summary>
public class ConversationRepository
{
    public string ConversationId { get; set; } = string.Empty;
    public string RepositoryId { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime CreatedAt { get; set; }
}
