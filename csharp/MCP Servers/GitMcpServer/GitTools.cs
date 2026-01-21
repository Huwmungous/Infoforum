using LibGit2Sharp;
using System.Text.Json;

namespace GitMcpServer;

public static class GitTools
{
    public static Task<object> GitInit(JsonElement args)
    {
        var path = args.GetProperty("path").GetString()!;
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        Repository.Init(path);
        return Task.FromResult<object>(new { success = true, path, message = "Repository initialized" });
    }

    public static Task<object> GitStatus(JsonElement args)
    {
        var repoPath = args.GetProperty("repoPath").GetString()!;
        using var repo = new Repository(repoPath);
        var status = repo.RetrieveStatus();
        var modified = status.Modified.Select(m => m.FilePath).ToList();
        var added = status.Added.Select(a => a.FilePath).ToList();
        var removed = status.Removed.Select(r => r.FilePath).ToList();
        var untracked = status.Untracked.Select(u => u.FilePath).ToList();
        return Task.FromResult<object>(new { success = true, modified, added, removed, untracked, isDirty = status.IsDirty });
    }

    public static Task<object> GitAdd(JsonElement args)
    {
        var repoPath = args.GetProperty("repoPath").GetString()!;
        var filePattern = args.GetProperty("filePattern").GetString()!;
        using var repo = new Repository(repoPath);
        Commands.Stage(repo, filePattern);
        return Task.FromResult<object>(new { success = true, filePattern, message = "Files staged" });
    }

    public static Task<object> GitCommit(JsonElement args)
    {
        var repoPath = args.GetProperty("repoPath").GetString()!;
        var message = args.GetProperty("message").GetString()!;
        var author = args.TryGetProperty("author", out var a) ? a.GetString() : "MCP User";
        var email = args.TryGetProperty("email", out var e) ? e.GetString() : "mcp@localhost";
        using var repo = new Repository(repoPath);
        var signature = new Signature(author, email, DateTimeOffset.Now);
        var commit = repo.Commit(message, signature, signature);
        return Task.FromResult<object>(new { success = true, commitSha = commit.Sha, message });
    }

    public static Task<object> GitDiff(JsonElement args)
    {
        var repoPath = args.GetProperty("repoPath").GetString()!;
        var file = args.TryGetProperty("file", out var f) ? f.GetString() : null;
        using var repo = new Repository(repoPath);
        var diff = file != null ? repo.Diff.Compare<Patch>([file]) : repo.Diff.Compare<Patch>();
        return Task.FromResult<object>(new { success = true, diff = diff.Content, changes = diff.LinesAdded + diff.LinesDeleted });
    }

    public static Task<object> GitLog(JsonElement args)
    {
        var repoPath = args.GetProperty("repoPath").GetString()!;
        var maxCount = args.TryGetProperty("maxCount", out var m) ? m.GetInt32() : 10;
        using var repo = new Repository(repoPath);
        var commits = repo.Commits.Take(maxCount).Select(c => new
        {
            sha = c.Sha[..8],
            message = c.MessageShort,
            author = c.Author.Name,
            date = c.Author.When.ToString("o")
        }).ToList();
        return Task.FromResult<object>(new { success = true, commits });
    }

    public static Task<object> GitBranchList(JsonElement args)
    {
        var repoPath = args.GetProperty("repoPath").GetString()!;
        using var repo = new Repository(repoPath);
        var branches = repo.Branches.Select(b => new { name = b.FriendlyName, isHead = b.IsCurrentRepositoryHead, isRemote = b.IsRemote }).ToList();
        return Task.FromResult<object>(new { success = true, branches, current = repo.Head.FriendlyName });
    }

    public static Task<object> GitBranchCreate(JsonElement args)
    {
        var repoPath = args.GetProperty("repoPath").GetString()!;
        var branchName = args.GetProperty("branchName").GetString()!;
        using var repo = new Repository(repoPath);
        var branch = repo.CreateBranch(branchName);
        return Task.FromResult<object>(new { success = true, branchName = branch.FriendlyName });
    }

    public static Task<object> GitCheckout(JsonElement args)
    {
        var repoPath = args.GetProperty("repoPath").GetString()!;
        var branchName = args.GetProperty("branchName").GetString()!;
        using var repo = new Repository(repoPath);
        Commands.Checkout(repo, branchName);
        return Task.FromResult<object>(new { success = true, branchName, message = "Checked out" });
    }

    public static Task<object> GitClone(JsonElement args)
    {
        var url = args.GetProperty("url").GetString()!;
        var localPath = args.GetProperty("localPath").GetString()!;
        var path = Repository.Clone(url, localPath);
        return Task.FromResult<object>(new { success = true, url, localPath = path });
    }

    public static Task<object> GitPull(JsonElement args)
    {
        var repoPath = args.GetProperty("repoPath").GetString()!;
        using var repo = new Repository(repoPath);
        var signature = new Signature("MCP User", "mcp@localhost", DateTimeOffset.Now);
        Commands.Pull(repo, signature, null);
        return Task.FromResult<object>(new { success = true, message = "Pull completed" });
    }
}
