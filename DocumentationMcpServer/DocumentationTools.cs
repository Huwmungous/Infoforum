using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DocumentationMcpServer;

public static class DocumentationTools
{
    public static Task<object> GenerateReadme(JsonElement args)
    {
        var projectName = args.GetProperty("projectName").GetString()!;
        var description = args.TryGetProperty("description", out var d) ? d.GetString() : "A new project";
        var features = args.TryGetProperty("features", out var f) ? f.EnumerateArray().Select(x => x.GetString()!).ToArray() : Array.Empty<string>();
        
        var sb = new StringBuilder();
        sb.AppendLine($"# {projectName}");
        sb.AppendLine();
        sb.AppendLine(description);
        sb.AppendLine();
        
        if (features.Length > 0)
        {
            sb.AppendLine("## Features");
            sb.AppendLine();
            foreach (var feature in features)
                sb.AppendLine($"- {feature}");
            sb.AppendLine();
        }
        
        sb.AppendLine("## Installation");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("dotnet build");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Usage");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine("dotnet run");
        sb.AppendLine("```");
        
        return Task.FromResult<object>(new { success = true, projectName, content = sb.ToString() });
    }

    public static Task<object> GenerateApiDocs(JsonElement args)
    {
        var className = args.GetProperty("className").GetString()!;
        var methods = args.GetProperty("methods").EnumerateArray().Select(m => new {
            Name = m.GetProperty("name").GetString()!,
            Description = m.TryGetProperty("description", out var d) ? d.GetString() : "",
            Parameters = m.TryGetProperty("parameters", out var p) ? p.EnumerateArray().Select(x => x.GetString()!).ToArray() : Array.Empty<string>(),
            ReturnType = m.TryGetProperty("returnType", out var r) ? r.GetString() : "void"
        }).ToArray();
        
        var sb = new StringBuilder();
        sb.AppendLine($"# {className} API Documentation");
        sb.AppendLine();
        
        foreach (var method in methods)
        {
            sb.AppendLine($"## {method.Name}");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(method.Description))
            {
                sb.AppendLine(method.Description);
                sb.AppendLine();
            }
            sb.AppendLine("**Parameters:**");
            if (method.Parameters.Length > 0)
            {
                foreach (var param in method.Parameters)
                    sb.AppendLine($"- `{param}`");
            }
            else
            {
                sb.AppendLine("- None");
            }
            sb.AppendLine();
            sb.AppendLine($"**Returns:** `{method.ReturnType}`");
            sb.AppendLine();
        }
        
        return Task.FromResult<object>(new { success = true, className, content = sb.ToString() });
    }

    public static Task<object> CreateMigrationReport(JsonElement args)
    {
        var projectName = args.GetProperty("projectName").GetString()!;
        var filesConverted = args.TryGetProperty("filesConverted", out var fc) ? fc.GetInt32() : 0;
        var filesTotal = args.TryGetProperty("filesTotal", out var ft) ? ft.GetInt32() : 0;
        var issues = args.TryGetProperty("issues", out var i) ? i.EnumerateArray().Select(x => x.GetString()!).ToArray() : Array.Empty<string>();
        
        var percentage = filesTotal > 0 ? (filesConverted * 100.0 / filesTotal) : 0;
        
        var sb = new StringBuilder();
        sb.AppendLine($"# Migration Report: {projectName}");
        sb.AppendLine();
        sb.AppendLine($"**Date:** {DateTime.Now:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine("## Progress");
        sb.AppendLine();
        sb.AppendLine($"- Files Converted: {filesConverted}/{filesTotal} ({percentage:F1}%)");
        sb.AppendLine();
        
        if (issues.Length > 0)
        {
            sb.AppendLine("## Issues Found");
            sb.AppendLine();
            foreach (var issue in issues)
                sb.AppendLine($"- {issue}");
            sb.AppendLine();
        }
        
        sb.AppendLine("## Next Steps");
        sb.AppendLine();
        sb.AppendLine("1. Review converted files");
        sb.AppendLine("2. Run tests");
        sb.AppendLine("3. Address outstanding issues");
        
        return Task.FromResult<object>(new { success = true, projectName, filesConverted, filesTotal, percentage = $"{percentage:F1}%", content = sb.ToString() });
    }

    public static Task<object> GenerateChangelog(JsonElement args)
    {
        var version = args.GetProperty("version").GetString()!;
        var changes = args.GetProperty("changes").EnumerateArray().Select(c => new {
            Type = c.GetProperty("type").GetString()!,
            Description = c.GetProperty("description").GetString()!
        }).ToArray();
        
        var sb = new StringBuilder();
        sb.AppendLine("# Changelog");
        sb.AppendLine();
        sb.AppendLine($"## [{version}] - {DateTime.Now:yyyy-MM-dd}");
        sb.AppendLine();
        
        var grouped = changes.GroupBy(c => c.Type);
        foreach (var group in grouped.OrderBy(g => g.Key))
        {
            sb.AppendLine($"### {group.Key}");
            foreach (var change in group)
                sb.AppendLine($"- {change.Description}");
            sb.AppendLine();
        }
        
        return Task.FromResult<object>(new { success = true, version, changeCount = changes.Length, content = sb.ToString() });
    }

    public static Task<object> GenerateXmlComments(JsonElement args)
    {
        var methodName = args.GetProperty("methodName").GetString()!;
        var summary = args.TryGetProperty("summary", out var s) ? s.GetString() : $"Description for {methodName}";
        var parameters = args.TryGetProperty("parameters", out var p) ? p.EnumerateArray().Select(x => x.GetString()!).ToArray() : Array.Empty<string>();
        var returnDescription = args.TryGetProperty("returnDescription", out var r) ? r.GetString() : "Result of the operation";
        
        var sb = new StringBuilder();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {summary}");
        sb.AppendLine("/// </summary>");
        
        foreach (var param in parameters)
        {
            sb.AppendLine($"/// <param name=\"{param}\">The {param} parameter</param>");
        }
        
        sb.AppendLine($"/// <returns>{returnDescription}</returns>");
        
        return Task.FromResult<object>(new { success = true, methodName, content = sb.ToString() });
    }

    public static Task<object> GenerateClassDiagram(JsonElement args)
    {
        var classes = args.GetProperty("classes").EnumerateArray().Select(c => new {
            Name = c.GetProperty("name").GetString()!,
            Properties = c.TryGetProperty("properties", out var p) ? p.EnumerateArray().Select(x => x.GetString()!).ToArray() : Array.Empty<string>(),
            Methods = c.TryGetProperty("methods", out var m) ? m.EnumerateArray().Select(x => x.GetString()!).ToArray() : Array.Empty<string>()
        }).ToArray();
        
        var sb = new StringBuilder();
        sb.AppendLine("```mermaid");
        sb.AppendLine("classDiagram");
        
        foreach (var cls in classes)
        {
            sb.AppendLine($"    class {cls.Name} {{");
            foreach (var prop in cls.Properties)
                sb.AppendLine($"        +{prop}");
            foreach (var method in cls.Methods)
                sb.AppendLine($"        +{method}()");
            sb.AppendLine("    }");
        }
        
        sb.AppendLine("```");
        
        return Task.FromResult<object>(new { success = true, classCount = classes.Length, content = sb.ToString() });
    }
}
