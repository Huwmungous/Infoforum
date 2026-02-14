using IFOllama.Classes.Models;
using IFOllama.WebService.Data;
using IFOllama.WebService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IFOllama.WebService.Controllers;

[ApiController]
[Route("api/repositories")]
[Authorize(Policy = "MustBeIntelligenceUser")]
public class RepositoriesController(
    IGitRepositoryStore repoStore,
    IConversationStore conversationStore,
    GitWorkspaceService workspace,
    ILogger<RepositoriesController> logger) : ControllerBase
{
    // ── Repository CRUD ──────────────────────────────────────────────

    /// <summary>
    /// Lists all repositories registered by the user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListRepositories([FromQuery] string userId)
    {
        var repos = await repoStore.ListAsync(userId);
        // Don't expose credentials in the list response
        return Ok(repos.Select(r => new
        {
            r.Id,
            r.Name,
            r.Url,
            r.DefaultBranch,
            r.DetectedProjectType,
            r.ClonedAt,
            r.LastPulledAt
        }));
    }

    /// <summary>
    /// Clones and registers a new repository.
    /// </summary>
    [HttpPost("clone")]
    public async Task<IActionResult> CloneRepository([FromBody] CloneRequest request, [FromQuery] string userId)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Url))
            return BadRequest(new { error = "name and url are required" });

        try
        {
            var repo = await workspace.CloneRepositoryAsync(
                userId, request.Name, request.Url,
                request.CredentialType, request.Credential ?? string.Empty);

            return Ok(new
            {
                repo.Id,
                repo.Name,
                repo.Url,
                repo.DefaultBranch,
                repo.DetectedProjectType,
                repo.ClonedAt
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to clone repository {Url}", request.Url);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Pulls the latest changes on the default branch.
    /// </summary>
    [HttpPost("{repoId}/pull")]
    public async Task<IActionResult> PullLatest(string repoId, [FromQuery] string userId)
    {
        var repo = await repoStore.GetAsync(repoId, userId);
        if (repo == null) return NotFound(new { error = "Repository not found" });

        try
        {
            await workspace.PullLatestAsync(repo);
            return Ok(new { success = true, lastPulledAt = repo.LastPulledAt });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to pull repository {Name}", repo.Name);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Removes a repository and its cloned data.
    /// </summary>
    [HttpDelete("{repoId}")]
    public async Task<IActionResult> RemoveRepository(string repoId, [FromQuery] string userId)
    {
        var repo = await repoStore.GetAsync(repoId, userId);
        if (repo == null) return NotFound(new { error = "Repository not found" });

        await workspace.RemoveRepositoryFromDiskAsync(repo);
        await repoStore.RemoveAsync(repoId, userId);
        return Ok(new { success = true });
    }

    // ── Conversation-repo linking ────────────────────────────────────

    /// <summary>
    /// Gets all repos linked to a conversation, with their enabled state.
    /// </summary>
    [HttpGet("conversation/{conversationId}")]
    public async Task<IActionResult> GetConversationRepos(string conversationId, [FromQuery] string userId)
    {
        if (!await conversationStore.OwnsConversationAsync(conversationId, userId))
            return Forbid();

        var links = await repoStore.GetConversationReposAsync(conversationId);
        var userRepos = await repoStore.ListAsync(userId);

        // Enrich links with repo metadata
        var result = links.Select(link =>
        {
            var repo = userRepos.Find(r => r.Id == link.RepositoryId);
            return new
            {
                link.RepositoryId,
                Name = repo?.Name ?? "Unknown",
                Url = repo?.Url ?? "",
                DetectedProjectType = repo?.DetectedProjectType ?? ProjectType.Unknown,
                link.BranchName,
                link.Enabled,
                link.CreatedAt
            };
        });

        return Ok(result);
    }

    /// <summary>
    /// Links a repository to a conversation, creating a working branch.
    /// </summary>
    [HttpPost("conversation/{conversationId}/link")]
    public async Task<IActionResult> LinkRepoToConversation(
        string conversationId,
        [FromBody] LinkRepoRequest request,
        [FromQuery] string userId)
    {
        if (!await conversationStore.OwnsConversationAsync(conversationId, userId))
            return Forbid();

        var repo = await repoStore.GetAsync(request.RepositoryId, userId);
        if (repo == null) return NotFound(new { error = "Repository not found" });

        try
        {
            // Pull latest first
            await workspace.PullLatestAsync(repo);

            // Create the conversation branch
            var title = request.ConversationTitle ?? conversationId;
            var branchName = await workspace.CreateConversationBranchAsync(repo, conversationId, title);

            var link = new ConversationRepository
            {
                ConversationId = conversationId,
                RepositoryId = request.RepositoryId,
                BranchName = branchName,
                Enabled = true,
                CreatedAt = DateTime.UtcNow
            };

            await repoStore.LinkRepoToConversationAsync(link);

            return Ok(new
            {
                link.RepositoryId,
                repo.Name,
                link.BranchName,
                link.Enabled,
                repo.DetectedProjectType
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to link repo {RepoId} to conversation {ConversationId}",
                request.RepositoryId, conversationId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Toggles whether a repo is enabled for a conversation.
    /// </summary>
    [HttpPatch("conversation/{conversationId}/{repoId}/enabled")]
    public async Task<IActionResult> SetRepoEnabled(
        string conversationId,
        string repoId,
        [FromBody] EnabledRequest request,
        [FromQuery] string userId)
    {
        if (!await conversationStore.OwnsConversationAsync(conversationId, userId))
            return Forbid();

        await repoStore.SetRepoEnabledAsync(conversationId, repoId, request.Enabled);
        return Ok(new { success = true, enabled = request.Enabled });
    }

    /// <summary>
    /// Unlinks a repository from a conversation.
    /// </summary>
    [HttpDelete("conversation/{conversationId}/{repoId}")]
    public async Task<IActionResult> UnlinkRepo(
        string conversationId,
        string repoId,
        [FromQuery] string userId)
    {
        if (!await conversationStore.OwnsConversationAsync(conversationId, userId))
            return Forbid();

        await repoStore.UnlinkRepoFromConversationAsync(conversationId, repoId);
        return Ok(new { success = true });
    }

    // ── File operations ──────────────────────────────────────────────

    /// <summary>
    /// Gets the file tree of a repository's working copy.
    /// </summary>
    [HttpGet("{repoId}/tree")]
    public async Task<IActionResult> GetFileTree(string repoId, [FromQuery] string userId)
    {
        var repo = await repoStore.GetAsync(repoId, userId);
        if (repo == null) return NotFound(new { error = "Repository not found" });

        var tree = await workspace.GetFileTreeAsync(repo);
        return Ok(new { tree });
    }

    /// <summary>
    /// Reads a file from the working copy.
    /// </summary>
    [HttpGet("{repoId}/file")]
    public async Task<IActionResult> ReadFile(string repoId, [FromQuery] string path, [FromQuery] string userId)
    {
        var repo = await repoStore.GetAsync(repoId, userId);
        if (repo == null) return NotFound(new { error = "Repository not found" });

        try
        {
            var content = await workspace.ReadFileAsync(repo, path);
            return Ok(new { path, content });
        }
        catch (FileNotFoundException)
        {
            return NotFound(new { error = $"File not found: {path}" });
        }
    }

    /// <summary>
    /// Writes a file to the working copy.
    /// </summary>
    [HttpPut("{repoId}/file")]
    public async Task<IActionResult> WriteFile(
        string repoId,
        [FromBody] WriteFileRequest request,
        [FromQuery] string userId)
    {
        var repo = await repoStore.GetAsync(repoId, userId);
        if (repo == null) return NotFound(new { error = "Repository not found" });

        await workspace.WriteFileAsync(repo, request.Path, request.Content);
        return Ok(new { success = true, path = request.Path });
    }

    /// <summary>
    /// Deletes a file from the working copy.
    /// </summary>
    [HttpDelete("{repoId}/file")]
    public async Task<IActionResult> DeleteFile(
        string repoId,
        [FromQuery] string path,
        [FromQuery] string userId)
    {
        var repo = await repoStore.GetAsync(repoId, userId);
        if (repo == null) return NotFound(new { error = "Repository not found" });

        await workspace.DeleteFileAsync(repo, path);
        return Ok(new { success = true, path });
    }

    // ── Build ────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the project in the working copy using the detected toolchain.
    /// </summary>
    [HttpPost("{repoId}/build")]
    public async Task<IActionResult> Build(
        string repoId,
        [FromBody] BuildRequest? request,
        [FromQuery] string userId)
    {
        var repo = await repoStore.GetAsync(repoId, userId);
        if (repo == null) return NotFound(new { error = "Repository not found" });

        var result = await workspace.BuildAsync(repo, request?.ProjectPath);
        return Ok(result);
    }

    // ── Commit & merge ───────────────────────────────────────────────

    /// <summary>
    /// Gets the git status of a repository's working copy.
    /// </summary>
    [HttpGet("{repoId}/status")]
    public async Task<IActionResult> GetStatus(string repoId, [FromQuery] string userId)
    {
        var repo = await repoStore.GetAsync(repoId, userId);
        if (repo == null) return NotFound(new { error = "Repository not found" });

        var status = await workspace.GetStatusAsync(repo);
        return Ok(new { status });
    }

    /// <summary>
    /// Gets the diff of uncommitted changes.
    /// </summary>
    [HttpGet("{repoId}/diff")]
    public async Task<IActionResult> GetDiff(string repoId, [FromQuery] string userId)
    {
        var repo = await repoStore.GetAsync(repoId, userId);
        if (repo == null) return NotFound(new { error = "Repository not found" });

        var diff = await workspace.GetDiffAsync(repo);
        return Ok(new { diff });
    }

    /// <summary>
    /// Commits all changes in the working copy.
    /// </summary>
    [HttpPost("{repoId}/commit")]
    public async Task<IActionResult> Commit(
        string repoId,
        [FromBody] CommitRequest request,
        [FromQuery] string userId)
    {
        var repo = await repoStore.GetAsync(repoId, userId);
        if (repo == null) return NotFound(new { error = "Repository not found" });

        try
        {
            var result = await workspace.CommitAllAsync(repo, request.Message,
                request.AuthorName ?? "IFOllama", request.AuthorEmail ?? "ifollama@localhost");
            return Ok(new { success = true, output = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Merges the conversation branch into the default branch and pushes.
    /// </summary>
    [HttpPost("conversation/{conversationId}/{repoId}/merge")]
    public async Task<IActionResult> MergeAndPush(
        string conversationId,
        string repoId,
        [FromQuery] string userId)
    {
        if (!await conversationStore.OwnsConversationAsync(conversationId, userId))
            return Forbid();

        var repo = await repoStore.GetAsync(repoId, userId);
        if (repo == null) return NotFound(new { error = "Repository not found" });

        var links = await repoStore.GetConversationReposAsync(conversationId);
        var link = links.Find(l => l.RepositoryId == repoId);
        if (link == null) return NotFound(new { error = "Repository not linked to this conversation" });

        try
        {
            var result = await workspace.MergeAndPushAsync(repo, link.BranchName);
            return Ok(new { success = true, message = result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to merge and push for repo {RepoId}", repoId);
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Scaffold ─────────────────────────────────────────────────────

    /// <summary>
    /// Scaffolds a new project in the repository.
    /// </summary>
    [HttpPost("{repoId}/scaffold")]
    public async Task<IActionResult> ScaffoldProject(
        string repoId,
        [FromBody] ProjectScaffoldRequest request,
        [FromQuery] string userId)
    {
        var repo = await repoStore.GetAsync(repoId, userId);
        if (repo == null) return NotFound(new { error = "Repository not found" });

        try
        {
            var result = await workspace.ScaffoldProjectAsync(repo, request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to scaffold project in repo {RepoId}", repoId);
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Request DTOs ─────────────────────────────────────────────────

    public record CloneRequest(
        string Name,
        string Url,
        GitCredentialType CredentialType = GitCredentialType.PersonalAccessToken,
        string? Credential = null);

    public record LinkRepoRequest(string RepositoryId, string? ConversationTitle = null);

    public record EnabledRequest(bool Enabled);

    public record WriteFileRequest(string Path, string Content);

    public record BuildRequest(string? ProjectPath = null);

    public record CommitRequest(
        string Message,
        string? AuthorName = null,
        string? AuthorEmail = null);
}
