namespace IFOllama.Classes.Models;

/// <summary>
/// Represents a git repository that a user has registered for use in conversations.
/// </summary>
public class GitRepositoryConfig
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public GitCredentialType CredentialType { get; set; }
    public string Credential { get; set; } = string.Empty;
    public string LocalPath { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = "main";
    public ProjectType DetectedProjectType { get; set; }
    public DateTime ClonedAt { get; set; }
    public DateTime LastPulledAt { get; set; }
}
