using IFOllama.Classes.Models;
using IFOllama.WebService.Data;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace IFOllama.WebService.Services;

/// <summary>
/// Handles all git, file, build, and scaffolding operations on repository working copies.
/// Supports .NET, React/TypeScript, Angular/TypeScript, and Delphi project types.
/// </summary>
public partial class GitWorkspaceService
{
    private readonly string _reposBasePath;
    private readonly IGitRepositoryStore _repoStore;
    private readonly ILogger<GitWorkspaceService> _logger;
    private readonly IConfiguration _config;

    public GitWorkspaceService(
        IConfiguration config,
        IGitRepositoryStore repoStore,
        ILogger<GitWorkspaceService> logger)
    {
        _config = config;
        _repoStore = repoStore;
        _logger = logger;
        _reposBasePath = config["Storage:ReposPath"] ?? "/data/repos";
        Directory.CreateDirectory(_reposBasePath);
    }

    // ═══════════════════════════════════════════════════════════════
    //  Git operations
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Clones a repository for a user, storing credentials for future operations.
    /// </summary>
    public async Task<GitRepositoryConfig> CloneRepositoryAsync(
        string userId,
        string name,
        string url,
        GitCredentialType credentialType,
        string credential)
    {
        var repoId = Guid.NewGuid().ToString("N");
        var localPath = Path.Combine(_reposBasePath, SanitisePath(userId), SanitisePath(name));

        if(Directory.Exists(localPath))
        {
            _logger.LogInformation("Repository directory already exists at {Path}, removing first", localPath);
            ForceDeleteDirectory(localPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

        var cloneUrl = BuildAuthenticatedUrl(url, credentialType, credential);

        var result = await RunGitAsync(
            Path.GetDirectoryName(localPath)!,
            $"clone {cloneUrl} {Path.GetFileName(localPath)}",
            credentialType == GitCredentialType.SshKey ? credential : null);

        if(result.ExitCode != 0)
            throw new InvalidOperationException($"Git clone failed: {result.Errors}");

        // Detect default branch
        var branchResult = await RunGitAsync(localPath, "rev-parse --abbrev-ref HEAD");
        var defaultBranch = branchResult.Output.Trim();
        if(string.IsNullOrEmpty(defaultBranch)) defaultBranch = "main";

        // Detect project type
        var projectType = DetectProjectType(localPath);

        var repoConfig = new GitRepositoryConfig
        {
            Id = repoId,
            UserId = userId,
            Name = name,
            Url = url,
            CredentialType = credentialType,
            Credential = credential,
            LocalPath = localPath,
            DefaultBranch = defaultBranch,
            DetectedProjectType = projectType,
            ClonedAt = DateTime.UtcNow,
            LastPulledAt = DateTime.UtcNow
        };

        await _repoStore.SaveAsync(repoConfig);
        _logger.LogInformation("Cloned repository {Name} ({Url}) to {Path} — detected type: {Type}",
            name, url, localPath, projectType);

        return repoConfig;
    }

    /// <summary>
    /// Pulls latest changes on the default branch.
    /// </summary>
    public async Task PullLatestAsync(GitRepositoryConfig repo)
    {
        await RunGitAsync(repo.LocalPath, $"checkout {repo.DefaultBranch}");
        var pullUrl = BuildAuthenticatedUrl(repo.Url, repo.CredentialType, repo.Credential);

        var result = await RunGitAsync(repo.LocalPath, $"pull {pullUrl} {repo.DefaultBranch}",
            repo.CredentialType == GitCredentialType.SshKey ? repo.Credential : null);

        if(result.ExitCode != 0)
            _logger.LogWarning("Git pull had issues: {Errors}", result.Errors);

        repo.LastPulledAt = DateTime.UtcNow;
        await _repoStore.SaveAsync(repo);
    }

    /// <summary>
    /// Creates a conversation-specific branch from the default branch.
    /// </summary>
    public async Task<string> CreateConversationBranchAsync(
        GitRepositoryConfig repo,
        string conversationId,
        string conversationTitle)
    {
        var branchName = SanitiseBranchName($"{conversationId}_{conversationTitle}");

        await RunGitAsync(repo.LocalPath, $"checkout {repo.DefaultBranch}");

        var result = await RunGitAsync(repo.LocalPath, $"checkout -b {branchName}");
        if(result.ExitCode != 0)
        {
            result = await RunGitAsync(repo.LocalPath, $"checkout {branchName}");
            if(result.ExitCode != 0)
                throw new InvalidOperationException($"Failed to create/checkout branch {branchName}: {result.Errors}");
        }

        _logger.LogInformation("Created branch {Branch} in repo {Repo}", branchName, repo.Name);
        return branchName;
    }

    /// <summary>
    /// Checks out an existing conversation branch.
    /// </summary>
    public async Task CheckoutBranchAsync(GitRepositoryConfig repo, string branchName)
    {
        var result = await RunGitAsync(repo.LocalPath, $"checkout {branchName}");
        if(result.ExitCode != 0)
            throw new InvalidOperationException($"Failed to checkout branch {branchName}: {result.Errors}");
    }

    /// <summary>
    /// Gets the git status of the working copy.
    /// </summary>
    public async Task<string> GetStatusAsync(GitRepositoryConfig repo)
    {
        var result = await RunGitAsync(repo.LocalPath, "status --porcelain");
        return result.Output;
    }

    /// <summary>
    /// Gets the diff of uncommitted changes.
    /// </summary>
    public async Task<string> GetDiffAsync(GitRepositoryConfig repo)
    {
        var result = await RunGitAsync(repo.LocalPath, "diff");
        var stagedDiff = await RunGitAsync(repo.LocalPath, "diff --cached");
        return result.Output + stagedDiff.Output;
    }

    /// <summary>
    /// Stages all changes and commits with the given message.
    /// </summary>
    public async Task<string> CommitAllAsync(
        GitRepositoryConfig repo,
        string message,
        string authorName = "IFOllama",
        string authorEmail = "ifollama@localhost")
    {
        await RunGitAsync(repo.LocalPath, "add -A");
        var result = await RunGitAsync(repo.LocalPath,
            $"commit -m \"{EscapeGitMessage(message)}\" --author=\"{authorName} <{authorEmail}>\"");

        if(result.ExitCode != 0)
        {
            if(result.Output.Contains("nothing to commit"))
                return "Nothing to commit — working tree clean.";
            throw new InvalidOperationException($"Git commit failed: {result.Errors}");
        }

        _logger.LogInformation("Committed changes in repo {Repo}: {Message}", repo.Name, message);
        return result.Output;
    }

    /// <summary>
    /// Merges conversation branch into default branch via fast-forward and pushes.
    /// </summary>
    public async Task<string> MergeAndPushAsync(GitRepositoryConfig repo, string branchName)
    {
        var checkoutResult = await RunGitAsync(repo.LocalPath, $"checkout {repo.DefaultBranch}");
        if(checkoutResult.ExitCode != 0)
            throw new InvalidOperationException($"Failed to checkout {repo.DefaultBranch}: {checkoutResult.Errors}");

        var mergeResult = await RunGitAsync(repo.LocalPath, $"merge --ff-only {branchName}");
        if(mergeResult.ExitCode != 0)
            throw new InvalidOperationException(
                $"Fast-forward merge failed. The branch may have diverged from {repo.DefaultBranch}. " +
                $"Details: {mergeResult.Errors}");

        var pushUrl = BuildAuthenticatedUrl(repo.Url, repo.CredentialType, repo.Credential);
        var pushResult = await RunGitAsync(repo.LocalPath, $"push {pushUrl} {repo.DefaultBranch}",
            repo.CredentialType == GitCredentialType.SshKey ? repo.Credential : null);

        if(pushResult.ExitCode != 0)
            throw new InvalidOperationException($"Push failed: {pushResult.Errors}");

        _logger.LogInformation("Merged branch {Branch} into {Default} and pushed for repo {Repo}",
            branchName, repo.DefaultBranch, repo.Name);

        return $"Successfully merged {branchName} into {repo.DefaultBranch} and pushed to origin.";
    }

    /// <summary>
    /// Removes a cloned repository from disk.
    /// </summary>
    public Task RemoveRepositoryFromDiskAsync(GitRepositoryConfig repo)
    {
        if(Directory.Exists(repo.LocalPath))
        {
            ForceDeleteDirectory(repo.LocalPath);
            _logger.LogInformation("Removed repository directory {Path}", repo.LocalPath);
        }

        return Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════
    //  File operations
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets the file tree of a repository as a structured listing.
    /// </summary>
    public Task<string> GetFileTreeAsync(GitRepositoryConfig repo, int maxDepth = 5)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Repository: {repo.Name} ({repo.DetectedProjectType})");
        sb.AppendLine($"Path: {repo.LocalPath}");
        sb.AppendLine("---");
        BuildFileTree(repo.LocalPath, repo.LocalPath, sb, 0, maxDepth);
        return Task.FromResult(sb.ToString());
    }

    /// <summary>
    /// Reads the content of a file in the working copy.
    /// </summary>
    public async Task<string> ReadFileAsync(GitRepositoryConfig repo, string relativePath)
    {
        var fullPath = ValidatePath(repo, relativePath);
        if(!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {relativePath}");

        return await File.ReadAllTextAsync(fullPath);
    }

    /// <summary>
    /// Writes or overwrites a file in the working copy.
    /// </summary>
    public async Task WriteFileAsync(GitRepositoryConfig repo, string relativePath, string content)
    {
        var fullPath = ValidatePath(repo, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(fullPath, content);
        _logger.LogInformation("Wrote file {Path} in repo {Repo}", relativePath, repo.Name);
    }

    /// <summary>
    /// Deletes a file from the working copy.
    /// </summary>
    public Task DeleteFileAsync(GitRepositoryConfig repo, string relativePath)
    {
        var fullPath = ValidatePath(repo, relativePath);
        if(File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger.LogInformation("Deleted file {Path} in repo {Repo}", relativePath, repo.Name);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Applies file modifications from AI output. Parses ===FILE:path=== blocks.
    /// Returns the list of files that were modified.
    /// </summary>
    public async Task<List<string>> ApplyFileModificationsAsync(GitRepositoryConfig repo, string aiOutput)
    {
        var modifiedFiles = new List<string>();
        var matches = FileBlockPattern().Matches(aiOutput);

        foreach(Match match in matches)
        {
            var filePath = match.Groups[1].Value.Trim();
            var content = match.Groups[2].Value.TrimStart('\r', '\n').TrimEnd();

            try
            {
                await WriteFileAsync(repo, filePath, content);
                modifiedFiles.Add(filePath);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to apply modification to {File}", filePath);
            }
        }

        return modifiedFiles;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Project type detection
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Detects the project type by examining files in the repository.
    /// </summary>
    public static ProjectType DetectProjectType(string repoPath)
    {
        // Check for .NET
        if(Directory.GetFiles(repoPath, "*.sln", SearchOption.TopDirectoryOnly).Length > 0 ||
            Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories)
                .Any(f => !f.Contains("bin") && !f.Contains("obj")) ||
            Directory.GetFiles(repoPath, "*.fsproj", SearchOption.AllDirectories)
                .Any(f => !f.Contains("bin") && !f.Contains("obj")))
        {
            return ProjectType.DotNet;
        }

        // Check for Delphi
        if(Directory.GetFiles(repoPath, "*.dproj", SearchOption.AllDirectories).Length > 0 ||
            Directory.GetFiles(repoPath, "*.dpr", SearchOption.AllDirectories).Length > 0 ||
            Directory.GetFiles(repoPath, "*.dpk", SearchOption.AllDirectories).Length > 0)
        {
            return ProjectType.Delphi;
        }

        // Check for Angular (has angular.json)
        if(File.Exists(Path.Combine(repoPath, "angular.json")) ||
            File.Exists(Path.Combine(repoPath, ".angular-cli.json")))
        {
            return ProjectType.AngularTypeScript;
        }

        // Check for React (package.json with react dependency)
        if(File.Exists(Path.Combine(repoPath, "package.json")))
        {
            try
            {
                var packageJson = File.ReadAllText(Path.Combine(repoPath, "package.json"));
                if(packageJson.Contains("\"react\"") || packageJson.Contains("\"next\""))
                    return ProjectType.ReactTypeScript;
            }
            catch
            {
                // Fall through
            }

            return ProjectType.ReactTypeScript;
        }

        return ProjectType.Unknown;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Build operations (polymorphic by project type)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds the project using the appropriate toolchain for the detected project type.
    /// </summary>
    public async Task<BuildResult> BuildAsync(GitRepositoryConfig repo, string? projectPath = null)
    {
        return repo.DetectedProjectType switch
        {
            ProjectType.DotNet => await BuildDotNetAsync(repo, projectPath),
            ProjectType.ReactTypeScript => await BuildReactTypeScriptAsync(repo),
            ProjectType.AngularTypeScript => await BuildAngularTypeScriptAsync(repo),
            ProjectType.Delphi => await BuildDelphiAsync(repo, projectPath),
            _ => new BuildResult
            {
                Success = false,
                ExitCode = -1,
                ProjectType = repo.DetectedProjectType,
                Errors = $"No build strategy configured for project type: {repo.DetectedProjectType}. " +
                         "Set the project type manually or ensure the project files are present."
            }
        };
    }

    private async Task<BuildResult> BuildDotNetAsync(GitRepositoryConfig repo, string? projectPath)
    {
        var target = projectPath ?? FindDotNetBuildTarget(repo.LocalPath);
        if(target == null)
        {
            return new BuildResult
            {
                Success = false,
                ExitCode = -1,
                ProjectType = ProjectType.DotNet,
                Errors = "No .sln or .csproj file found in the repository."
            };
        }

        var fullPath = Path.GetFullPath(Path.Combine(repo.LocalPath, target));
        ValidatePath(repo, target);

        var workDir = File.Exists(fullPath) ? Path.GetDirectoryName(fullPath)! : fullPath;
        var buildTarget = File.Exists(fullPath) ? Path.GetFileName(fullPath) : ".";

        // Restore first
        await RunProcessAsync(workDir, "dotnet", $"restore {buildTarget}");

        // Build
        var result = await RunProcessAsync(workDir, "dotnet", $"build {buildTarget} --no-restore");
        var diagnostics = ParseDotNetDiagnostics(result.Output + "\n" + result.Errors);

        return new BuildResult
        {
            Success = result.ExitCode == 0,
            ExitCode = result.ExitCode,
            Output = result.Output,
            Errors = result.Errors,
            ProjectType = ProjectType.DotNet,
            BuildCommand = $"dotnet build {buildTarget}",
            Diagnostics = diagnostics
        };
    }

    private async Task<BuildResult> BuildReactTypeScriptAsync(GitRepositoryConfig repo)
    {
        if(!Directory.Exists(Path.Combine(repo.LocalPath, "node_modules")))
        {
            var installResult = await RunProcessAsync(repo.LocalPath, "npm", "install");
            if(installResult.ExitCode != 0)
            {
                return new BuildResult
                {
                    Success = false,
                    ExitCode = installResult.ExitCode,
                    Output = installResult.Output,
                    Errors = installResult.Errors,
                    ProjectType = ProjectType.ReactTypeScript,
                    BuildCommand = "npm install"
                };
            }
        }

        // TypeScript type-check
        var tscResult = await RunProcessAsync(repo.LocalPath, "npx", "tsc --noEmit");
        var diagnostics = ParseTypeScriptDiagnostics(tscResult.Output + "\n" + tscResult.Errors);

        // Also run the build script
        var buildResult = await RunProcessAsync(repo.LocalPath, "npm", "run build");

        var combinedSuccess = tscResult.ExitCode == 0 && buildResult.ExitCode == 0;
        var combinedOutput = $"--- TypeScript check ---\n{tscResult.Output}\n--- Build ---\n{buildResult.Output}";
        var combinedErrors = $"{tscResult.Errors}\n{buildResult.Errors}".Trim();

        diagnostics.AddRange(ParseTypeScriptDiagnostics(buildResult.Errors));

        return new BuildResult
        {
            Success = combinedSuccess,
            ExitCode = combinedSuccess ? 0 : 1,
            Output = combinedOutput,
            Errors = combinedErrors,
            ProjectType = ProjectType.ReactTypeScript,
            BuildCommand = "npx tsc --noEmit && npm run build",
            Diagnostics = diagnostics
        };
    }

    private async Task<BuildResult> BuildAngularTypeScriptAsync(GitRepositoryConfig repo)
    {
        if(!Directory.Exists(Path.Combine(repo.LocalPath, "node_modules")))
        {
            var installResult = await RunProcessAsync(repo.LocalPath, "npm", "install");
            if(installResult.ExitCode != 0)
            {
                return new BuildResult
                {
                    Success = false,
                    ExitCode = installResult.ExitCode,
                    Output = installResult.Output,
                    Errors = installResult.Errors,
                    ProjectType = ProjectType.AngularTypeScript,
                    BuildCommand = "npm install"
                };
            }
        }

        var result = await RunProcessAsync(repo.LocalPath, "npx", "ng build --configuration production");
        var diagnostics = ParseTypeScriptDiagnostics(result.Output + "\n" + result.Errors);

        return new BuildResult
        {
            Success = result.ExitCode == 0,
            ExitCode = result.ExitCode,
            Output = result.Output,
            Errors = result.Errors,
            ProjectType = ProjectType.AngularTypeScript,
            BuildCommand = "ng build --configuration production",
            Diagnostics = diagnostics
        };
    }

    private async Task<BuildResult> BuildDelphiAsync(GitRepositoryConfig repo, string? projectPath)
    {
        var compilerPath = _config["Delphi:CompilerPath"];
        var rsvarsPath = _config["Delphi:RsvarsPath"];

        if(string.IsNullOrEmpty(compilerPath))
        {
            return new BuildResult
            {
                Success = false,
                ExitCode = -1,
                ProjectType = ProjectType.Delphi,
                Errors = "Delphi compiler not configured. Set Delphi:CompilerPath in appsettings.json. " +
                         "Example: 'C:\\Program Files (x86)\\Embarcadero\\Studio\\23.0\\bin\\dcc64.exe' " +
                         "or use MSBuild with Delphi:RsvarsPath pointing to rsvars.bat."
            };
        }

        var target = projectPath ?? FindDelphiBuildTarget(repo.LocalPath);
        if(target == null)
        {
            return new BuildResult
            {
                Success = false,
                ExitCode = -1,
                ProjectType = ProjectType.Delphi,
                Errors = "No .dproj or .dpr file found in the repository."
            };
        }

        var fullPath = Path.GetFullPath(Path.Combine(repo.LocalPath, target));
        ValidatePath(repo, target);

        ProcessResult result;
        string buildCommand;

        if(target.EndsWith(".dproj", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(rsvarsPath))
        {
            buildCommand = $"msbuild {Path.GetFileName(fullPath)}";
            var batchScript = $"call \"{rsvarsPath}\" && msbuild \"{fullPath}\"";
            result = await RunProcessAsync(Path.GetDirectoryName(fullPath)!, "cmd", $"/c \"{batchScript}\"");
        }
        else
        {
            buildCommand = $"{Path.GetFileName(compilerPath)} {Path.GetFileName(fullPath)}";
            result = await RunProcessAsync(
                Path.GetDirectoryName(fullPath)!,
                compilerPath,
                $"\"{fullPath}\"");
        }

        var diagnostics = ParseDelphiDiagnostics(result.Output + "\n" + result.Errors);

        return new BuildResult
        {
            Success = result.ExitCode == 0,
            ExitCode = result.ExitCode,
            Output = result.Output,
            Errors = result.Errors,
            ProjectType = ProjectType.Delphi,
            BuildCommand = buildCommand,
            Diagnostics = diagnostics
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  Project scaffolding
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Scaffolds a new project in the repository using the appropriate toolchain.
    /// </summary>
    public async Task<BuildResult> ScaffoldProjectAsync(GitRepositoryConfig repo, ProjectScaffoldRequest request)
    {
        var targetDir = string.IsNullOrEmpty(request.SubDirectory)
            ? repo.LocalPath
            : Path.Combine(repo.LocalPath, request.SubDirectory);

        Directory.CreateDirectory(targetDir);

        var scaffoldResult = request.ProjectType switch
        {
            ProjectType.DotNet => await ScaffoldDotNetAsync(targetDir, request),
            ProjectType.ReactTypeScript => await ScaffoldReactAsync(targetDir, request),
            ProjectType.AngularTypeScript => await ScaffoldAngularAsync(targetDir, request),
            ProjectType.Delphi => ScaffoldDelphi(targetDir, request),
            _ => new BuildResult
            {
                Success = false,
                ExitCode = -1,
                ProjectType = request.ProjectType,
                Errors = $"Scaffolding not supported for project type: {request.ProjectType}"
            }
        };

        // Update the repo's detected type if it was unknown
        if(repo.DetectedProjectType == ProjectType.Unknown && scaffoldResult.Success)
        {
            repo.DetectedProjectType = request.ProjectType;
            await _repoStore.SaveAsync(repo);
        }

        return scaffoldResult;
    }

    private static async Task<BuildResult> ScaffoldDotNetAsync(string targetDir, ProjectScaffoldRequest request)
    {
        var template = string.IsNullOrEmpty(request.Template) ? "classlib" : request.Template;
        var name = string.IsNullOrEmpty(request.Name) ? "NewProject" : request.Name;

        var result = await RunProcessAsync(targetDir, "dotnet",
            $"new {template} --name {name} --framework net10.0 --output {name}");

        return new BuildResult
        {
            Success = result.ExitCode == 0,
            ExitCode = result.ExitCode,
            Output = result.Output,
            Errors = result.Errors,
            ProjectType = ProjectType.DotNet,
            BuildCommand = $"dotnet new {template} --name {name}"
        };
    }

    private static async Task<BuildResult> ScaffoldReactAsync(string targetDir, ProjectScaffoldRequest request)
    {
        var name = string.IsNullOrEmpty(request.Name) ? "new-react-app" : request.Name;
        var template = string.IsNullOrEmpty(request.Template) ? "react-ts" : request.Template;

        var result = await RunProcessAsync(targetDir, "npm",
            $"create vite@latest {name} -- --template {template}");

        var projectDir = Path.Combine(targetDir, name);
        if(Directory.Exists(projectDir))
            await RunProcessAsync(projectDir, "npm", "install");

        return new BuildResult
        {
            Success = result.ExitCode == 0,
            ExitCode = result.ExitCode,
            Output = result.Output,
            Errors = result.Errors,
            ProjectType = ProjectType.ReactTypeScript,
            BuildCommand = $"npm create vite@latest {name} -- --template {template}"
        };
    }

    private static async Task<BuildResult> ScaffoldAngularAsync(string targetDir, ProjectScaffoldRequest request)
    {
        var name = string.IsNullOrEmpty(request.Name) ? "new-angular-app" : request.Name;

        var result = await RunProcessAsync(targetDir, "npx",
            $"@angular/cli new {name} --style=scss --routing=true --skip-git");

        return new BuildResult
        {
            Success = result.ExitCode == 0,
            ExitCode = result.ExitCode,
            Output = result.Output,
            Errors = result.Errors,
            ProjectType = ProjectType.AngularTypeScript,
            BuildCommand = $"ng new {name}"
        };
    }

    private static BuildResult ScaffoldDelphi(string targetDir, ProjectScaffoldRequest request)
    {
        var name = string.IsNullOrEmpty(request.Name) ? "NewDelphiProject" : request.Name;
        var projectDir = Path.Combine(targetDir, name);
        Directory.CreateDirectory(projectDir);

        var template = string.IsNullOrEmpty(request.Template) ? "console" : request.Template.ToLowerInvariant();

        switch(template)
        {
            case "console":
                File.WriteAllText(Path.Combine(projectDir, $"{name}.dpr"),
                    $$"""
                    program {{name}};

                    {$APPTYPE CONSOLE}

                    uses
                      System.SysUtils;

                    begin
                      try
                        WriteLn('{{name}} started.');
                      except
                        on E: Exception do
                          WriteLn(E.ClassName, ': ', E.Message);
                      end;
                    end.
                    """);
                break;

            case "vcl":
                File.WriteAllText(Path.Combine(projectDir, $"{name}.dpr"),
                    $$"""
                    program {{name}};

                    uses
                      Vcl.Forms,
                      MainForm in 'MainForm.pas' {frmMain};

                    {$R *.res}

                    begin
                      Application.Initialize;
                      Application.MainFormOnTaskbar := True;
                      Application.CreateForm(TfrmMain, frmMain);
                      Application.Run;
                    end.
                    """);
                File.WriteAllText(Path.Combine(projectDir, "MainForm.pas"),
                    """
                    unit MainForm;

                    interface

                    uses
                      Winapi.Windows, Winapi.Messages, System.SysUtils, System.Variants,
                      System.Classes, Vcl.Graphics, Vcl.Controls, Vcl.Forms, Vcl.Dialogs;

                    type
                      TfrmMain = class(TForm)
                      private
                      public
                      end;

                    var
                      frmMain: TfrmMain;

                    implementation

                    {$R *.dfm}

                    end.
                    """);
                File.WriteAllText(Path.Combine(projectDir, "MainForm.dfm"),
                    """
                    object frmMain: TfrmMain
                      Left = 0
                      Top = 0
                      Caption = 'Main Form'
                      ClientHeight = 480
                      ClientWidth = 640
                      Color = clBtnFace
                      Font.Charset = DEFAULT_CHARSET
                      Font.Color = clWindowText
                      Font.Height = -12
                      Font.Name = 'Segoe UI'
                      Font.Style = []
                      TextHeight = 15
                    end
                    """);
                break;

            default:
                return new BuildResult
                {
                    Success = false,
                    ExitCode = -1,
                    ProjectType = ProjectType.Delphi,
                    Errors = $"Unknown Delphi template: {template}. Supported templates: console, vcl"
                };
        }

        return new BuildResult
        {
            Success = true,
            ExitCode = 0,
            Output = $"Created Delphi {template} project '{name}' at {projectDir}",
            ProjectType = ProjectType.Delphi,
            BuildCommand = $"scaffold delphi {template} {name}"
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  Context building for AI
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Builds a summary of the repo context suitable for injection into an AI system prompt.
    /// </summary>
    public async Task<string> BuildRepoContextAsync(GitRepositoryConfig repo, string branchName)
    {
        await CheckoutBranchAsync(repo, branchName);

        var sb = new StringBuilder();
        sb.AppendLine($"=== Repository: {repo.Name} ({repo.DetectedProjectType}) ===");
        sb.AppendLine($"Branch: {branchName}");
        sb.AppendLine($"Default branch: {repo.DefaultBranch}");
        sb.AppendLine();

        sb.AppendLine(GetProjectTypeInstructions(repo.DetectedProjectType));
        sb.AppendLine();

        sb.AppendLine("--- File Structure ---");
        BuildFileTree(repo.LocalPath, repo.LocalPath, sb, 0, 4);
        sb.AppendLine();

        var keyFiles = FindKeyFiles(repo.LocalPath, repo.DetectedProjectType);
        if(keyFiles.Count > 0)
        {
            sb.AppendLine("--- Key Files ---");
            foreach(var file in keyFiles)
            {
                var relativePath = Path.GetRelativePath(repo.LocalPath, file);
                try
                {
                    var content = await File.ReadAllTextAsync(file);
                    if(content.Length > 10_000)
                        content = content[..10_000] + "\n[... truncated ...]";

                    sb.AppendLine($"\n--- {relativePath} ---");
                    sb.AppendLine(content);
                }
                catch(Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read key file {File}", relativePath);
                }
            }
        }

        return sb.ToString();
    }

    private static string GetProjectTypeInstructions(ProjectType projectType) => projectType switch
    {
        ProjectType.DotNet =>
            """
            This is a .NET project targeting net10.0.
            When modifying code, output files using the format:
            ===FILE:relative/path/to/File.cs===
            <file content>
            ===END_FILE===
            After changes, the system will automatically run 'dotnet build' and report any errors.
            Ensure all code compiles with zero warnings and zero errors.
            You may request a file to be read by saying: READ_FILE:relative/path/to/File.cs
            """,
        ProjectType.ReactTypeScript =>
            """
            This is a React TypeScript project.
            When modifying code, output files using the format:
            ===FILE:relative/path/to/File.tsx===
            <file content>
            ===END_FILE===
            After changes, the system will run 'tsc --noEmit' and 'npm run build' to check for errors.
            Ensure all code passes TypeScript strict checks with no errors.
            You may request a file to be read by saying: READ_FILE:relative/path/to/File.tsx
            """,
        ProjectType.AngularTypeScript =>
            """
            This is an Angular TypeScript project.
            When modifying code, output files using the format:
            ===FILE:relative/path/to/Component.ts===
            <file content>
            ===END_FILE===
            After changes, the system will run 'ng build' to check for errors.
            Ensure all code compiles cleanly.
            You may request a file to be read by saying: READ_FILE:relative/path/to/File.ts
            """,
        ProjectType.Delphi =>
            """
            This is a Delphi/Object Pascal project.
            When modifying code, output files using the format:
            ===FILE:relative/path/to/Unit.pas===
            <file content>
            ===END_FILE===
            After changes, the system will attempt to compile using the configured Delphi compiler.
            Follow standard Delphi coding conventions with uses clause ordering and proper unit structure.
            You may request a file to be read by saying: READ_FILE:relative/path/to/Unit.pas
            """,
        _ =>
            """
            When modifying code, output files using the format:
            ===FILE:relative/path/to/file===
            <file content>
            ===END_FILE===
            You may request a file to be read by saying: READ_FILE:relative/path/to/file
            """
    };

    // ═══════════════════════════════════════════════════════════════
    //  Diagnostic parsers (per project type)
    // ═══════════════════════════════════════════════════════════════

    private static List<BuildDiagnostic> ParseDotNetDiagnostics(string output)
    {
        var diagnostics = new List<BuildDiagnostic>();

        foreach(var line in output.Split('\n'))
        {
            var match = DotNetDiagnosticPattern().Match(line);
            if(match.Success)
            {
                diagnostics.Add(new BuildDiagnostic
                {
                    File = match.Groups[1].Value,
                    Line = int.TryParse(match.Groups[2].Value, out var l) ? l : 0,
                    Column = int.TryParse(match.Groups[3].Value, out var c) ? c : 0,
                    Severity = match.Groups[4].Value,
                    Code = match.Groups[5].Value,
                    Message = match.Groups[6].Value.Trim()
                });
            }
        }

        return diagnostics;
    }

    private static List<BuildDiagnostic> ParseTypeScriptDiagnostics(string output)
    {
        var diagnostics = new List<BuildDiagnostic>();

        foreach(var line in output.Split('\n'))
        {
            var match = TypeScriptDiagnosticPattern().Match(line);
            if(match.Success)
            {
                diagnostics.Add(new BuildDiagnostic
                {
                    File = match.Groups[1].Value,
                    Line = int.TryParse(match.Groups[2].Value, out var l) ? l : 0,
                    Column = int.TryParse(match.Groups[3].Value, out var c) ? c : 0,
                    Severity = match.Groups[4].Value,
                    Code = match.Groups[5].Value,
                    Message = match.Groups[6].Value.Trim()
                });
            }
        }

        return diagnostics;
    }

    private static List<BuildDiagnostic> ParseDelphiDiagnostics(string output)
    {
        var diagnostics = new List<BuildDiagnostic>();

        foreach(var line in output.Split('\n'))
        {
            var match = DelphiDiagnosticPattern().Match(line);
            if(match.Success)
            {
                diagnostics.Add(new BuildDiagnostic
                {
                    File = match.Groups[1].Value,
                    Line = int.TryParse(match.Groups[2].Value, out var l) ? l : 0,
                    Severity = match.Groups[3].Value,
                    Code = match.Groups[4].Value,
                    Message = match.Groups[5].Value.Trim()
                });
            }
        }

        return diagnostics;
    }

    // ═══════════════════════════════════════════════════════════════
    //  Private helpers
    // ═══════════════════════════════════════════════════════════════

    private static string ValidatePath(GitRepositoryConfig repo, string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(repo.LocalPath, relativePath));
        if(!fullPath.StartsWith(repo.LocalPath))
            throw new UnauthorizedAccessException("Path traversal attempt detected.");
        return fullPath;
    }

    private static string BuildAuthenticatedUrl(string url, GitCredentialType credType, string credential)
    {
        if(credType == GitCredentialType.SshKey)
            return url;

        if(credType == GitCredentialType.PersonalAccessToken && !string.IsNullOrEmpty(credential))
        {
            if(url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return url.Insert(8, $"{credential}@");
        }

        return url;
    }

    private async Task<ProcessResult> RunGitAsync(string workDir, string arguments, string? sshKeyPath = null)
    {
        var env = new Dictionary<string, string>();
        if(!string.IsNullOrEmpty(sshKeyPath))
            env["GIT_SSH_COMMAND"] = $"ssh -i {sshKeyPath} -o StrictHostKeyChecking=no";

        return await RunProcessAsync(workDir, "git", arguments, env);
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string workDir,
        string command,
        string arguments,
        Dictionary<string, string>? env = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if(env != null)
        {
            foreach(var (key, value) in env)
                startInfo.EnvironmentVariables[key] = value;
        }

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) => { if(e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if(e.Data != null) errorBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            Output = outputBuilder.ToString(),
            Errors = errorBuilder.ToString()
        };
    }

    private void BuildFileTree(string rootPath, string currentPath, StringBuilder sb, int depth, int maxDepth)
    {
        if(depth >= maxDepth) return;

        var indent = new string(' ', depth * 2);

        try
        {
            foreach(var dir in Directory.GetDirectories(currentPath).OrderBy(d => d))
            {
                var dirName = Path.GetFileName(dir);
                if(dirName.StartsWith('.') ||
                    dirName is "bin" or "obj" or "node_modules" or ".git" or "__history" or "__recovery" or "DCU")
                    continue;

                sb.AppendLine($"{indent}{dirName}/");
                BuildFileTree(rootPath, dir, sb, depth + 1, maxDepth);
            }

            foreach(var file in Directory.GetFiles(currentPath).OrderBy(f => f))
            {
                var fileName = Path.GetFileName(file);
                if(fileName.StartsWith('.') && fileName != ".gitignore") continue;

                var size = new FileInfo(file).Length;
                var sizeStr = size switch
                {
                    < 1024 => $"{size}B",
                    < 1024 * 1024 => $"{size / 1024}KB",
                    _ => $"{size / (1024 * 1024)}MB"
                };
                sb.AppendLine($"{indent}{fileName} ({sizeStr})");
            }
        }
        catch(Exception ex)
        {
            _logger.LogWarning(ex, "Error building file tree at {Path}", currentPath);
        }
    }

    private static List<string> FindKeyFiles(string repoPath, ProjectType projectType)
    {
        var keyPatterns = new List<string> { "README.md", "README.txt" };

        switch(projectType)
        {
            case ProjectType.DotNet:
                keyPatterns.AddRange(["*.sln", "*.csproj", "*.fsproj", "Directory.Build.props", "global.json"]);
                break;
            case ProjectType.ReactTypeScript:
            case ProjectType.AngularTypeScript:
                keyPatterns.AddRange(["package.json", "tsconfig.json", "vite.config.ts", "angular.json"]);
                break;
            case ProjectType.Delphi:
                keyPatterns.AddRange(["*.dpr", "*.dproj", "*.dpk", "*.groupproj"]);
                break;
        }

        var keyFiles = new List<string>();
        foreach(var pattern in keyPatterns)
        {
            keyFiles.AddRange(Directory.GetFiles(repoPath, pattern, SearchOption.AllDirectories)
                .Where(f => !f.Contains("bin") && !f.Contains("obj") &&
                           !f.Contains("node_modules") && !f.Contains(".git"))
                .Take(20));
        }

        return keyFiles.Take(30).ToList();
    }

    private static string? FindDotNetBuildTarget(string repoPath)
    {
        var slnFiles = Directory.GetFiles(repoPath, "*.sln", SearchOption.TopDirectoryOnly);
        if(slnFiles.Length > 0)
            return Path.GetRelativePath(repoPath, slnFiles[0]);

        var csprojFiles = Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains("bin") && !f.Contains("obj"))
            .ToList();

        return csprojFiles.Count > 0 ? Path.GetRelativePath(repoPath, csprojFiles[0]) : null;
    }

    private static string? FindDelphiBuildTarget(string repoPath)
    {
        var dprojFiles = Directory.GetFiles(repoPath, "*.dproj", SearchOption.AllDirectories);
        if(dprojFiles.Length > 0)
            return Path.GetRelativePath(repoPath, dprojFiles[0]);

        var dprFiles = Directory.GetFiles(repoPath, "*.dpr", SearchOption.AllDirectories);
        return dprFiles.Length > 0 ? Path.GetRelativePath(repoPath, dprFiles[0]) : null;
    }

    private static string SanitisePath(string input) =>
        string.Join("_", input.Split(Path.GetInvalidFileNameChars()));

    private static string SanitiseBranchName(string name)
    {
        var sanitised = BranchNameCleanup().Replace(name, "_");
        if(sanitised.Length > 80) sanitised = sanitised[..80];
        return sanitised.Trim('_');
    }

    private static string EscapeGitMessage(string message) =>
        message.Replace("\"", "\\\"").Replace("\n", " ");

    private static void ForceDeleteDirectory(string path)
    {
        foreach(var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(path, recursive: true);
    }

    [GeneratedRegex(@"===FILE:(.+?)===\r?\n([\s\S]*?)===END_FILE===")]
    private static partial Regex FileBlockPattern();

    [GeneratedRegex(@"(.+?)\((\d+),(\d+)\):\s+(error|warning)\s+(\w+):\s+(.+)")]
    private static partial Regex DotNetDiagnosticPattern();

    [GeneratedRegex(@"(.+?)\((\d+),(\d+)\):\s+(error|warning)\s+(TS\d+):\s+(.+)")]
    private static partial Regex TypeScriptDiagnosticPattern();

    [GeneratedRegex(@"(?:\[dcc\d+\s+)?(\w+\.\w+)\((\d+)\)\]?:\s*(Error|Warning|Hint|Fatal)?\s*:?\s*(\w*\d*)\s*(.+)")]
    private static partial Regex DelphiDiagnosticPattern();

    [GeneratedRegex(@"[^a-zA-Z0-9_\-/]")]
    private static partial Regex BranchNameCleanup();

    private sealed class ProcessResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Errors { get; set; } = string.Empty;
    }
}