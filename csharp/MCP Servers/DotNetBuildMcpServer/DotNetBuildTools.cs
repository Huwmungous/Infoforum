using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace DotNetBuildMcpServer;

public static class DotNetBuildTools
{
    public static async Task<object> DotNetBuild(JsonElement args)
    {
        var projectPath = args.GetProperty("projectPath").GetString()!;
        var configuration = args.TryGetProperty("configuration", out var conf) ? conf.GetString() : "Debug";

        if (!File.Exists(projectPath))
            throw new FileNotFoundException($"Project file not found: {projectPath}");

        var result = await ExecuteDotNetCommand("build", projectPath, ["--configuration", configuration!]);

        return new
        {
            success = result.ExitCode == 0,
            exitCode = result.ExitCode,
            output = result.Output,
            errors = result.Errors,
            projectPath,
            configuration
        };
    }

    public static async Task<object> DotNetClean(JsonElement args)
    {
        var projectPath = args.GetProperty("projectPath").GetString()!;

        if (!File.Exists(projectPath))
            throw new FileNotFoundException($"Project file not found: {projectPath}");

        var result = await ExecuteDotNetCommand("clean", projectPath, []);

        return new
        {
            success = result.ExitCode == 0,
            exitCode = result.ExitCode,
            output = result.Output,
            projectPath
        };
    }

    public static async Task<object> DotNetRestore(JsonElement args)
    {
        var projectPath = args.GetProperty("projectPath").GetString()!;

        if (!File.Exists(projectPath))
            throw new FileNotFoundException($"Project file not found: {projectPath}");

        var result = await ExecuteDotNetCommand("restore", projectPath, []);

        return new
        {
            success = result.ExitCode == 0,
            exitCode = result.ExitCode,
            output = result.Output,
            projectPath
        };
    }

    public static async Task<object> DotNetTest(JsonElement args)
    {
        var projectPath = args.GetProperty("projectPath").GetString()!;
        var filter = args.TryGetProperty("filter", out var flt) ? flt.GetString() : null;

        if (!File.Exists(projectPath))
            throw new FileNotFoundException($"Project file not found: {projectPath}");

        string[] extraArgs = filter != null ? ["--filter", filter] : [];
        var result = await ExecuteDotNetCommand("test", projectPath, extraArgs);

        return new
        {
            success = result.ExitCode == 0,
            exitCode = result.ExitCode,
            output = result.Output,
            errors = result.Errors,
            projectPath,
            filter
        };
    }

    public static async Task<object> DotNetPublish(JsonElement args)
    {
        var projectPath = args.GetProperty("projectPath").GetString()!;
        var configuration = args.TryGetProperty("configuration", out var conf) ? conf.GetString() : "Release";
        var runtime = args.TryGetProperty("runtime", out var rt) ? rt.GetString() : null;

        if (!File.Exists(projectPath))
            throw new FileNotFoundException($"Project file not found: {projectPath}");

        var extraArgs = new List<string> { "--configuration", configuration! };
        if (runtime != null)
        {
            extraArgs.Add("--runtime");
            extraArgs.Add(runtime);
        }

        var result = await ExecuteDotNetCommand("publish", projectPath, [.. extraArgs]);

        return new
        {
            success = result.ExitCode == 0,
            exitCode = result.ExitCode,
            output = result.Output,
            errors = result.Errors,
            projectPath,
            configuration,
            runtime
        };
    }

    public static async Task<object> GetBuildErrors(JsonElement args)
    {
        var projectPath = args.GetProperty("projectPath").GetString()!;

        if (!File.Exists(projectPath))
            throw new FileNotFoundException($"Project file not found: {projectPath}");

        var result = await ExecuteDotNetCommand("build", projectPath, ["--no-restore"]);

        var errors = ParseBuildOutput(result.Output + result.Errors, "error");

        return new
        {
            success = result.ExitCode == 0,
            errorCount = errors.Count,
            errors,
            projectPath
        };
    }

    public static async Task<object> GetBuildWarnings(JsonElement args)
    {
        var projectPath = args.GetProperty("projectPath").GetString()!;

        if (!File.Exists(projectPath))
            throw new FileNotFoundException($"Project file not found: {projectPath}");

        var result = await ExecuteDotNetCommand("build", projectPath, ["--no-restore"]);

        var warnings = ParseBuildOutput(result.Output + result.Errors, "warning");

        return new
        {
            success = result.ExitCode == 0,
            warningCount = warnings.Count,
            warnings,
            projectPath
        };
    }

