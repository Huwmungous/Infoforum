using System.Text;
using System.Text.Json;

namespace DocumentationMcpServer;

public static class DocumentationTools
{
    public static Task<object> GenerateReadme(JsonElement args)
    {
        var projectName = args.GetProperty("projectName").GetString()!;
        var description = args.TryGetProperty("description", out var d) ? d.GetString() : "A new project";
        string[] features = args.TryGetProperty("features", out var f) ?
            [.. f.EnumerateArray().Select(x => x.GetString()!)] :
            [];

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
        var methods = args.GetProperty("methods").EnumerateArray().Select(m => new
        {
            Name = m.GetProperty("name").GetString()!,
            Description = m.TryGetProperty("description", out var d) ? d.GetString() : "",
            Parameters = (string[])(m.TryGetProperty("parameters", out var p) ?
                [.. p.EnumerateArray().Select(x => x.GetString()!)] :
                []),
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
        string[] issues = args.TryGetProperty("issues", out var i) ?
            [.. i.EnumerateArray().Select(x => x.GetString()!)] :
            [];

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

        return Task.FromResult<object>(new
        {
            success = true,
            projectName,
            filesConverted,
            filesTotal,
            percentage = $"{percentage:F1}%",
            content = sb.ToString()
        });
    }

    public static Task<object> GenerateChangelog(JsonElement args)
    {
        var version = args.GetProperty("version").GetString()!;
        var changes = args.GetProperty("changes").EnumerateArray().Select(c => new
        {
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

        return Task.FromResult<object>(new
        {
            success = true,
            version,
            changeCount = changes.Length,
            content = sb.ToString()
        });
    }

    public static Task<object> GenerateXmlComments(JsonElement args)
    {
        var methodName = args.GetProperty("methodName").GetString()!;
        var summary = args.TryGetProperty("summary", out var s) ? s.GetString() : $"Description for {methodName}";
        string[] parameters = args.TryGetProperty("parameters", out var p) ?
            [.. p.EnumerateArray().Select(x => x.GetString()!)] :
            [];
        var returnDescription = args.TryGetProperty("returnDescription", out var r) ?
            r.GetString() : "Result of the operation";

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
        var classes = args.GetProperty("classes").EnumerateArray().Select(c => new
        {
            Name = c.GetProperty("name").GetString()!,
            Properties = (string[])(c.TryGetProperty("properties", out var p) ?
                [.. p.EnumerateArray().Select(x => x.GetString()!)] :
                []),
            Methods = (string[])(c.TryGetProperty("methods", out var m) ?
                [.. m.EnumerateArray().Select(x => x.GetString()!)] :
                [])
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

    public static Task<object> GenerateSwaggerDocumentation(JsonElement args)
    {
        var endpointName = args.GetProperty("endpointName").GetString()!;
        var httpMethod = args.TryGetProperty("httpMethod", out var hm) ? hm.GetString()! : "Get";
        var route = args.TryGetProperty("route", out var rt) ? rt.GetString() : "";
        var summary = args.TryGetProperty("summary", out var sm) ? sm.GetString()! : $"{endpointName} operation";
        var description = args.TryGetProperty("description", out var desc) ? desc.GetString() : summary;
        string[] tags = args.TryGetProperty("tags", out var t) ?
            [.. t.EnumerateArray().Select(x => x.GetString()!)] :
            ["API"];
        var apiStyle = args.TryGetProperty("apiStyle", out var style) && style.GetString() is string s ? s : "minimal";

        var parameters = args.TryGetProperty("parameters", out var prms) ?
            prms.EnumerateArray().Select(pm => new
            {
                Name = pm.GetProperty("name").GetString()!,
                Type = pm.GetProperty("type").GetString()!,
                Description = pm.TryGetProperty("description", out var d) ? d.GetString() : "",
                Required = pm.TryGetProperty("required", out var req) && req.GetBoolean(),
                Location = pm.TryGetProperty("location", out var loc) ? loc.GetString()! : "query"
            }).ToArray() :
            [];

        var responses = args.TryGetProperty("responses", out var resp) ?
            resp.EnumerateArray().Select(r => new
            {
                StatusCode = r.GetProperty("statusCode").GetInt32(),
                Description = r.TryGetProperty("description", out var d) ? d.GetString() : (string?)null,
                ReturnType = r.TryGetProperty("returnType", out var rt) ? rt.GetString() : (string?)null
            }).ToArray() :
            [new { StatusCode = 200, Description = (string?)"Success", ReturnType = (string?)null }];

        var sb = new StringBuilder();

        if (apiStyle.Equals("controller", StringComparison.OrdinalIgnoreCase))
        {
            // Controller-based API with attributes
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// {summary}");
            sb.AppendLine("/// </summary>");

            foreach (var param in parameters.Where(p => p.Location != "body"))
            {
                sb.AppendLine($"/// <param name=\"{param.Name}\">{param.Description}</param>");
            }

            sb.AppendLine($"[Http{httpMethod}(\"{route}\")]");

            foreach (var response in responses)
            {
                if (!string.IsNullOrEmpty(response.ReturnType))
                {
                    sb.AppendLine($"[ProducesResponseType(typeof({response.ReturnType}), StatusCodes.Status{response.StatusCode}{GetStatusCodeName(response.StatusCode)})]");
                }
                else
                {
                    sb.AppendLine($"[ProducesResponseType(StatusCodes.Status{response.StatusCode}{GetStatusCodeName(response.StatusCode)})]");
                }
            }

            sb.AppendLine($"[SwaggerOperation(");
            sb.AppendLine($"    Summary = \"{summary}\",");
            sb.AppendLine($"    Description = \"{description}\",");
            sb.AppendLine($"    Tags = new[] {{ {string.Join(", ", tags.Select(tag => $"\"{tag}\""))} }}");
            sb.AppendLine(")]");
        }
        else
        {
            // Minimal API fluent style
            sb.AppendLine($".WithName(\"{endpointName}\")");
            sb.AppendLine($".WithSummary(\"{summary}\")");
            sb.AppendLine($".WithDescription(\"{description}\")");
            sb.AppendLine($".WithTags({string.Join(", ", tags.Select(tag => $"\"{tag}\""))})");

            var bodyParam = parameters.FirstOrDefault(p => p.Location == "body");
            if (bodyParam != null)
            {
                sb.AppendLine($".Accepts<{bodyParam.Type}>(\"application/json\")");
            }

            foreach (var response in responses)
            {
                if (!string.IsNullOrEmpty(response.ReturnType))
                {
                    sb.AppendLine($".Produces<{response.ReturnType}>({response.StatusCode})");
                }
                else
                {
                    sb.AppendLine($".Produces({response.StatusCode})");
                }
            }

            if (parameters.Length > 0)
            {
                sb.AppendLine($".WithOpenApi(operation => ");
                sb.AppendLine("{");
                foreach (var param in parameters.Where(p => p.Location != "body"))
                {
                    sb.AppendLine($"    operation.Parameters.Add(new OpenApiParameter");
                    sb.AppendLine("    {");
                    sb.AppendLine($"        Name = \"{param.Name}\",");
                    sb.AppendLine($"        In = ParameterLocation.{char.ToUpper(param.Location[0]) + param.Location[1..]},");
                    sb.AppendLine($"        Required = {param.Required.ToString().ToLower()},");
                    sb.AppendLine($"        Description = \"{param.Description}\",");
                    sb.AppendLine($"        Schema = new OpenApiSchema {{ Type = \"{GetOpenApiType(param.Type)}\" }}");
                    sb.AppendLine("    });");
                }
                sb.AppendLine("    return operation;");
                sb.AppendLine("})");
            }
        }

        return Task.FromResult<object>(new
        {
            success = true,
            endpointName,
            apiStyle,
            content = sb.ToString()
        });
    }

    private static string GetStatusCodeName(int statusCode)
    {
        return statusCode switch
        {
            200 => "OK",
            201 => "Created",
            204 => "NoContent",
            400 => "BadRequest",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "NotFound",
            409 => "Conflict",
            500 => "InternalServerError",
            _ => ""
        };
    }

    private static string GetOpenApiType(string csharpType)
    {
        return csharpType.ToLower() switch
        {
            "int" or "int32" or "long" or "int64" => "integer",
            "string" => "string",
            "bool" or "boolean" => "boolean",
            "decimal" or "double" or "float" => "number",
            "datetime" or "datetimeoffset" => "string",
            "guid" => "string",
            _ => "object"
        };
    }
}