    public static async Task<object> DotNetAddPackage(JsonElement args)
    {
        var projectPath = args.GetProperty("projectPath").GetString()!;
        var packageName = args.GetProperty("packageName").GetString()!;
        var version = args.TryGetProperty("version", out var ver) ? ver.GetString() : null;

        if (!File.Exists(projectPath))
            throw new FileNotFoundException($"Project file not found: {projectPath}");

        var extraArgs = new List<string> { "package", packageName };
        if (version != null)
        {
            extraArgs.Add("--version");
            extraArgs.Add(version);
        }

        var result = await ExecuteDotNetCommand("add", projectPath, [.. extraArgs]);

        return new
        {
            success = result.ExitCode == 0,
            exitCode = result.ExitCode,
            output = result.Output,
            projectPath,
            packageName,
            version
        };
    }

    public static async Task<object> DotNetRemovePackage(JsonElement args)
    {
        var projectPath = args.GetProperty("projectPath").GetString()!;
        var packageName = args.GetProperty("packageName").GetString()!;

        if (!File.Exists(projectPath))
            throw new FileNotFoundException($"Project file not found: {projectPath}");

        var result = await ExecuteDotNetCommand("remove", projectPath, ["package", packageName]);

        return new
        {
            success = result.ExitCode == 0,
            exitCode = result.ExitCode,
            output = result.Output,
            projectPath,
            packageName
        };
    }

    public static async Task<object> DotNetListPackages(JsonElement args)
    {
        var projectPath = args.GetProperty("projectPath").GetString()!;

        if (!File.Exists(projectPath))
            throw new FileNotFoundException($"Project file not found: {projectPath}");

        var result = await ExecuteDotNetCommand("list", projectPath, ["package"]);

        return new
        {
            success = result.ExitCode == 0,
            output = result.Output,
            projectPath
        };
    }

    public static Task<object> AnalyzeCodeSyntax(JsonElement args)
    {
        var filePath = args.GetProperty("filePath").GetString()!;

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var code = File.ReadAllText(filePath);
        var tree = CSharpSyntaxTree.ParseText(code);
        var diagnostics = tree.GetDiagnostics();

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => new
        {
            message = d.GetMessage(),
            location = d.Location.GetLineSpan().StartLinePosition.Line + 1,
            severity = "Error"
        }).ToList();

        var warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).Select(d => new
        {
            message = d.GetMessage(),
            location = d.Location.GetLineSpan().StartLinePosition.Line + 1,
            severity = "Warning"
        }).ToList();

        return Task.FromResult<object>(new
        {
            success = errors.Count == 0,
            filePath,
            errorCount = errors.Count,
            warningCount = warnings.Count,
            errors,
            warnings
        });
    }

    public static Task<object> ValidateCSharpCode(JsonElement args)
    {
        var code = args.GetProperty("code").GetString()!;

        var tree = CSharpSyntaxTree.ParseText(code);
        var diagnostics = tree.GetDiagnostics();

        var hasErrors = diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

        return Task.FromResult<object>(new
        {
            isValid = !hasErrors,
            diagnostics = diagnostics.Select(d => new
            {
                message = d.GetMessage(),
                severity = d.Severity.ToString(),
                line = d.Location.GetLineSpan().StartLinePosition.Line + 1
            }).ToList()
        });
    }

    private static async Task<CommandResult> ExecuteDotNetCommand(string command, string projectPath, string[] extraArgs)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add(command);
        startInfo.ArgumentList.Add(Path.GetFileName(projectPath));
        foreach (var arg in extraArgs)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                outputBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return new CommandResult
        {
            ExitCode = process.ExitCode,
            Output = outputBuilder.ToString(),
            Errors = errorBuilder.ToString()
        };
    }

    private static List<object> ParseBuildOutput(string output, string messageType)
    {
        var results = new List<object>();
        var lines = output.Split('\n');

        foreach (var line in lines)
        {
            if (line.Contains($": {messageType} ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split([$": {messageType} "], StringSplitOptions.None);
                if (parts.Length >= 2)
                {
                    var locationPart = parts[0];
                    var messagePart = parts[1];

                    results.Add(new
                    {
                        type = messageType,
                        message = messagePart.Trim(),
                        location = locationPart.Trim()
                    });
                }
            }
        }

        return results;
    }

    private sealed class CommandResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; } = string.Empty;
        public string Errors { get; set; } = string.Empty;
    }
}