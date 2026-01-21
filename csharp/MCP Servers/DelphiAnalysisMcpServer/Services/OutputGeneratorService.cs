using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using DelphiAnalysisMcpServer.Models;

namespace DelphiAnalysisMcpServer.Services;

/// <summary>
/// Service for generating output in various formats (folder, zip, scripts).
/// </summary>
public partial class OutputGeneratorService(ILogger<OutputGeneratorService> logger)
{
    private readonly ILogger<OutputGeneratorService> _logger = logger;

    #region Generated Regex Patterns

    [GeneratedRegex(@":\s*BackgroundService\b")]
    private static partial Regex BackgroundServiceRegex();

    [GeneratedRegex(@":\s*IHostedService\b")]
    private static partial Regex HostedServiceRegex();

    [GeneratedRegex(@"public\s+(?:partial\s+)?class\s+(\w+)")]
    private static partial Regex ClassNameRegex();

    [GeneratedRegex(@"private\s+readonly\s+HttpClient\s+_")]
    private static partial Regex HttpClientFieldRegex();

    [GeneratedRegex(@"public\s+async\s+Task<[^>]+>\s+(\w+Async)\s*\(")]
    private static partial Regex AsyncMethodRegex();

    [GeneratedRegex(@":\s*ServiceBase\b")]
    private static partial Regex ServiceBaseInheritanceRegex();

    #endregion

    #region LoggerMessage Definitions

    [LoggerMessage(Level = LogLevel.Warning, Message = "Detected degenerate AI output in {SourceFile}, skipping dependency analysis")]
    private partial void LogDegenerateOutput(string sourceFile);

    [LoggerMessage(Level = LogLevel.Information, Message = "Detected hosted service: {ClassName}")]
    private partial void LogHostedServiceDetected(string? className);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Generated code uses SqlClient instead of Firebird in {SourceFile}")]
    private partial void LogSqlClientWarning(string sourceFile);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Generated code has {LineCount} lines, may be degenerate")]
    private partial void LogLargeFileWarning(int lineCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Detected {PackageCount} additional NuGet packages, {ServiceCount} hosted services")]
    private partial void LogDependenciesDetected(int packageCount, int serviceCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Repository placeholder: {RepoPath}")]
    private partial void LogRepositoryPlaceholder(string repoPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Controller placeholder: {ControllerPath}")]
    private partial void LogControllerPlaceholder(string controllerPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated: {FilePath}")]
    private partial void LogFileGenerated(string filePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "React component placeholder: {ComponentPath}")]
    private partial void LogReactPlaceholder(string componentPath);

    #endregion

    /// <summary>
    /// Tracks detected dependencies from generated code analysis.
    /// </summary>
    private class DetectedDependencies
    {
        public HashSet<string> NuGetPackages { get; } = [];
        public HashSet<string> HostedServices { get; } = [];
        public HashSet<string> HttpClientServices { get; } = [];
        public HashSet<string> AdditionalUsings { get; } = [];
        public bool UsesSmtp { get; set; }
        public bool UsesHttpClient { get; set; }

        // Track which namespaces actually have content
        public bool HasDtos { get; set; }
        public bool HasRepositories { get; set; }
        public bool HasControllers { get; set; }

        // Track code issues needing compatibility shims
        public bool NeedsGatewayShim { get; set; }
        public bool UsesServiceBase { get; set; }
    }

    /// <summary>
    /// Analyzes generated code to detect required dependencies.
    /// </summary>
    private DetectedDependencies AnalyzeGeneratedCode(ProjectTranslationSummary summary)
    {
        var deps = new DetectedDependencies();

        foreach (var result in summary.Results.Where(r => r.Success && r.TranslatedCode is not null))
        {
            var code = result.TranslatedCode!;

            // Check for degenerate output (AI loops, garbage tokens)
            if (IsDegenerateOutput(code))
            {
                LogDegenerateOutput(result.SourceFile);
                continue;
            }

            var className = ExtractClassName(code);

            // Detect BackgroundService / IHostedService
            if (BackgroundServiceRegex().IsMatch(code) || HostedServiceRegex().IsMatch(code))
            {
                deps.NuGetPackages.Add("Microsoft.Extensions.Hosting");
                if (!string.IsNullOrEmpty(className))
                {
                    deps.HostedServices.Add(className);
                }
                LogHostedServiceDetected(className);
            }

            // Detect SqlClient usage (wrong for Firebird projects, but add package if used)
            if (code.Contains("System.Data.SqlClient") || code.Contains("SqlConnection") || code.Contains("SqlCommand"))
            {
                deps.NuGetPackages.Add("Microsoft.Data.SqlClient");
                LogSqlClientWarning(result.SourceFile);
            }

            // Detect HttpClient usage
            if (code.Contains("HttpClient") || code.Contains("IHttpClientFactory"))
            {
                deps.UsesHttpClient = true;
                deps.NuGetPackages.Add("Microsoft.Extensions.Http");

                // If class uses HttpClient, register it for typed client
                if (!string.IsNullOrEmpty(className) && HttpClientFieldRegex().IsMatch(code))
                {
                    deps.HttpClientServices.Add(className);
                }
            }

            // Detect SMTP/Email usage
            if (code.Contains("SmtpClient") || code.Contains("MailMessage") ||
                code.Contains("System.Net.Mail"))
            {
                deps.UsesSmtp = true;
                // MailKit is the modern replacement for System.Net.Mail
                deps.NuGetPackages.Add("MailKit");
            }

            // Detect SignalR
            if (code.Contains("HubConnection") || code.Contains(": Hub"))
            {
                deps.NuGetPackages.Add("Microsoft.AspNetCore.SignalR.Client");
            }

            // Detect Newtonsoft.Json (legacy, but AI might generate it)
            if (code.Contains("Newtonsoft.Json") || code.Contains("JsonConvert"))
            {
                deps.NuGetPackages.Add("Newtonsoft.Json");
            }

            // Detect AutoMapper
            if (code.Contains("IMapper") || code.Contains("AutoMapper"))
            {
                deps.NuGetPackages.Add("AutoMapper");
                deps.NuGetPackages.Add("AutoMapper.Extensions.Microsoft.DependencyInjection");
            }

            // Detect FluentValidation
            if (code.Contains("AbstractValidator") || code.Contains("FluentValidation"))
            {
                deps.NuGetPackages.Add("FluentValidation");
                deps.NuGetPackages.Add("FluentValidation.DependencyInjectionExtensions");
            }

            // Detect MediatR
            if (code.Contains("IMediator") || code.Contains("IRequest<"))
            {
                deps.NuGetPackages.Add("MediatR");
            }

            // Detect Serilog
            if (code.Contains("Serilog"))
            {
                deps.NuGetPackages.Add("Serilog.AspNetCore");
            }

            // Detect Polly (resilience)
            if (code.Contains("Policy.") || code.Contains("Polly"))
            {
                deps.NuGetPackages.Add("Polly");
                deps.NuGetPackages.Add("Microsoft.Extensions.Http.Polly");
            }

            // Detect caching
            if (code.Contains("IDistributedCache") || code.Contains("IMemoryCache"))
            {
                deps.NuGetPackages.Add("Microsoft.Extensions.Caching.Memory");
            }

            // Detect Timer-based services (common in converted Delphi services)
            if (code.Contains("PeriodicTimer") || code.Contains("System.Timers.Timer"))
            {
                deps.NuGetPackages.Add("Microsoft.Extensions.Hosting");
            }

            // Detect Gateway usage (Delphi global/singleton pattern needing shim)
            if (code.Contains("Gateway.") || code.Contains("Gateway.RootPath") || code.Contains("Gateway.GetGUID"))
            {
                deps.NeedsGatewayShim = true;
            }

            // Detect ServiceBase usage (will be converted to console application)
            if (code.Contains("ServiceBase") || code.Contains("System.ServiceProcess"))
            {
                deps.UsesServiceBase = true;
                // No special packages needed - converted to simple console app
            }
        }

        return deps;
    }

    /// <summary>
    /// Detects degenerate AI output (loops, garbage tokens, excessive repetition).
    /// </summary>
    private bool IsDegenerateOutput(string code)
    {
        // Check for garbage tokens from model breakdown
        if (code.Contains("<|im_start|>") || code.Contains("<|im_end|>") ||
            code.Contains("<|endoftext|>") || code.Contains('⏎'))
        {
            return true;
        }

        // Check for AI conversation markers
        if (code.Contains("Could you please clarify") ||
            code.Contains("It seems like you") ||
            code.Contains("Let me know if") ||
            code.Contains("I hope this helps") ||
            code.Contains("Feel free to ask") ||
            code.Contains("Here's an example") ||
            code.Contains("### 1.") ||
            code.Contains("### 2.") ||
            code.Contains("**Sign up") ||
            code.Contains("- **"))
        {
            return true;
        }

        // Check for markdown code blocks embedded in code (not at start)
        var codeBlockIndex = code.IndexOf("```csharp", 100, StringComparison.Ordinal);
        if (codeBlockIndex > 0)
        {
            return true;
        }

        // Check for excessive method repetition (same signature appearing many times)
        var methodMatches = AsyncMethodRegex().Matches(code);
        if (methodMatches.Count > 50)
        {
            // Count unique method names
            var uniqueNames = methodMatches.Cast<Match>().Select(m => m.Groups[1].Value).Distinct().Count();
            // If less than 10% are unique, it's likely degenerate
            if (uniqueNames < methodMatches.Count * 0.1)
            {
                return true;
            }
        }

        // Check for excessive line count (likely runaway generation)
        var lineCount = code.Split('\n').Length;
        if (lineCount > 2000)
        {
            LogLargeFileWarning(lineCount);
            // Don't auto-reject, just warn - some large files are legitimate
        }

        return false;
    }

    /// <summary>
    /// Extracts the primary class name from C# code.
    /// </summary>
    private static string? ExtractClassName(string code)
    {
        var match = ClassNameRegex().Match(code);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Generates output based on the configured options.
    /// </summary>
    public async Task<OutputResult> GenerateOutputAsync(
        DelphiProject project,
        ProjectTranslationSummary summary,
        TranslationOptions translationOptions,
        OutputOptions outputOptions,
        ApiSpecification? apiSpec = null,
        List<ReactComponentDefinition>? reactComponents = null)
    {
        var result = new OutputResult { ProjectName = project.Name };

        // Ensure output directory exists
        var outputPath = Path.GetFullPath(outputOptions.OutputPath);
        Directory.CreateDirectory(outputPath);

        // Generate the C# API project structure
        var apiProjectFolder = Path.Combine(outputPath, $"{project.Name}.Api");
        await GenerateApiProjectStructureAsync(project, summary, translationOptions, apiProjectFolder, apiSpec);
        result.FolderPath = apiProjectFolder;

        // Generate React project if UI target is React
        if (translationOptions.UITarget == UITargetFramework.React)
        {
            var reactProjectFolder = Path.Combine(outputPath, $"{project.Name}.Web");
            await GenerateReactProjectStructureAsync(project, translationOptions, reactProjectFolder, reactComponents, apiSpec);
            result.ReactProjectPath = reactProjectFolder;
        }

        // Generate solution file if requested
        if (outputOptions.GenerateSolution)
        {
            await GenerateSolutionFileAsync(project.Name, outputPath, translationOptions.UITarget == UITargetFramework.React);
        }

        // Generate zip if requested
        if (outputOptions.Format == OutputFormat.Zip)
        {
            var zipPath = Path.Combine(outputPath, $"{project.Name}.zip");
            await CreateZipAsync(outputPath, zipPath, project.Name);
            result.ZipPath = zipPath;
        }

        // Generate deployment scripts if requested
        if (outputOptions.GenerateDeploymentScripts)
        {
            var scriptsFolder = Path.Combine(outputPath, "scripts");
            Directory.CreateDirectory(scriptsFolder);

            if (outputOptions.ScriptFormat is ScriptFormat.PowerShell or ScriptFormat.Both)
            {
                var psPath = Path.Combine(scriptsFolder, $"deploy-{project.Name}.ps1");
                await GeneratePowerShellScriptAsync(project.Name, translationOptions, psPath);
                result.PowerShellScriptPath = psPath;
            }

            if (outputOptions.ScriptFormat is ScriptFormat.Bash or ScriptFormat.Both)
            {
                var bashPath = Path.Combine(scriptsFolder, $"deploy-{project.Name}.sh");
                await GenerateBashScriptAsync(project.Name, translationOptions, bashPath);
                result.BashScriptPath = bashPath;
            }
        }

        return result;
    }

    private async Task GenerateApiProjectStructureAsync(
        DelphiProject project,
        ProjectTranslationSummary summary,
        TranslationOptions options,
        string projectFolder,
        ApiSpecification? apiSpec)
    {
        Directory.CreateDirectory(projectFolder);

        // Analyze generated code for dependencies BEFORE generating project files
        var detectedDeps = AnalyzeGeneratedCode(summary);
        LogDependenciesDetected(detectedDeps.NuGetPackages.Count, detectedDeps.HostedServices.Count);

        // Track which namespaces actually have content
        detectedDeps.HasRepositories = apiSpec?.Repositories.Count > 0;
        detectedDeps.HasControllers = apiSpec?.Controllers.Count > 0;
        detectedDeps.HasDtos = apiSpec?.Dtos.Count > 0;

        // Generate .csproj file for Web API with detected packages
        var csprojPath = Path.Combine(projectFolder, $"{project.Name}.Api.csproj");
        await GenerateApiCsprojAsync(options, csprojPath, detectedDeps);

        // Generate Gateway.cs compatibility shim in Classes folder if needed
        if (detectedDeps.NeedsGatewayShim)
        {
            var classesFolder = Path.Combine(projectFolder, "Classes");
            Directory.CreateDirectory(classesFolder);
            await GenerateGatewayShimAsync(classesFolder, options.BaseNamespace);
        }

        // Generate repositories (extend BaseRepository from SfDApi.Classes)
        if (detectedDeps.HasRepositories)
        {
            var repoFolder = Path.Combine(projectFolder, options.ApiOptions.RepositoryNamespace);
            Directory.CreateDirectory(repoFolder);

            foreach (var repo in apiSpec!.Repositories)
            {
                var repoPath = Path.Combine(repoFolder, $"{repo.Name}.cs");
                await GenerateRepositoryFileAsync(repo, repoPath, options);
                LogFileGenerated(repoPath);
            }
        }

        // Generate controllers
        if (detectedDeps.HasControllers)
        {
            var controllerFolder = Path.Combine(projectFolder, options.ApiOptions.ControllerNamespace);
            Directory.CreateDirectory(controllerFolder);

            foreach (var controller in apiSpec!.Controllers)
            {
                var controllerPath = Path.Combine(controllerFolder, $"{controller.Name}.cs");
                await GenerateControllerFileAsync(controller, controllerPath, options);
                LogFileGenerated(controllerPath);
            }
        }

        // Generate DTOs
        if (detectedDeps.HasDtos)
        {
            var dtoFolder = Path.Combine(projectFolder, options.ApiOptions.DtoNamespace);
            Directory.CreateDirectory(dtoFolder);

            foreach (var dto in apiSpec!.Dtos)
            {
                var dtoPath = Path.Combine(dtoFolder, $"{dto.Name}.cs");
                await GenerateDtoFileAsync(dto, dtoPath, options);
            }
        }

        // Generate translated unit files (non-database code) with post-processing
        foreach (var result in summary.Results.Where(r => r.Success && r.TranslatedCode is not null))
        {
            var fileName = $"{Path.GetFileNameWithoutExtension(result.SourceFile)}.cs";
            var filePath = Path.Combine(projectFolder, fileName);

            // Post-process code for common issues
            var code = PostProcessTranslatedCode(result.TranslatedCode!, detectedDeps);
            await File.WriteAllTextAsync(filePath, code);
            LogFileGenerated(filePath);
        }

        // Generate Program.cs for API with detected services
        await GenerateApiProgramCsAsync(projectFolder, options, apiSpec, detectedDeps);

        // Generate GlobalUsings.cs with additional usings
        await GenerateApiGlobalUsingsAsync(projectFolder, options, detectedDeps);

        // Generate appsettings.json
        await GenerateAppSettingsAsync(projectFolder);

        // Generate README
        await GenerateReadmeAsync(project, summary, projectFolder, options);
    }

    private async Task GenerateReactProjectStructureAsync(
        DelphiProject project,
        TranslationOptions options,
        string projectFolder,
        List<ReactComponentDefinition>? components,
        ApiSpecification? apiSpec)
    {
        Directory.CreateDirectory(projectFolder);

        // Create React project structure
        var srcFolder = Path.Combine(projectFolder, "src");
        var componentsFolder = Path.Combine(srcFolder, "components");
        var pagesFolder = Path.Combine(srcFolder, "pages");
        var servicesFolder = Path.Combine(srcFolder, "services");
        var typesFolder = Path.Combine(srcFolder, "types");

        Directory.CreateDirectory(srcFolder);
        Directory.CreateDirectory(componentsFolder);
        Directory.CreateDirectory(pagesFolder);
        Directory.CreateDirectory(servicesFolder);
        Directory.CreateDirectory(typesFolder);

        // Generate package.json (with @sfd/web-common if configured)
        await GeneratePackageJsonAsync(projectFolder, project.Name, options);

        // Generate vite.config (js or ts based on configuration)
        await GenerateViteConfigAsync(projectFolder, options);

        // Generate API service
        await GenerateApiServiceAsync(servicesFolder, options, apiSpec);

        // Generate index.html (with correct script extension)
        await GenerateIndexHtmlAsync(projectFolder, project.Name, options);

        // Generate main.jsx/tsx (with AppInitializer if configured)
        await GenerateMainTsxAsync(srcFolder, options);

        // Generate App.jsx/tsx (with AuthProvider if configured)
        await GenerateAppTsxAsync(srcFolder, components, options);

        // Generate tsconfig.json only if not using @sfd/web-common (which uses JS)
        if (!options.ApiOptions.SfdWebCommon.UseSfdWebCommon)
        {
            await GenerateTsConfigAsync(projectFolder);
        }

        // Generate component files
        if (components is not null)
        {
            foreach (var component in components)
            {
                var folder = component.ComponentType == ComponentType.Page ? pagesFolder : componentsFolder;
                var extension = options.ApiOptions.SfdWebCommon.UseSfdWebCommon ? "jsx" : "tsx";
                var componentPath = Path.Combine(folder, $"{component.Name}.{extension}");
                // Component code should be pre-generated
                LogReactPlaceholder(componentPath);
            }
        }

        // Generate types from DTOs (only for TypeScript projects)
        if (!options.ApiOptions.SfdWebCommon.UseSfdWebCommon && apiSpec?.Dtos.Count > 0)
        {
            await GenerateTypeScriptTypesAsync(typesFolder, apiSpec.Dtos);
        }
    }

    private static async Task GenerateApiCsprojAsync(TranslationOptions options, string path, DetectedDependencies deps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<Project Sdk=\"Microsoft.NET.Sdk.Web\">");
        sb.AppendLine();
        sb.AppendLine("  <PropertyGroup>");
        sb.AppendLine($"    <TargetFramework>{options.TargetFramework}</TargetFramework>");
        sb.AppendLine("    <ImplicitUsings>enable</ImplicitUsings>");
        sb.AppendLine("    <Nullable>enable</Nullable>");
        sb.AppendLine("    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>");
        sb.AppendLine("    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>");
        sb.AppendLine("    <EnableNETAnalyzers>true</EnableNETAnalyzers>");
        sb.AppendLine("    <AnalysisLevel>latest</AnalysisLevel>");
        sb.AppendLine("  </PropertyGroup>");
        sb.AppendLine();

        // Project references
        var hasProjectRefs = (options.SfdGlobal.UseSfdGlobal && !string.IsNullOrEmpty(options.SfdGlobal.SfdGlobalProjectPath))
                          || !string.IsNullOrEmpty(options.SfdGlobal.SfDApiClassesProjectPath);

        if (hasProjectRefs)
        {
            sb.AppendLine("  <ItemGroup>");

            // Add SfD.Global project reference if configured
            if (options.SfdGlobal.UseSfdGlobal && !string.IsNullOrEmpty(options.SfdGlobal.SfdGlobalProjectPath))
            {
                sb.AppendLine($"    <ProjectReference Include=\"{options.SfdGlobal.SfdGlobalProjectPath}\" />");
            }

            // Add SfDApi.Classes project reference for BaseRepository
            if (!string.IsNullOrEmpty(options.SfdGlobal.SfDApiClassesProjectPath))
            {
                sb.AppendLine($"    <ProjectReference Include=\"{options.SfdGlobal.SfDApiClassesProjectPath}\" />");
            }

            sb.AppendLine("  </ItemGroup>");
            sb.AppendLine();
        }

        sb.AppendLine("  <ItemGroup>");

        // Core packages always included (Firebird database access)
        sb.AppendLine("    <PackageReference Include=\"Dapper\" Version=\"2.1.*\" />");
        sb.AppendLine("    <PackageReference Include=\"FirebirdSql.Data.FirebirdClient\" Version=\"10.*\" />");
        sb.AppendLine("    <PackageReference Include=\"Swashbuckle.AspNetCore\" Version=\"6.*\" />");

        // Add detected packages with appropriate versions
        foreach (var package in deps.NuGetPackages.OrderBy(p => p))
        {
            var version = GetPackageVersion(package);
            sb.AppendLine($"    <PackageReference Include=\"{package}\" Version=\"{version}\" />");
        }

        sb.AppendLine("  </ItemGroup>");
        sb.AppendLine();
        sb.AppendLine("</Project>");

        await File.WriteAllTextAsync(path, sb.ToString());
    }

    /// <summary>
    /// Gets the recommended version for a NuGet package.
    /// </summary>
    private static string GetPackageVersion(string packageName)
    {
        return packageName switch
        {
            "Microsoft.Extensions.Hosting" => "8.*",
            "Microsoft.Extensions.Http" => "8.*",
            "Microsoft.Extensions.Http.Polly" => "8.*",
            "Microsoft.Extensions.Caching.Memory" => "8.*",
            "Microsoft.AspNetCore.SignalR.Client" => "8.*",
            "MailKit" => "4.*",
            "Newtonsoft.Json" => "13.*",
            "AutoMapper" => "12.*",
            "AutoMapper.Extensions.Microsoft.DependencyInjection" => "12.*",
            "FluentValidation" => "11.*",
            "FluentValidation.DependencyInjectionExtensions" => "11.*",
            "MediatR" => "12.*",
            "Serilog.AspNetCore" => "8.*",
            "Polly" => "8.*",
            _ => "*" // Latest stable for unknown packages
        };
    }

    private static async Task GenerateBaseRepositoryAsync(string folder, string baseNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Data;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using Dapper;");
        sb.AppendLine("using FirebirdSql.Data.FirebirdClient;");
        sb.AppendLine();
        sb.AppendLine($"namespace {baseNamespace}.Classes;");
        sb.AppendLine();
        sb.AppendLine("public abstract class BaseRepository");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly AsyncLocal<FbTransaction?> _ambient = new();");
        sb.AppendLine();
        sb.AppendLine("    protected IDbConnection GetConnection(FbTransaction? external = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (external?.Connection is not null) return external.Connection;");
        sb.AppendLine("        if (_ambient.Value?.Connection is not null) return _ambient.Value.Connection;");
        sb.AppendLine("        throw new InvalidOperationException(\"No active connection available\");");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    protected FbTransaction? GetTransaction(FbTransaction? external = null)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (external is not null) return external;");
        sb.AppendLine("        return _ambient.Value;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public void Enlist(FbTransaction transaction) => _ambient.Value = transaction;");
        sb.AppendLine();
        sb.AppendLine("    protected void RollbackTransaction()");
        sb.AppendLine("    {");
        sb.AppendLine("        try { _ambient.Value?.Rollback(); }");
        sb.AppendLine("        catch { /* swallow */ }");
        sb.AppendLine("        finally");
        sb.AppendLine("        {");
        sb.AppendLine("            _ambient.Value?.Dispose();");
        sb.AppendLine("            _ambient.Value = null;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        var path = Path.Combine(folder, "BaseRepository.cs");
        await File.WriteAllTextAsync(path, sb.ToString());
    }

    /// <summary>
    /// Generates Gateway.cs compatibility shim for Delphi global/singleton patterns.
    /// </summary>
    private static async Task GenerateGatewayShimAsync(string folder, string baseNamespace)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {baseNamespace}.Classes;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine("/// Compatibility shim for Delphi Gateway global/singleton pattern.");
        sb.AppendLine("/// Provides common application-wide utilities that were available globally in Delphi.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class Gateway");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets or sets the application root path.");
        sb.AppendLine("    /// Defaults to the application base directory.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static string RootPath { get; set; } = AppDomain.CurrentDomain.BaseDirectory;");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Generates a new GUID string without dashes (matches Delphi CreateGUID format).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static string GetGUID() => Guid.NewGuid().ToString(\"N\");");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets a GUID with standard formatting (with dashes).");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static string GetGUIDFormatted() => Guid.NewGuid().ToString(\"D\");");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Combines the root path with a relative path.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static string GetFullPath(string relativePath) => Path.Combine(RootPath, relativePath);");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the application configuration folder path.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static string ConfigPath => Path.Combine(RootPath, \"Config\");");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Gets the application logs folder path.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static string LogPath => Path.Combine(RootPath, \"Logs\");");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Called when the application/service starts.");
        sb.AppendLine("    /// Override behavior by setting the OnStart action.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static Action? OnStart { get; set; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Called when the application/service stops.");
        sb.AppendLine("    /// Override behavior by setting the OnStop action.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static Action? OnStop { get; set; }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Initializes the Gateway. Call from service Start.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static void Start()");
        sb.AppendLine("    {");
        sb.AppendLine("        // Ensure directories exist");
        sb.AppendLine("        Directory.CreateDirectory(ConfigPath);");
        sb.AppendLine("        Directory.CreateDirectory(LogPath);");
        sb.AppendLine("        OnStart?.Invoke();");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Cleanup when service stops.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static void Stop()");
        sb.AppendLine("    {");
        sb.AppendLine("        OnStop?.Invoke();");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        var path = Path.Combine(folder, "Gateway.cs");
        await File.WriteAllTextAsync(path, sb.ToString());
    }

    /// <summary>
    /// Post-processes translated code to fix common issues.
    /// </summary>
    private static string PostProcessTranslatedCode(string code, DetectedDependencies deps)
    {
        // Fix ServiceBase usage - convert to console application
        if (deps.UsesServiceBase || code.Contains("ServiceBase") || code.Contains("System.ServiceProcess"))
        {
            code = ConvertServiceToConsoleApp(code);
        }

        // Remove duplicate Gateway class definitions (should use Classes/Gateway.cs)
        code = RemoveDuplicateGatewayClass(code);

        // Fix RootPath references to use Gateway.RootPath
        code = FixRootPathReferences(code);

        // Fix unconverted email component references
        code = FixEmailComponentReferences(code);

        // Fix readonly field issues
        code = FixReadonlyFieldAssignments(code);

        // Fix entry point conflicts
        code = FixEntryPointConflicts(code);

        // Fix nullable reference issues
        code = FixNullableReferenceIssues(code);

        return code;
    }

    /// <summary>
    /// Fixes unconverted email component references.
    /// </summary>
    private static string FixEmailComponentReferences(string code)
    {
        // Fix ClMailMessage property that should have been converted to MimeMessage
        code = code.Replace("public MimeMessage ClMailMessage", "public MimeMessage? Message");
        code = code.Replace("public MimeMessage clMailMessage", "public MimeMessage? Message");
        code = code.Replace(".ClMailMessage", ".Message");
        code = code.Replace(".clMailMessage", ".Message");

        // Fix FMailMessage references
        code = code.Replace("public MimeMessage FMailMessage", "public MimeMessage? Message");
        code = code.Replace(".FMailMessage", ".Message");

        // Fix FSmtp references  
        code = code.Replace("public SmtpClient FSmtp", "public SmtpClient? Client");
        code = code.Replace(".FSmtp", ".Client");

        // Remove Progress and SendCommand event subscriptions that don't exist in MailKit
        var lines = code.Split('\n').ToList();
        lines = [.. lines.Where(l =>
            !l.Contains(".Progress +=") &&
            !l.Contains(".SendCommand +=") &&
            !l.Contains(".BodyEncoding =") &&
            !l.Contains(".SubjectEncoding ="))];

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Fixes readonly field assignments outside of constructors.
    /// </summary>
    private static string FixReadonlyFieldAssignments(string code)
    {
        // Find readonly fields
        var readonlyFieldPattern = ReadonlyFieldRegex();
        var matches = readonlyFieldPattern.Matches(code);

        foreach (Match match in matches)
        {
            var fieldName = match.Groups[1].Value;

            // Check if this field is assigned outside a constructor
            // Simple heuristic: if field is assigned with "= new" or "= value" outside constructor
            var assignmentPattern = new Regex($@"(?<!readonly\s+\w+\s+){fieldName}\s*=\s*(?!null)");

            // If there's an assignment that's not in a constructor context, remove readonly
            if (assignmentPattern.IsMatch(code))
            {
                // Remove readonly from this field declaration
                code = code.Replace($"private readonly ", "private ");
            }
        }

        // Also fix specific patterns: connection fields should not be readonly if reassigned
        if (code.Contains("_sqlConnection =") || code.Contains("_connection =") || code.Contains("_fbConnection ="))
        {
            code = code.Replace("private readonly SqlConnection _sqlConnection", "private SqlConnection? _sqlConnection");
            code = code.Replace("private readonly FbConnection _connection", "private FbConnection? _connection");
            code = code.Replace("private readonly FbConnection _fbConnection", "private FbConnection? _fbConnection");
        }

        return code;
    }

    [GeneratedRegex(@"private\s+readonly\s+\w+\s+(\w+)\s*;")]
    private static partial Regex ReadonlyFieldRegex();

    /// <summary>
    /// Fixes entry point conflicts (both top-level statements and Main method).
    /// </summary>
    private static string FixEntryPointConflicts(string code)
    {
        // If there's both a Program.Main and what looks like top-level code, remove the Main
        if (code.Contains("public static async Task Main(") || code.Contains("public static void Main("))
        {
            // Check if there's also top-level code patterns
            var hasTopLevelCode = code.Contains("await ") && !code.Contains("class ") && !code.Contains("namespace ");

            if (!hasTopLevelCode)
            {
                // This file has a Main method, ensure it's the only entry point
                // Remove any top-level await statements that might conflict
                return code;
            }
        }

        // Remove duplicate Main methods if file is meant to be a service/utility class
        if ((code.Contains("class Email") || code.Contains("class Connection") || code.Contains("class Service"))
            && !code.Contains("class Program"))
        {
            // Remove standalone Main method from utility classes
            code = RemoveMainMethod(code);
        }

        return code;
    }

    /// <summary>
    /// Removes a Main method from code (used when it shouldn't be an entry point).
    /// </summary>
    private static string RemoveMainMethod(string code)
    {
        var lines = code.Split('\n').ToList();
        var result = new List<string>();
        var inMainMethod = false;
        var braceDepth = 0;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            // Detect Main method start
            if ((trimmed.Contains("public static void Main(") ||
                 trimmed.Contains("public static async Task Main(") ||
                 trimmed.Contains("static void Main(") ||
                 trimmed.Contains("static async Task Main(")) && !inMainMethod)
            {
                inMainMethod = true;
                braceDepth = 0;
                continue;
            }

            if (inMainMethod)
            {
                braceDepth += line.Count(c => c == '{');
                braceDepth -= line.Count(c => c == '}');

                if (braceDepth <= 0 && trimmed == "}")
                {
                    inMainMethod = false;
                    continue;
                }
                continue; // Skip lines inside Main
            }

            result.Add(line);
        }

        return string.Join('\n', result);
    }

    /// <summary>
    /// Fixes common nullable reference type issues.
    /// </summary>
    private static string FixNullableReferenceIssues(string code)
    {
        // Make connection fields nullable
        code = code.Replace("private SqlConnection _sqlConnection;", "private SqlConnection? _sqlConnection;");
        code = code.Replace("private FbConnection _connection;", "private FbConnection? _connection;");
        code = code.Replace("private FbConnection _fbConnection;", "private FbConnection? _fbConnection;");
        code = code.Replace("private SmtpClient _client;", "private SmtpClient? _client;");
        code = code.Replace("private MimeMessage _message;", "private MimeMessage? _message;");

        // Fix null assignments to non-nullable
        code = code.Replace("_sqlConnection = null;", "_sqlConnection = null;"); // Already nullable after above
        code = code.Replace("_connection = null;", "_connection = null;");

        // Wrap Directory.CreateDirectory calls with null checks
        code = DirectoryCreateDirectoryNullCheckRegex().Replace(code,
            "if (!string.IsNullOrEmpty($1)) Directory.CreateDirectory($1)");

        return code;
    }

    [GeneratedRegex(@"Directory\.CreateDirectory\((\w+)\)")]
    private static partial Regex DirectoryCreateDirectoryNullCheckRegex();

    /// <summary>
    /// Removes duplicate Gateway class definitions from translated code.
    /// </summary>
    private static string RemoveDuplicateGatewayClass(string code)
    {
        // Remove standalone Gateway class definition (will use the one from Classes folder)
        var lines = code.Split('\n').ToList();
        var result = new List<string>();
        var skipUntilCloseBrace = false;
        var braceDepth = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Detect start of duplicate Gateway class
            if ((trimmed.StartsWith("public static class Gateway") ||
                 trimmed.StartsWith("public class Gateway")) && !skipUntilCloseBrace)
            {
                skipUntilCloseBrace = true;
                braceDepth = 0;
                result.Add("// Duplicate Gateway class removed - using ConvertedApp.Classes.Gateway instead");
                continue;
            }

            if (skipUntilCloseBrace)
            {
                braceDepth += line.Count(c => c == '{');
                braceDepth -= line.Count(c => c == '}');

                if (braceDepth <= 0 && trimmed == "}")
                {
                    skipUntilCloseBrace = false;
                }
                continue;
            }

            result.Add(line);
        }

        return string.Join('\n', result);
    }

    /// <summary>
    /// Fixes RootPath references to use Gateway.RootPath.
    /// </summary>
    private static string FixRootPathReferences(string code)
    {
        // Fix bare RootPath at start of expressions using string operations
        var lines = code.Split('\n').ToList();
        var result = new List<string>();

        foreach (var line in lines)
        {
            var fixedLine = line;

            // Match RootPath that's not already prefixed with Gateway. or part of another word
            if (fixedLine.Contains("RootPath") && !fixedLine.Contains("Gateway.RootPath"))
            {
                // Check if it's a standalone reference (not a property definition)
                if (!fixedLine.Contains("public") && !fixedLine.Contains("private") &&
                    !fixedLine.Contains("static string RootPath"))
                {
                    fixedLine = fixedLine.Replace("RootPath +", "Gateway.RootPath +");
                    fixedLine = fixedLine.Replace("RootPath;", "Gateway.RootPath;");
                    fixedLine = fixedLine.Replace("= RootPath", "= Gateway.RootPath");
                    fixedLine = fixedLine.Replace("(RootPath", "(Gateway.RootPath");
                }
            }

            result.Add(fixedLine);
        }

        return string.Join('\n', result);
    }

    /// <summary>
    /// Converts Delphi Windows Service (ServiceBase) to a C# console application.
    /// Console apps can be run directly or installed as services using NSSM or sc.exe.
    /// </summary>
    private static string ConvertServiceToConsoleApp(string code)
    {
        // Remove System.ServiceProcess using - not needed for console app
        code = code.Replace("using System.ServiceProcess;", "");

        // Remove ServiceBase inheritance - console app doesn't need a base class
        code = ServiceBaseInheritanceRegex().Replace(code, "");

        // Remove ServiceName property assignment
        code = string.Join('\n', code.Split('\n').Where(l => !l.Contains("ServiceName =")));

        // Remove ServiceBase.Run calls
        code = string.Join('\n', code.Split('\n').Where(l => !l.Contains("ServiceBase.Run")));

        // Check if this is a service class that needs to be converted to Program.cs style
        if (code.Contains("class") && (code.Contains("OnStart") || code.Contains("OnStop")))
        {
            // This is a service class - convert to console app pattern
            code = ConvertServiceClassToConsoleApp(code);
        }

        return code;
    }

    /// <summary>
    /// Converts a Delphi TService class to a C# console application Program class.
    /// </summary>
    private static string ConvertServiceClassToConsoleApp(string code)
    {
        var lines = code.Split('\n').ToList();
        var result = new List<string>();
        var inClass = false;
        var onStartBody = new List<string>();
        var onStopBody = new List<string>();
        var currentMethod = "";
        var methodBody = new List<string>();
        var inMethod = false;
        var methodBraceDepth = 0;

        // First pass: extract OnStart and OnStop bodies
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            // Track class entry
            if (trimmed.Contains("class ") && !trimmed.StartsWith("//"))
            {
                inClass = true;
            }

            if (inClass)
            {
                // Check for OnStart method
                if (trimmed.Contains("OnStart") && trimmed.Contains("void"))
                {
                    currentMethod = "OnStart";
                    inMethod = true;
                    methodBraceDepth = 0;
                    methodBody.Clear();
                    continue;
                }

                // Check for OnStop method
                if (trimmed.Contains("OnStop") && trimmed.Contains("void"))
                {
                    currentMethod = "OnStop";
                    inMethod = true;
                    methodBraceDepth = 0;
                    methodBody.Clear();
                    continue;
                }

                if (inMethod)
                {
                    methodBraceDepth += line.Count(c => c == '{');
                    methodBraceDepth -= line.Count(c => c == '}');

                    if (methodBraceDepth > 0 || (methodBraceDepth == 0 && !trimmed.StartsWith('}')))
                    {
                        if (!trimmed.StartsWith('{') && methodBraceDepth > 0)
                        {
                            methodBody.Add(line);
                        }
                    }

                    if (methodBraceDepth <= 0 && trimmed == "}")
                    {
                        if (currentMethod == "OnStart")
                        {
                            onStartBody.AddRange(methodBody);
                        }
                        else if (currentMethod == "OnStop")
                        {
                            onStopBody.AddRange(methodBody);
                        }
                        inMethod = false;
                        currentMethod = "";
                    }
                }
            }
        }

        // Generate console app code
        result.Add("using System;");
        result.Add("using System.Threading;");
        result.Add("using System.Threading.Tasks;");
        result.Add("");
        result.Add("namespace ConvertedApp;");
        result.Add("");
        result.Add("/// <summary>");
        result.Add("/// Console application converted from Windows Service.");
        result.Add("/// Run with: dotnet run");
        result.Add("/// Install as service with: sc create ServiceName binPath=\"path\\to\\exe\"");
        result.Add("/// Or use NSSM: nssm install ServiceName path\\to\\exe");
        result.Add("/// </summary>");
        result.Add("public class Program");
        result.Add("{");
        result.Add("    private static readonly CancellationTokenSource _cts = new();");
        result.Add("    private static bool _isRunning = true;");
        result.Add("");
        result.Add("    public static async Task Main(string[] args)");
        result.Add("    {");
        result.Add("        // Handle Ctrl+C gracefully");
        result.Add("        Console.CancelKeyPress += (sender, e) =>");
        result.Add("        {");
        result.Add("            e.Cancel = true;");
        result.Add("            _cts.Cancel();");
        result.Add("            _isRunning = false;");
        result.Add("            Console.WriteLine(\"Shutdown signal received...\");");
        result.Add("        };");
        result.Add("");
        result.Add("        // Handle process termination (for service managers)");
        result.Add("        AppDomain.CurrentDomain.ProcessExit += (sender, e) =>");
        result.Add("        {");
        result.Add("            _cts.Cancel();");
        result.Add("            _isRunning = false;");
        result.Add("        };");
        result.Add("");
        result.Add("        Console.WriteLine(\"Service starting...\");");
        result.Add("");
        result.Add("        // === OnStart logic ===");

        // Add OnStart body
        if (onStartBody.Count > 0)
        {
            foreach (var line in onStartBody)
            {
                // Re-indent
                var trimmed = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    result.Add("        " + trimmed);
                }
            }
        }
        else
        {
            result.Add("        Gateway.Start();");
        }

        result.Add("");
        result.Add("        Console.WriteLine(\"Service started. Press Ctrl+C to stop.\");");
        result.Add("");
        result.Add("        // Main service loop");
        result.Add("        try");
        result.Add("        {");
        result.Add("            while (_isRunning && !_cts.Token.IsCancellationRequested)");
        result.Add("            {");
        result.Add("                // Service processing happens here");
        result.Add("                await Task.Delay(100, _cts.Token);");
        result.Add("            }");
        result.Add("        }");
        result.Add("        catch (OperationCanceledException)");
        result.Add("        {");
        result.Add("            // Expected during shutdown");
        result.Add("        }");
        result.Add("");
        result.Add("        Console.WriteLine(\"Service stopping...\");");
        result.Add("");
        result.Add("        // === OnStop logic ===");

        // Add OnStop body
        if (onStopBody.Count > 0)
        {
            foreach (var line in onStopBody)
            {
                var trimmed = line.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    result.Add("        " + trimmed);
                }
            }
        }
        else
        {
            result.Add("        Gateway.Stop();");
        }

        result.Add("");
        result.Add("        Console.WriteLine(\"Service stopped.\");");
        result.Add("    }");
        result.Add("}");

        return string.Join('\n', result);
    }

    /// <summary>
    /// Replaces an entire method (signature + body) with new content.
    /// </summary>
    private static string ReplaceMethodEntirely(string code, string methodSignature, Func<string, string> replacement)
    {
        var lines = code.Split('\n').ToList();
        var result = new List<string>();
        var i = 0;

        while (i < lines.Count)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            // Check if this line contains the method signature
            if (trimmed.StartsWith(methodSignature.Split('(')[0]) && trimmed.Contains('('))
            {
                // Found the method - extract the body
                var bodyLines = new List<string>();
                var braceDepth = 0;

                // Skip to opening brace
                while (i < lines.Count)
                {
                    var currentLine = lines[i];
                    if (currentLine.Contains('{'))
                    {
                        braceDepth += currentLine.Count(c => c == '{');
                        braceDepth -= currentLine.Count(c => c == '}');
                        i++;
                        break;
                    }
                    i++;
                }

                // Collect body until closing brace
                while (i < lines.Count && braceDepth > 0)
                {
                    var currentLine = lines[i];
                    braceDepth += currentLine.Count(c => c == '{');
                    braceDepth -= currentLine.Count(c => c == '}');

                    if (braceDepth > 0)
                    {
                        bodyLines.Add(currentLine);
                    }
                    i++;
                }

                // Add the replacement
                var body = string.Join('\n', bodyLines);
                var newContent = replacement(body);

                // Add proper indentation
                var indent = line.Length - line.TrimStart().Length;
                var indentStr = new string(' ', indent);
                foreach (var newLine in newContent.Split('\n'))
                {
                    result.Add(indentStr + newLine.TrimStart());
                }

                continue;
            }

            result.Add(line);
            i++;
        }

        return string.Join('\n', result);
    }

    /// <summary>
    /// Removes duplicate method definitions.
    /// </summary>
    private static string RemoveDuplicateMethods(string code, string methodSignatureStart)
    {
        var lines = code.Split('\n').ToList();
        var result = new List<string>();
        var foundFirst = false;
        var i = 0;

        while (i < lines.Count)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.StartsWith(methodSignatureStart))
            {
                if (foundFirst)
                {
                    // Skip this duplicate method entirely
                    var braceDepth = 0;
                    while (i < lines.Count)
                    {
                        var currentLine = lines[i];
                        braceDepth += currentLine.Count(c => c == '{');
                        braceDepth -= currentLine.Count(c => c == '}');
                        i++;

                        if (braceDepth <= 0 && currentLine.Trim() == "}")
                        {
                            break;
                        }
                    }
                    result.Add("    // Duplicate method removed");
                    continue;
                }
                foundFirst = true;
            }

            result.Add(line);
            i++;
        }

        return string.Join('\n', result);
    }

    private static async Task GenerateDtoFileAsync(DtoDefinition dto, string path, TranslationOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"namespace {options.BaseNamespace}.{options.ApiOptions.DtoNamespace};");
        sb.AppendLine();

        // Source Delphi units comment
        if (dto.SourceUnits.Count > 0)
        {
            sb.AppendLine("// =============================================================================");
            sb.AppendLine("// Source Delphi Units:");
            foreach (var unit in dto.SourceUnits)
            {
                sb.AppendLine($"//   - {unit}");
            }
            sb.AppendLine("// =============================================================================");
            sb.AppendLine();
        }

        if (!string.IsNullOrEmpty(dto.Description))
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// {dto.Description}");
            sb.AppendLine("/// </summary>");
        }

        if (dto.UseRecord)
        {
            sb.Append($"public record {dto.Name}(");
            var props = dto.Properties.Select(p =>
                $"{p.Type}{(p.IsNullable ? "?" : "")} {p.Name}");
            sb.Append(string.Join(", ", props));
            sb.AppendLine(");");
        }
        else
        {
            sb.AppendLine($"public class {dto.Name}");
            sb.AppendLine("{");
            foreach (var prop in dto.Properties)
            {
                if (!string.IsNullOrEmpty(prop.Description))
                {
                    sb.AppendLine($"    /// <summary>{prop.Description}</summary>");
                }
                sb.AppendLine($"    public {prop.Type}{(prop.IsNullable ? "?" : "")} {prop.Name} {{ get; set; }}");
            }
            sb.AppendLine("}");
        }

        await File.WriteAllTextAsync(path, sb.ToString());
    }

    /// <summary>
    /// Generates a controller class with source Delphi unit comments.
    /// </summary>
    private static async Task GenerateControllerFileAsync(ControllerDefinition controller, string path, TranslationOptions options)
    {
        var sb = new StringBuilder();

        // Using statements
        sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
        sb.AppendLine();
        sb.AppendLine($"namespace {options.BaseNamespace}.{options.ApiOptions.ControllerNamespace};");
        sb.AppendLine();

        // Source Delphi units comment
        if (controller.SourceUnits.Count > 0)
        {
            sb.AppendLine("// =============================================================================");
            sb.AppendLine("// Source Delphi Units:");
            foreach (var unit in controller.SourceUnits)
            {
                sb.AppendLine($"//   - {unit}");
            }
            sb.AppendLine("// =============================================================================");
            sb.AppendLine();
        }

        // XML documentation
        if (!string.IsNullOrEmpty(controller.Description))
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// {controller.Description}");
            sb.AppendLine("/// </summary>");
        }

        // Controller attributes and class declaration
        sb.AppendLine("[ApiController]");
        sb.AppendLine($"[Route(\"{options.ApiOptions.ApiRoutePrefix}/{controller.Route}\")]");
        sb.AppendLine($"public class {controller.Name} : ControllerBase");
        sb.AppendLine("{");

        // Constructor with logger injection
        sb.AppendLine($"    private readonly ILogger<{controller.Name}> _logger;");
        sb.AppendLine();
        sb.AppendLine($"    public {controller.Name}(ILogger<{controller.Name}> logger)");
        sb.AppendLine("    {");
        sb.AppendLine("        _logger = logger;");
        sb.AppendLine("    }");

        // Generate actions
        foreach (var action in controller.Actions)
        {
            sb.AppendLine();
            GenerateControllerAction(sb, action);
        }

        // If no actions defined, generate a sample action
        if (controller.Actions.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("    // TODO: Add controller actions");
            sb.AppendLine("    // Example:");
            sb.AppendLine("    // [HttpGet]");
            sb.AppendLine("    // public async Task<IActionResult> GetAll()");
            sb.AppendLine("    // {");
            sb.AppendLine("    //     return Ok(new { message = \"Not implemented\" });");
            sb.AppendLine("    // }");
        }

        sb.AppendLine("}");

        await File.WriteAllTextAsync(path, sb.ToString());
    }

    /// <summary>
    /// Generates a single controller action.
    /// </summary>
    private static void GenerateControllerAction(StringBuilder sb, ControllerAction action)
    {
        // Source Delphi units comment for this action
        if (action.SourceUnits.Count > 0)
        {
            sb.AppendLine($"    // Source: {string.Join(", ", action.SourceUnits)}");
        }

        // HTTP method attribute
        var httpAttribute = action.HttpMethod.ToUpperInvariant() switch
        {
            "GET" => "[HttpGet]",
            "POST" => "[HttpPost]",
            "PUT" => "[HttpPut]",
            "DELETE" => "[HttpDelete]",
            "PATCH" => "[HttpPatch]",
            _ => "[HttpGet]"
        };

        if (!string.IsNullOrEmpty(action.Route))
        {
            httpAttribute = httpAttribute.Replace("]", $"(\"{action.Route}\")]");
        }

        sb.AppendLine($"    {httpAttribute}");

        // Build parameter list
        var parameters = action.Parameters.Select(p =>
        {
            var paramStr = $"{p.Type} {p.Name}";
            if (p.DefaultValue is not null)
            {
                paramStr += $" = {p.DefaultValue}";
            }
            return paramStr;
        });

        var paramList = string.Join(", ", parameters);

        // Method signature
        sb.AppendLine($"    public async {action.ReturnType} {action.Name}({paramList})");
        sb.AppendLine("    {");
        sb.AppendLine("        // TODO: Implement action logic");
        sb.AppendLine("        throw new NotImplementedException();");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Generates a repository class that extends BaseRepository from SfDApi.Classes.
    /// All database access uses Firebird via Dapper.
    /// </summary>
    private static async Task GenerateRepositoryFileAsync(RepositoryDefinition repo, string path, TranslationOptions options)
    {
        var sb = new StringBuilder();

        // Using statements - reference SfDApi.Classes for BaseRepository
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Data;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Dapper;");
        sb.AppendLine("using FirebirdSql.Data.FirebirdClient;");
        sb.AppendLine("using SfDApi.Classes;");

        // Add DTO namespace if we have DTOs
        if (options.ApiOptions.GenerateDtos)
        {
            sb.AppendLine($"using {options.BaseNamespace}.{options.ApiOptions.DtoNamespace};");
        }

        sb.AppendLine();
        sb.AppendLine($"namespace {options.BaseNamespace}.{options.ApiOptions.RepositoryNamespace};");
        sb.AppendLine();

        // Source Delphi units comment
        if (repo.SourceUnits.Count > 0)
        {
            sb.AppendLine("// =============================================================================");
            sb.AppendLine("// Source Delphi Units:");
            foreach (var unit in repo.SourceUnits)
            {
                sb.AppendLine($"//   - {unit}");
            }
            sb.AppendLine("// =============================================================================");
            sb.AppendLine();
        }

        // XML documentation
        if (!string.IsNullOrEmpty(repo.Description))
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// {repo.Description}");
            sb.AppendLine("/// </summary>");
        }
        else
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// Repository for database operations. Extends BaseRepository for Firebird transaction support.");
            sb.AppendLine("/// </summary>");
        }

        // Class declaration - extends BaseRepository
        sb.AppendLine($"public class {repo.Name} : BaseRepository");
        sb.AppendLine("{");

        // Constructor with FbConnection injection
        sb.AppendLine("    private readonly FbConnection _connection;");
        sb.AppendLine();
        sb.AppendLine($"    public {repo.Name}(FbConnection connection)");
        sb.AppendLine("    {");
        sb.AppendLine("        _connection = connection ?? throw new ArgumentNullException(nameof(connection));");
        sb.AppendLine("    }");

        // Generate methods
        foreach (var method in repo.Methods)
        {
            sb.AppendLine();
            GenerateRepositoryMethod(sb, method, options);
        }

        // If no methods defined, generate a sample method
        if (repo.Methods.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("    // TODO: Add repository methods for database operations");
            sb.AppendLine("    // Example:");
            sb.AppendLine("    // public async Task<IEnumerable<T>> GetAllAsync<T>(FbTransaction? transaction = null)");
            sb.AppendLine("    // {");
            sb.AppendLine("    //     var conn = GetConnection(transaction);");
            sb.AppendLine("    //     var trans = GetTransaction(transaction);");
            sb.AppendLine("    //     return await conn.QueryAsync<T>(\"SELECT * FROM TableName\", transaction: trans);");
            sb.AppendLine("    // }");
        }

        sb.AppendLine("}");

        await File.WriteAllTextAsync(path, sb.ToString());
    }

    /// <summary>
    /// Generates a single repository method using Dapper and Firebird patterns.
    /// </summary>
    private static void GenerateRepositoryMethod(StringBuilder sb, RepositoryMethod method, TranslationOptions _)
    {
        // Source Delphi units comment for this method
        if (method.SourceUnits.Count > 0)
        {
            sb.AppendLine($"    // Source: {string.Join(", ", method.SourceUnits)}");
        }

        // XML documentation
        if (!string.IsNullOrEmpty(method.Description))
        {
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {method.Description}");
            sb.AppendLine($"    /// </summary>");
        }

        // Build parameter list
        var parameters = new List<string>();
        foreach (var param in method.Parameters)
        {
            var paramStr = $"{param.Type} {param.Name}";
            if (param.DefaultValue is not null)
            {
                paramStr += $" = {param.DefaultValue}";
            }
            parameters.Add(paramStr);
        }

        // Always add optional transaction parameter for Firebird support
        if (!method.Parameters.Any(p => p.Name.Equals("transaction", StringComparison.OrdinalIgnoreCase)))
        {
            parameters.Add("FbTransaction? transaction = null");
        }

        var paramList = string.Join(", ", parameters);

        // Method signature
        sb.AppendLine($"    public async {method.ReturnType} {method.Name}({paramList})");
        sb.AppendLine("    {");

        // Get connection and transaction from BaseRepository
        sb.AppendLine("        var conn = GetConnection(transaction);");
        sb.AppendLine("        var trans = GetTransaction(transaction);");
        sb.AppendLine();

        // Generate method body based on operation type
        switch (method.OperationType)
        {
            case DatabaseOperationType.Select:
                GenerateSelectMethodBody(sb, method);
                break;
            case DatabaseOperationType.Insert:
                GenerateInsertMethodBody(sb, method);
                break;
            case DatabaseOperationType.Update:
                GenerateUpdateMethodBody(sb, method);
                break;
            case DatabaseOperationType.Delete:
                GenerateDeleteMethodBody(sb, method);
                break;
            case DatabaseOperationType.StoredProcedure:
                GenerateStoredProcMethodBody(sb, method);
                break;
            case DatabaseOperationType.ExecuteScalar:
                GenerateExecuteScalarMethodBody(sb, method);
                break;
            default:
                GenerateDefaultMethodBody(sb, method);
                break;
        }

        sb.AppendLine("    }");
    }

    private static void GenerateSelectMethodBody(StringBuilder sb, RepositoryMethod method)
    {
        var sql = string.IsNullOrEmpty(method.SqlStatement) ? "SELECT * FROM TableName WHERE 1=1" : method.SqlStatement;

        // Build Dapper parameters
        var dapperParams = method.Parameters
            .Where(p => !p.Name.Equals("transaction", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (dapperParams.Count > 0)
        {
            sb.AppendLine($"        const string sql = @\"{sql}\";");
            sb.AppendLine();
            sb.AppendLine("        var parameters = new");
            sb.AppendLine("        {");
            foreach (var param in dapperParams)
            {
                sb.AppendLine($"            {param.Name},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        return await conn.QueryAsync<dynamic>(sql, parameters, trans);");
        }
        else
        {
            sb.AppendLine($"        const string sql = @\"{sql}\";");
            sb.AppendLine();
            sb.AppendLine("        return await conn.QueryAsync<dynamic>(sql, transaction: trans);");
        }
    }

    private static void GenerateInsertMethodBody(StringBuilder sb, RepositoryMethod method)
    {
        var sql = string.IsNullOrEmpty(method.SqlStatement)
            ? "INSERT INTO TableName (Column1, Column2) VALUES (@Value1, @Value2)"
            : method.SqlStatement;

        sb.AppendLine($"        const string sql = @\"{sql}\";");
        sb.AppendLine();

        var dapperParams = method.Parameters
            .Where(p => !p.Name.Equals("transaction", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (dapperParams.Count > 0)
        {
            sb.AppendLine("        var parameters = new");
            sb.AppendLine("        {");
            foreach (var param in dapperParams)
            {
                sb.AppendLine($"            {param.Name},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        return await conn.ExecuteAsync(sql, parameters, trans);");
        }
        else
        {
            sb.AppendLine("        return await conn.ExecuteAsync(sql, transaction: trans);");
        }
    }

    private static void GenerateUpdateMethodBody(StringBuilder sb, RepositoryMethod method)
    {
        var sql = string.IsNullOrEmpty(method.SqlStatement)
            ? "UPDATE TableName SET Column1 = @Value1 WHERE Id = @Id"
            : method.SqlStatement;

        sb.AppendLine($"        const string sql = @\"{sql}\";");
        sb.AppendLine();

        var dapperParams = method.Parameters
            .Where(p => !p.Name.Equals("transaction", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (dapperParams.Count > 0)
        {
            sb.AppendLine("        var parameters = new");
            sb.AppendLine("        {");
            foreach (var param in dapperParams)
            {
                sb.AppendLine($"            {param.Name},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        return await conn.ExecuteAsync(sql, parameters, trans);");
        }
        else
        {
            sb.AppendLine("        return await conn.ExecuteAsync(sql, transaction: trans);");
        }
    }

    private static void GenerateDeleteMethodBody(StringBuilder sb, RepositoryMethod method)
    {
        var sql = string.IsNullOrEmpty(method.SqlStatement)
            ? "DELETE FROM TableName WHERE Id = @Id"
            : method.SqlStatement;

        sb.AppendLine($"        const string sql = @\"{sql}\";");
        sb.AppendLine();

        var dapperParams = method.Parameters
            .Where(p => !p.Name.Equals("transaction", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (dapperParams.Count > 0)
        {
            sb.AppendLine("        var parameters = new");
            sb.AppendLine("        {");
            foreach (var param in dapperParams)
            {
                sb.AppendLine($"            {param.Name},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        return await conn.ExecuteAsync(sql, parameters, trans);");
        }
        else
        {
            sb.AppendLine("        return await conn.ExecuteAsync(sql, transaction: trans);");
        }
    }

    private static void GenerateStoredProcMethodBody(StringBuilder sb, RepositoryMethod method)
    {
        var sql = string.IsNullOrEmpty(method.SqlStatement)
            ? "EXECUTE PROCEDURE ProcedureName"
            : method.SqlStatement;

        sb.AppendLine($"        const string sql = @\"{sql}\";");
        sb.AppendLine();

        var dapperParams = method.Parameters
            .Where(p => !p.Name.Equals("transaction", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (dapperParams.Count > 0)
        {
            sb.AppendLine("        var parameters = new");
            sb.AppendLine("        {");
            foreach (var param in dapperParams)
            {
                sb.AppendLine($"            {param.Name},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        return await conn.QueryAsync<dynamic>(sql, parameters, trans, commandType: CommandType.StoredProcedure);");
        }
        else
        {
            sb.AppendLine("        return await conn.QueryAsync<dynamic>(sql, transaction: trans, commandType: CommandType.StoredProcedure);");
        }
    }

    private static void GenerateExecuteScalarMethodBody(StringBuilder sb, RepositoryMethod method)
    {
        var sql = string.IsNullOrEmpty(method.SqlStatement)
            ? "SELECT COUNT(*) FROM TableName"
            : method.SqlStatement;

        sb.AppendLine($"        const string sql = @\"{sql}\";");
        sb.AppendLine();

        var dapperParams = method.Parameters
            .Where(p => !p.Name.Equals("transaction", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (dapperParams.Count > 0)
        {
            sb.AppendLine("        var parameters = new");
            sb.AppendLine("        {");
            foreach (var param in dapperParams)
            {
                sb.AppendLine($"            {param.Name},");
            }
            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        return await conn.ExecuteScalarAsync<object>(sql, parameters, trans);");
        }
        else
        {
            sb.AppendLine("        return await conn.ExecuteScalarAsync<object>(sql, transaction: trans);");
        }
    }

    private static void GenerateDefaultMethodBody(StringBuilder sb, RepositoryMethod method)
    {
        if (!string.IsNullOrEmpty(method.SqlStatement))
        {
            sb.AppendLine($"        const string sql = @\"{method.SqlStatement}\";");
            sb.AppendLine();

            var dapperParams = method.Parameters
                .Where(p => !p.Name.Equals("transaction", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (dapperParams.Count > 0)
            {
                sb.AppendLine("        var parameters = new");
                sb.AppendLine("        {");
                foreach (var param in dapperParams)
                {
                    sb.AppendLine($"            {param.Name},");
                }
                sb.AppendLine("        };");
                sb.AppendLine();
                sb.AppendLine("        return await conn.ExecuteAsync(sql, parameters, trans);");
            }
            else
            {
                sb.AppendLine("        return await conn.ExecuteAsync(sql, transaction: trans);");
            }
        }
        else
        {
            sb.AppendLine("        // TODO: Implement database operation");
            sb.AppendLine("        throw new NotImplementedException();");
        }
    }

    private static async Task GenerateApiProgramCsAsync(
        string projectFolder,
        TranslationOptions options,
        ApiSpecification? apiSpec,
        DetectedDependencies deps)
    {
        var sb = new StringBuilder();

        if (options.SfdGlobal.UseSfdGlobal)
        {
            // Generate SfD.Global integrated Program.cs
            GenerateSfdGlobalProgramCs(sb, options, apiSpec, deps);
        }
        else
        {
            // Generate standard Program.cs (existing behavior)
            GenerateStandardProgramCs(sb, options, apiSpec, deps);
        }

        var path = Path.Combine(projectFolder, "Program.cs");
        await File.WriteAllTextAsync(path, sb.ToString());
    }

    /// <summary>
    /// Generates Program.cs using SfD.Global ConfigService, ServiceAuthenticator, SfdLogger, and PortResolver.
    /// </summary>
    private static void GenerateSfdGlobalProgramCs(
        StringBuilder sb,
        TranslationOptions options,
        ApiSpecification? apiSpec,
        DetectedDependencies deps)
    {
        var serviceName = $"{options.BaseNamespace} Web Service";

        // Using statements
        sb.AppendLine("using SfD.Global;");
        sb.AppendLine("using SfD.Global.Auth;");
        sb.AppendLine("using SfD.Global.Config;");
        sb.AppendLine("using SfD.Global.Logging;");
        sb.AppendLine("using SfD.Global.Models;");
        sb.AppendLine();

        sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
        sb.AppendLine();

        // ConfigService initialization
        sb.AppendLine("// CRITICAL: Initialize ConfigService before anything else");
        sb.AppendLine($"ConfigService.SetAppType(AppType.{options.SfdGlobal.AppType});");
        sb.AppendLine("await ConfigService.InitializeAsync();");
        sb.AppendLine();

        // Service authentication
        sb.AppendLine("// Authenticate and get access token");
        sb.AppendLine("var accessToken = await ServiceAuthenticator.GetServiceAccessTokenAsync();");
        sb.AppendLine();

        // Database configuration from ConfigService
        sb.AppendLine("// Fetch Firebird database configuration from config service");
        sb.AppendLine("Console.WriteLine(\"\\nFetching Firebird database configuration...\");");
        sb.AppendLine($"var firebirdConfig = await ConfigService.GetConfigAsync<FBConnection>(\"{options.SfdGlobal.DatabaseConfigKey}\", accessToken)");
        sb.AppendLine("    ?? throw new InvalidOperationException(\"Failed to fetch Firebird database configuration\");");
        sb.AppendLine();

        // Port configuration via PortResolver
        sb.AppendLine("// CRITICAL: Use centralized port management via SfD.Global");
        sb.AppendLine("int port = PortResolver.GetPort();");
        sb.AppendLine("Console.WriteLine($\"\\nUsing port: {port}\");");
        sb.AppendLine();
        sb.AppendLine("builder.WebHost.ConfigureKestrel(options =>");
        sb.AppendLine("{");
        sb.AppendLine("    options.ListenAnyIP(port);");
        sb.AppendLine("});");
        sb.AppendLine();

        // Logging configuration with SfdLogger
        sb.AppendLine("// Configure logging with SfD.Global logger");
        sb.AppendLine("builder.Logging.ClearProviders();");
        sb.AppendLine("builder.Logging.AddConsole();");
        sb.AppendLine();
        sb.AppendLine("var sfdLoggerConfig = new SfdLoggerConfiguration");
        sb.AppendLine("{");
        sb.AppendLine("    LoggerServiceUrl = ConfigService.LoggerServiceUrl,");
        sb.AppendLine("    ClientId = ConfigService.ClientId,");
        sb.AppendLine("    Realm = ConfigService.Realm,");
        sb.AppendLine("    MinimumLogLevel = LogLevel.Information");
        sb.AppendLine("};");
        sb.AppendLine("builder.Logging.AddSfdLogger(sfdLoggerConfig);");
        sb.AppendLine();

        // Register SfdLogger for direct injection
        sb.AppendLine("// Register SfdLogger for direct injection if needed");
        sb.AppendLine("var sfdLogger = new SfdLogger(");
        sb.AppendLine($"    \"{serviceName}\",");
        sb.AppendLine("    sfdLoggerConfig.LoggerServiceUrl,");
        sb.AppendLine("    sfdLoggerConfig.ClientId,");
        sb.AppendLine("    sfdLoggerConfig.Realm");
        sb.AppendLine(");");
        sb.AppendLine("builder.Services.AddSingleton(sfdLogger);");
        sb.AppendLine();

        // Register configuration objects for DI
        sb.AppendLine("// Register configuration objects for DI");
        sb.AppendLine("builder.Services.AddSingleton(firebirdConfig);");
        sb.AppendLine();
        sb.AppendLine("builder.Services.AddSingleton<IConfigProvider>(new ConfigProvider");
        sb.AppendLine("{");
        sb.AppendLine("    ClientId = ConfigService.ClientId,");
        sb.AppendLine("    OpenIdConfig = ConfigService.OpenIdConfig,");
        sb.AppendLine("    LoggerService = ConfigService.LoggerServiceUrl,");
        sb.AppendLine("    Realm = ConfigService.Realm");
        sb.AppendLine("});");
        sb.AppendLine();

        // Database connection factory using fetched config
        sb.AppendLine("// Database connection factory using fetched config");
        sb.AppendLine("builder.Services.AddScoped<FbConnection>(sp =>");
        sb.AppendLine("{");
        sb.AppendLine("    var config = sp.GetRequiredService<FBConnection>();");
        sb.AppendLine("    var connectionString = $\"Server={config.Host};Port={config.Port};Database={config.Database};\" +");
        sb.AppendLine("                           $\"User={config.UserName};Password={config.Password};Charset={config.Charset}\";");
        sb.AppendLine("    return new FbConnection(connectionString);");
        sb.AppendLine("});");
        sb.AppendLine();

        // Register repositories
        if (apiSpec is not null && apiSpec.Repositories.Count > 0)
        {
            sb.AppendLine("// Register repositories");
            foreach (var repo in apiSpec.Repositories)
            {
                sb.AppendLine($"builder.Services.AddScoped<{repo.Name}>();");
            }
            sb.AppendLine();
        }

        // Register hosted services
        if (deps.HostedServices.Count > 0)
        {
            sb.AppendLine("// Register background/hosted services");
            foreach (var service in deps.HostedServices.OrderBy(s => s))
            {
                sb.AppendLine($"builder.Services.AddHostedService<{service}>();");
            }
            sb.AppendLine();
        }

        // Register HttpClient services
        if (deps.HttpClientServices.Count > 0)
        {
            sb.AppendLine("// Register HTTP clients");
            foreach (var service in deps.HttpClientServices.OrderBy(s => s))
            {
                sb.AppendLine($"builder.Services.AddHttpClient<{service}>();");
            }
            sb.AppendLine();
        }
        else if (deps.UsesHttpClient)
        {
            sb.AppendLine("// Register HTTP client factory");
            sb.AppendLine("builder.Services.AddHttpClient();");
            sb.AppendLine();
        }

        // Add services
        sb.AppendLine("// Add services");
        sb.AppendLine("builder.Services.AddControllers();");
        sb.AppendLine("builder.Services.AddEndpointsApiExplorer();");
        sb.AppendLine("builder.Services.AddSwaggerGen(c =>");
        sb.AppendLine("{");
        sb.AppendLine($"    c.SwaggerDoc(\"v1\", new() {{ Title = \"{serviceName}\", Version = \"v1\" }});");
        sb.AppendLine("});");
        sb.AppendLine();

        sb.AppendLine("var app = builder.Build();");
        sb.AppendLine();

        sb.AppendLine("// Configure the HTTP request pipeline");
        sb.AppendLine("if (app.Environment.IsDevelopment())");
        sb.AppendLine("{");
        sb.AppendLine("    app.UseSwagger();");
        sb.AppendLine("    app.UseSwaggerUI();");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("app.UseAuthorization();");
        sb.AppendLine("app.MapControllers();");
        sb.AppendLine();

        // Startup logging using SfdLogger
        sb.AppendLine("// Log startup information using SfdLogger");
        sb.AppendLine("sfdLogger.LogInformation(\"ConfigService initialized successfully\");");
        sb.AppendLine("sfdLogger.LogInformation($\"  ClientId: {ConfigService.ClientId}\");");
        sb.AppendLine("sfdLogger.LogInformation($\"  Realm: {ConfigService.Realm}\");");
        sb.AppendLine("sfdLogger.LogInformation($\"  OpenIdConfig: {ConfigService.OpenIdConfig}\");");
        sb.AppendLine("sfdLogger.LogInformation($\"  LoggerService: {ConfigService.LoggerServiceUrl}\");");
        sb.AppendLine();
        sb.AppendLine("sfdLogger.LogInformation(\"Firebird config loaded\");");
        sb.AppendLine("sfdLogger.LogInformation($\"  Host: {firebirdConfig.Host}\");");
        sb.AppendLine("sfdLogger.LogInformation($\"  Port: {firebirdConfig.Port}\");");
        sb.AppendLine("sfdLogger.LogInformation($\"  Database: {firebirdConfig.Database}\");");
        sb.AppendLine("sfdLogger.LogInformation($\"  UserName: {firebirdConfig.UserName}\");");
        sb.AppendLine("sfdLogger.LogInformation($\"  Charset: {firebirdConfig.Charset}\");");
        sb.AppendLine();
        sb.AppendLine("sfdLogger.LogInformation(\"=======================================================\");");
        sb.AppendLine($"sfdLogger.LogInformation(\"{serviceName} Started Successfully\");");
        sb.AppendLine("sfdLogger.LogInformation($\"Listening on: http://0.0.0.0:{port}\");");
        sb.AppendLine("sfdLogger.LogInformation(\"=======================================================\");");
        sb.AppendLine();
        sb.AppendLine("app.Run();");
    }

    /// <summary>
    /// Generates standard Program.cs without SfD.Global integration (existing behavior).
    /// </summary>
    private static void GenerateStandardProgramCs(
        StringBuilder sb,
        TranslationOptions options,
        ApiSpecification? apiSpec,
        DetectedDependencies deps)
    {
        sb.AppendLine($"using {options.BaseNamespace}.Classes;");
        sb.AppendLine("using FirebirdSql.Data.FirebirdClient;");
        sb.AppendLine();
        sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
        sb.AppendLine();
        sb.AppendLine("// Add services");
        sb.AppendLine("builder.Services.AddControllers();");
        sb.AppendLine("builder.Services.AddEndpointsApiExplorer();");
        sb.AppendLine("builder.Services.AddSwaggerGen();");
        sb.AppendLine();
        sb.AppendLine("// Database connection factory");
        sb.AppendLine("builder.Services.AddScoped<FbConnection>(sp =>");
        sb.AppendLine("{");
        sb.AppendLine("    var connectionString = builder.Configuration.GetConnectionString(\"Firebird\");");
        sb.AppendLine("    return new FbConnection(connectionString);");
        sb.AppendLine("});");
        sb.AppendLine();

        // Register repositories
        if (apiSpec is not null && apiSpec.Repositories.Count > 0)
        {
            sb.AppendLine("// Register repositories");
            foreach (var repo in apiSpec.Repositories)
            {
                sb.AppendLine($"builder.Services.AddScoped<{repo.Name}>();");
            }
            sb.AppendLine();
        }

        // Register hosted services
        if (deps.HostedServices.Count > 0)
        {
            sb.AppendLine("// Register background/hosted services");
            foreach (var service in deps.HostedServices.OrderBy(s => s))
            {
                sb.AppendLine($"builder.Services.AddHostedService<{service}>();");
            }
            sb.AppendLine();
        }

        // Register HttpClient services
        if (deps.HttpClientServices.Count > 0)
        {
            sb.AppendLine("// Register HTTP clients");
            foreach (var service in deps.HttpClientServices.OrderBy(s => s))
            {
                sb.AppendLine($"builder.Services.AddHttpClient<{service}>();");
            }
            sb.AppendLine();
        }
        else if (deps.UsesHttpClient)
        {
            sb.AppendLine("// Register HTTP client factory");
            sb.AppendLine("builder.Services.AddHttpClient();");
            sb.AppendLine();
        }

        sb.AppendLine("// CORS for React frontend");
        sb.AppendLine("builder.Services.AddCors(options =>");
        sb.AppendLine("{");
        sb.AppendLine("    options.AddDefaultPolicy(policy =>");
        sb.AppendLine("    {");
        sb.AppendLine("        policy.WithOrigins(\"http://localhost:5173\", \"http://localhost:3000\")");
        sb.AppendLine("              .AllowAnyMethod()");
        sb.AppendLine("              .AllowAnyHeader();");
        sb.AppendLine("    });");
        sb.AppendLine("});");
        sb.AppendLine();
        sb.AppendLine("var app = builder.Build();");
        sb.AppendLine();
        sb.AppendLine("if (app.Environment.IsDevelopment())");
        sb.AppendLine("{");
        sb.AppendLine("    app.UseSwagger();");
        sb.AppendLine("    app.UseSwaggerUI();");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("app.UseCors();");
        sb.AppendLine("app.UseAuthorization();");
        sb.AppendLine("app.MapControllers();");
        sb.AppendLine();
        sb.AppendLine("app.Run();");
    }

    private static async Task GenerateApiGlobalUsingsAsync(
        string projectFolder,
        TranslationOptions options,
        DetectedDependencies deps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("global using System;");
        sb.AppendLine("global using System.Collections.Generic;");
        sb.AppendLine("global using System.Data;");
        sb.AppendLine("global using System.IO;");
        sb.AppendLine("global using System.Linq;");
        sb.AppendLine("global using System.Threading;");
        sb.AppendLine("global using System.Threading.Tasks;");
        sb.AppendLine("global using Dapper;");
        sb.AppendLine("global using FirebirdSql.Data.FirebirdClient;");
        sb.AppendLine("global using Microsoft.AspNetCore.Mvc;");

        // Add hosting usings if hosted services detected or ServiceBase was converted
        if (deps.HostedServices.Count > 0 || deps.UsesServiceBase ||
            deps.NuGetPackages.Contains("Microsoft.Extensions.Hosting"))
        {
            sb.AppendLine("global using Microsoft.Extensions.Hosting;");
        }

        // Add HttpClient usings if needed
        if (deps.UsesHttpClient || deps.HttpClientServices.Count > 0)
        {
            sb.AppendLine("global using System.Net.Http;");
            sb.AppendLine("global using System.Net.Http.Json;");
        }

        // Always include Classes namespace (has BaseRepository, Gateway, etc.)
        sb.AppendLine($"global using {options.BaseNamespace}.Classes;");

        // Only include Dtos namespace if DTOs were actually generated
        if (deps.HasDtos)
        {
            sb.AppendLine($"global using {options.BaseNamespace}.{options.ApiOptions.DtoNamespace};");
        }

        // Only include Repositories namespace if repositories were generated
        if (deps.HasRepositories)
        {
            sb.AppendLine($"global using {options.BaseNamespace}.{options.ApiOptions.RepositoryNamespace};");
        }

        // Only include Controllers namespace if controllers were generated
        if (deps.HasControllers)
        {
            sb.AppendLine($"global using {options.BaseNamespace}.{options.ApiOptions.ControllerNamespace};");
        }

        var path = Path.Combine(projectFolder, "GlobalUsings.cs");
        await File.WriteAllTextAsync(path, sb.ToString());
    }

    private static async Task GenerateAppSettingsAsync(string projectFolder)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"ConnectionStrings\": {");
        sb.AppendLine("    \"Firebird\": \"Server=localhost;Database=database.fdb;User=SYSDBA;Password=masterkey\"");
        sb.AppendLine("  },");
        sb.AppendLine("  \"Logging\": {");
        sb.AppendLine("    \"LogLevel\": {");
        sb.AppendLine("      \"Default\": \"Information\",");
        sb.AppendLine("      \"Microsoft.AspNetCore\": \"Warning\"");
        sb.AppendLine("    }");
        sb.AppendLine("  },");
        sb.AppendLine("  \"AllowedHosts\": \"*\"");
        sb.AppendLine("}");

        var path = Path.Combine(projectFolder, "appsettings.json");
        await File.WriteAllTextAsync(path, sb.ToString());
    }

    private static async Task GeneratePackageJsonAsync(string projectFolder, string projectName, TranslationOptions options)
    {
        var useSfdWebCommon = options.ApiOptions.SfdWebCommon.UseSfdWebCommon;

        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"name\": \"{projectName.ToLowerInvariant()}-web\",");
        sb.AppendLine("  \"private\": true,");
        sb.AppendLine("  \"version\": \"0.0.1\",");
        sb.AppendLine("  \"type\": \"module\",");
        sb.AppendLine("  \"scripts\": {");
        sb.AppendLine("    \"dev\": \"vite\",");

        if (useSfdWebCommon)
        {
            // JavaScript build (no TypeScript)
            sb.AppendLine("    \"build\": \"vite build\",");
            sb.AppendLine("    \"lint\": \"eslint . --ext js,jsx --report-unused-disable-directives --max-warnings 0\",");
        }
        else
        {
            // TypeScript build
            sb.AppendLine("    \"build\": \"tsc && vite build\",");
            sb.AppendLine("    \"lint\": \"eslint . --ext ts,tsx --report-unused-disable-directives --max-warnings 0\",");
        }

        sb.AppendLine("    \"preview\": \"vite preview\"");
        sb.AppendLine("  },");
        sb.AppendLine("  \"dependencies\": {");
        sb.AppendLine("    \"react\": \"^18.2.0\",");
        sb.AppendLine("    \"react-dom\": \"^18.2.0\",");

        if (useSfdWebCommon)
        {
            sb.AppendLine("    \"@sfd/web-common\": \"*\"");
        }
        else
        {
            sb.AppendLine("    \"react-router-dom\": \"^6.20.0\"");
        }

        sb.AppendLine("  },");
        sb.AppendLine("  \"devDependencies\": {");
        sb.AppendLine("    \"@types/react\": \"^18.2.37\",");
        sb.AppendLine("    \"@types/react-dom\": \"^18.2.15\",");

        if (!useSfdWebCommon)
        {
            sb.AppendLine("    \"@typescript-eslint/eslint-plugin\": \"^6.10.0\",");
            sb.AppendLine("    \"@typescript-eslint/parser\": \"^6.10.0\",");
        }

        sb.AppendLine("    \"@vitejs/plugin-react\": \"^4.2.0\",");
        sb.AppendLine("    \"eslint\": \"^8.53.0\",");
        sb.AppendLine("    \"eslint-plugin-react-hooks\": \"^4.6.0\",");
        sb.AppendLine("    \"eslint-plugin-react-refresh\": \"^0.4.4\",");

        if (!useSfdWebCommon)
        {
            sb.AppendLine("    \"typescript\": \"^5.2.2\",");
        }

        sb.AppendLine("    \"vite\": \"^5.0.0\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        var path = Path.Combine(projectFolder, "package.json");
        await File.WriteAllTextAsync(path, sb.ToString());
    }

    private static async Task GenerateTsConfigAsync(string projectFolder)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"compilerOptions\": {");
        sb.AppendLine("    \"target\": \"ES2020\",");
        sb.AppendLine("    \"useDefineForClassFields\": true,");
        sb.AppendLine("    \"lib\": [\"ES2020\", \"DOM\", \"DOM.Iterable\"],");
        sb.AppendLine("    \"module\": \"ESNext\",");
        sb.AppendLine("    \"skipLibCheck\": true,");
        sb.AppendLine("    \"moduleResolution\": \"bundler\",");
        sb.AppendLine("    \"allowImportingTsExtensions\": true,");
        sb.AppendLine("    \"resolveJsonModule\": true,");
        sb.AppendLine("    \"isolatedModules\": true,");
        sb.AppendLine("    \"noEmit\": true,");
        sb.AppendLine("    \"jsx\": \"react-jsx\",");
        sb.AppendLine("    \"strict\": true,");
        sb.AppendLine("    \"noUnusedLocals\": true,");
        sb.AppendLine("    \"noUnusedParameters\": true,");
        sb.AppendLine("    \"noFallthroughCasesInSwitch\": true,");
        sb.AppendLine("    \"baseUrl\": \".\",");
        sb.AppendLine("    \"paths\": {");
        sb.AppendLine("      \"@/*\": [\"src/*\"]");
        sb.AppendLine("    }");
        sb.AppendLine("  },");
        sb.AppendLine("  \"include\": [\"src\"],");
        sb.AppendLine("  \"references\": [{ \"path\": \"./tsconfig.node.json\" }]");
        sb.AppendLine("}");

        var path = Path.Combine(projectFolder, "tsconfig.json");
        await File.WriteAllTextAsync(path, sb.ToString());

        // Also create tsconfig.node.json
        var nodeSb = new StringBuilder();
        nodeSb.AppendLine("{");
        nodeSb.AppendLine("  \"compilerOptions\": {");
        nodeSb.AppendLine("    \"composite\": true,");
        nodeSb.AppendLine("    \"skipLibCheck\": true,");
        nodeSb.AppendLine("    \"module\": \"ESNext\",");
        nodeSb.AppendLine("    \"moduleResolution\": \"bundler\",");
        nodeSb.AppendLine("    \"allowSyntheticDefaultImports\": true");
        nodeSb.AppendLine("  },");
        nodeSb.AppendLine("  \"include\": [\"vite.config.ts\"]");
        nodeSb.AppendLine("}");

        var nodePath = Path.Combine(projectFolder, "tsconfig.node.json");
        await File.WriteAllTextAsync(nodePath, nodeSb.ToString());
    }

    private static async Task GenerateViteConfigAsync(string projectFolder, TranslationOptions options)
    {
        var useSfdWebCommon = options.ApiOptions.SfdWebCommon.UseSfdWebCommon;

        var sb = new StringBuilder();
        sb.AppendLine("import { defineConfig } from 'vite'");
        sb.AppendLine("import react from '@vitejs/plugin-react'");

        if (!useSfdWebCommon)
        {
            sb.AppendLine("import path from 'path'");
        }

        sb.AppendLine();
        sb.AppendLine("export default defineConfig({");
        sb.AppendLine("  plugins: [react()],");

        if (!useSfdWebCommon)
        {
            sb.AppendLine("  resolve: {");
            sb.AppendLine("    alias: {");
            sb.AppendLine("      '@': path.resolve(__dirname, './src'),");
            sb.AppendLine("    },");
            sb.AppendLine("  },");
        }

        sb.AppendLine("  server: {");

        if (!useSfdWebCommon)
        {
            sb.AppendLine("    proxy: {");
            sb.AppendLine("      '/api': {");
            sb.AppendLine("        target: 'http://localhost:5000',");
            sb.AppendLine("        changeOrigin: true,");
            sb.AppendLine("      },");
            sb.AppendLine("    },");
        }
        else
        {
            sb.AppendLine("    port: 5173,");
        }

        sb.AppendLine("  },");
        sb.AppendLine("})");

        var extension = useSfdWebCommon ? "js" : "ts";
        var path = Path.Combine(projectFolder, $"vite.config.{extension}");
        await File.WriteAllTextAsync(path, sb.ToString());
    }

    private static async Task GenerateApiServiceAsync(string folder, TranslationOptions options, ApiSpecification? apiSpec)
    {
        var sb = new StringBuilder();
        sb.AppendLine("const API_BASE_URL = import.meta.env.VITE_API_URL || '';");
        sb.AppendLine();
        sb.AppendLine("async function handleResponse<T>(response: Response): Promise<T> {");
        sb.AppendLine("  if (!response.ok) {");
        sb.AppendLine("    const error = await response.text();");
        sb.AppendLine("    throw new Error(error || `HTTP error! status: ${response.status}`);");
        sb.AppendLine("  }");
        sb.AppendLine("  return response.json();");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("async function getRequest<T>(url: string, params?: Record<string, any>): Promise<T> {");
        sb.AppendLine("  const queryString = params ? '?' + new URLSearchParams(params).toString() : '';");
        sb.AppendLine("  const response = await fetch(`${API_BASE_URL}${url}${queryString}`);");
        sb.AppendLine("  return handleResponse<T>(response);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("async function postRequest<T>(url: string, data?: any): Promise<T> {");
        sb.AppendLine("  const response = await fetch(`${API_BASE_URL}${url}`, {");
        sb.AppendLine("    method: 'POST',");
        sb.AppendLine("    headers: { 'Content-Type': 'application/json' },");
        sb.AppendLine("    body: data ? JSON.stringify(data) : undefined,");
        sb.AppendLine("  });");
        sb.AppendLine("  return handleResponse<T>(response);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("async function putRequest<T>(url: string, data?: any): Promise<T> {");
        sb.AppendLine("  const response = await fetch(`${API_BASE_URL}${url}`, {");
        sb.AppendLine("    method: 'PUT',");
        sb.AppendLine("    headers: { 'Content-Type': 'application/json' },");
        sb.AppendLine("    body: data ? JSON.stringify(data) : undefined,");
        sb.AppendLine("  });");
        sb.AppendLine("  return handleResponse<T>(response);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("async function deleteRequest<T>(url: string): Promise<T> {");
        sb.AppendLine("  const response = await fetch(`${API_BASE_URL}${url}`, {");
        sb.AppendLine("    method: 'DELETE',");
        sb.AppendLine("  });");
        sb.AppendLine("  return handleResponse<T>(response);");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("export const api = {");

        // Generate endpoint methods from apiSpec
        if (apiSpec is not null)
        {
            foreach (var controller in apiSpec.Controllers)
            {
                foreach (var action in controller.Actions)
                {
                    var methodName = ToCamelCase(action.Name);
                    var httpMethod = action.HttpMethod.ToLowerInvariant();
                    var route = $"/{options.ApiOptions.ApiRoutePrefix}/{controller.Route}/{action.Route}".Replace("//", "/");
                    sb.AppendLine($"  {methodName}: (params?: any) => {httpMethod}Request<any>('{route}', params),");
                }
            }
        }

        sb.AppendLine("};");
        sb.AppendLine();
        sb.AppendLine("export default api;");

        var path = Path.Combine(folder, "api.ts");
        await File.WriteAllTextAsync(path, sb.ToString());
    }

    private static async Task GenerateIndexHtmlAsync(string projectFolder, string projectName, TranslationOptions options)
    {
        var useSfdWebCommon = options.ApiOptions.SfdWebCommon.UseSfdWebCommon;
        var extension = useSfdWebCommon ? "jsx" : "tsx";

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("  <head>");
        sb.AppendLine("    <meta charset=\"UTF-8\" />");
        sb.AppendLine("    <link rel=\"icon\" type=\"image/svg+xml\" href=\"/vite.svg\" />");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />");
        sb.AppendLine($"    <title>{projectName}</title>");
        sb.AppendLine("  </head>");
        sb.AppendLine("  <body>");
        sb.AppendLine("    <div id=\"root\"></div>");
        sb.AppendLine($"    <script type=\"module\" src=\"/src/main.{extension}\"></script>");
        sb.AppendLine("  </body>");
        sb.AppendLine("</html>");

        var path = Path.Combine(projectFolder, "index.html");
        await File.WriteAllTextAsync(path, sb.ToString());
    }

    private static async Task GenerateMainTsxAsync(string srcFolder, TranslationOptions options)
    {
        var sb = new StringBuilder();
        var useSfdWebCommon = options.ApiOptions.SfdWebCommon.UseSfdWebCommon;

        if (useSfdWebCommon)
        {
            // Generate @sfd/web-common integrated main.jsx
            sb.AppendLine("import { StrictMode } from 'react';");
            sb.AppendLine("import ReactDOM from 'react-dom/client';");
            sb.AppendLine("import App from './App';");
            sb.AppendLine("import './index.css';");
            sb.AppendLine("import { AppInitializer } from '@sfd/web-common';");
            sb.AppendLine();
            sb.AppendLine("ReactDOM.createRoot(document.getElementById('root')).render(");
            sb.AppendLine("  <StrictMode>");
            sb.AppendLine($"    <AppInitializer appType=\"{options.ApiOptions.SfdWebCommon.AppType}\">");
            sb.AppendLine("      <App />");
            sb.AppendLine("    </AppInitializer>");
            sb.AppendLine("  </StrictMode>");
            sb.AppendLine(");");
        }
        else
        {
            // Generate standard main.tsx (existing behavior)
            sb.AppendLine("import React from 'react'");
            sb.AppendLine("import ReactDOM from 'react-dom/client'");
            sb.AppendLine("import { BrowserRouter } from 'react-router-dom'");
            sb.AppendLine("import App from './App'");
            sb.AppendLine("import './index.css'");
            sb.AppendLine();
            sb.AppendLine("ReactDOM.createRoot(document.getElementById('root')!).render(");
            sb.AppendLine("  <React.StrictMode>");
            sb.AppendLine("    <BrowserRouter>");
            sb.AppendLine("      <App />");
            sb.AppendLine("    </BrowserRouter>");
            sb.AppendLine("  </React.StrictMode>,");
            sb.AppendLine(")");
        }

        // Use .jsx extension when using @sfd/web-common (JavaScript), .tsx otherwise (TypeScript)
        var extension = useSfdWebCommon ? "jsx" : "tsx";
        var path = Path.Combine(srcFolder, $"main.{extension}");
        await File.WriteAllTextAsync(path, sb.ToString());

        // Also create index.css
        var cssSb = new StringBuilder();
        cssSb.AppendLine(":root {");
        cssSb.AppendLine("  font-family: Inter, system-ui, Avenir, Helvetica, Arial, sans-serif;");
        cssSb.AppendLine("  line-height: 1.5;");
        cssSb.AppendLine("  font-weight: 400;");
        cssSb.AppendLine("}");
        cssSb.AppendLine();
        cssSb.AppendLine("* {");
        cssSb.AppendLine("  box-sizing: border-box;");
        cssSb.AppendLine("  margin: 0;");
        cssSb.AppendLine("  padding: 0;");
        cssSb.AppendLine("}");
        cssSb.AppendLine();
        cssSb.AppendLine("body {");
        cssSb.AppendLine("  min-height: 100vh;");
        cssSb.AppendLine("}");

        var cssPath = Path.Combine(srcFolder, "index.css");
        await File.WriteAllTextAsync(cssPath, cssSb.ToString());
    }

    private static async Task GenerateAppTsxAsync(string srcFolder, List<ReactComponentDefinition>? components, TranslationOptions options)
    {
        var sb = new StringBuilder();
        var useSfdWebCommon = options.ApiOptions.SfdWebCommon.UseSfdWebCommon;
        var requiresAuth = options.ApiOptions.SfdWebCommon.RequiresAuth;

        if (useSfdWebCommon)
        {
            // Generate @sfd/web-common integrated App.jsx with AuthProvider
            sb.AppendLine("import './App.css';");

            // Import page components
            if (components is not null)
            {
                var pages = components.Where(c => c.ComponentType == ComponentType.Page).ToList();
                foreach (var page in pages)
                {
                    sb.AppendLine($"import {page.Name} from './pages/{page.Name}';");
                }
            }

            if (requiresAuth)
            {
                sb.AppendLine("import { AuthProvider, ProtectedRoute, Callback } from '@sfd/web-common/auth';");
            }
            sb.AppendLine();

            sb.AppendLine("function App() {");
            sb.AppendLine("  const basePath = import.meta.env.BASE_URL.replace(/\\/$/, '');");

            if (requiresAuth)
            {
                sb.AppendLine("  const isCallback = window.location.pathname === `${basePath}/callback`;");
                sb.AppendLine();
                sb.AppendLine("  const authConfig = {");
                sb.AppendLine("    redirectUri: window.location.origin + basePath + '/callback',");
                sb.AppendLine("    postLogoutRedirectUri: window.location.origin + basePath + '/',");
                sb.AppendLine($"    scope: '{options.ApiOptions.SfdWebCommon.AuthScopes}',");
                sb.AppendLine("    automaticSilentRenew: true,");
                sb.AppendLine("  };");
                sb.AppendLine();
                sb.AppendLine("  return (");
                sb.AppendLine("    <AuthProvider config={authConfig}>");
                sb.AppendLine("      {isCallback ? (");
                sb.AppendLine("        <Callback redirectUrl={basePath + '/'} />");
                sb.AppendLine("      ) : (");
                sb.AppendLine("        <ProtectedRoute>");

                // Add main content - either a single page or routing
                if (components is not null && components.Count > 0)
                {
                    var mainPage = components.FirstOrDefault(c => c.ComponentType == ComponentType.Page);
                    if (mainPage is not null)
                    {
                        sb.AppendLine($"          <{mainPage.Name} />");
                    }
                    else
                    {
                        sb.AppendLine("          <div>Welcome</div>");
                    }
                }
                else
                {
                    sb.AppendLine("          <div>Welcome</div>");
                }

                sb.AppendLine("        </ProtectedRoute>");
                sb.AppendLine("      )}");
                sb.AppendLine("    </AuthProvider>");
                sb.AppendLine("  );");
            }
            else
            {
                // No auth required
                sb.AppendLine();
                sb.AppendLine("  return (");
                sb.AppendLine("    <div className=\"app\">");

                if (components is not null && components.Count > 0)
                {
                    var mainPage = components.FirstOrDefault(c => c.ComponentType == ComponentType.Page);
                    if (mainPage is not null)
                    {
                        sb.AppendLine($"      <{mainPage.Name} />");
                    }
                    else
                    {
                        sb.AppendLine("      <div>Welcome</div>");
                    }
                }
                else
                {
                    sb.AppendLine("      <div>Welcome</div>");
                }

                sb.AppendLine("    </div>");
                sb.AppendLine("  );");
            }

            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("export default App;");
        }
        else
        {
            // Generate standard App.tsx (existing behavior)
            sb.AppendLine("import { Routes, Route } from 'react-router-dom'");

            if (components is not null)
            {
                var pages = components.Where(c => c.ComponentType == ComponentType.Page).ToList();
                foreach (var page in pages)
                {
                    sb.AppendLine($"import {page.Name} from './pages/{page.Name}'");
                }
            }

            sb.AppendLine();
            sb.AppendLine("function App() {");
            sb.AppendLine("  return (");
            sb.AppendLine("    <div className=\"app\">");
            sb.AppendLine("      <Routes>");
            sb.AppendLine("        <Route path=\"/\" element={<div>Welcome</div>} />");

            if (components is not null)
            {
                var pages = components.Where(c => c.ComponentType == ComponentType.Page).ToList();
                foreach (var page in pages)
                {
                    var route = page.Name.Replace("Page", "").ToLowerInvariant();
                    sb.AppendLine($"        <Route path=\"/{route}\" element={{<{page.Name} />}} />");
                }
            }

            sb.AppendLine("      </Routes>");
            sb.AppendLine("    </div>");
            sb.AppendLine("  )");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("export default App");
        }

        // Use .jsx extension when using @sfd/web-common (JavaScript), .tsx otherwise (TypeScript)
        var extension = useSfdWebCommon ? "jsx" : "tsx";
        var path = Path.Combine(srcFolder, $"App.{extension}");
        await File.WriteAllTextAsync(path, sb.ToString());

        // Create App.css
        var cssSb = new StringBuilder();
        cssSb.AppendLine(".app {");
        cssSb.AppendLine("  min-height: 100vh;");
        cssSb.AppendLine("}");

        var cssPath = Path.Combine(srcFolder, "App.css");
        await File.WriteAllTextAsync(cssPath, cssSb.ToString());
    }

    private static async Task GenerateTypeScriptTypesAsync(string folder, List<DtoDefinition> dtos)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Auto-generated TypeScript types from C# DTOs");
        sb.AppendLine();

        foreach (var dto in dtos)
        {
            sb.AppendLine($"export interface {dto.Name} {{");
            foreach (var prop in dto.Properties)
            {
                var tsType = MapCSharpTypeToTypeScript(prop.Type);
                var propName = ToCamelCase(prop.Name);
                sb.AppendLine($"  {propName}{(prop.IsNullable ? "?" : "")}: {tsType};");
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }

        var path = Path.Combine(folder, "index.ts");
        await File.WriteAllTextAsync(path, sb.ToString());
    }

    private static string MapCSharpTypeToTypeScript(string csharpType)
    {
        return csharpType.ToLowerInvariant() switch
        {
            "string" => "string",
            "int" or "int32" or "int64" or "long" or "short" or "byte" => "number",
            "decimal" or "double" or "float" => "number",
            "bool" or "boolean" => "boolean",
            "datetime" or "datetimeoffset" => "string", // ISO format
            "guid" => "string",
            _ when csharpType.StartsWith("List<") => $"{MapCSharpTypeToTypeScript(csharpType[5..^1])}[]",
            _ when csharpType.EndsWith("[]") => $"{MapCSharpTypeToTypeScript(csharpType[..^2])}[]",
            _ => "any"
        };
    }

    private static async Task GenerateSolutionFileAsync(string projectName, string outputPath, bool includeReact)
    {
        var solutionGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
        var apiProjectGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();

        var sb = new StringBuilder();
        sb.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        sb.AppendLine("# Visual Studio Version 17");
        sb.AppendLine("VisualStudioVersion = 17.0.31903.59");
        sb.AppendLine("MinimumVisualStudioVersion = 10.0.40219.1");
        sb.AppendLine($"Project(\"{solutionGuid}\") = \"{projectName}.Api\", \"{projectName}.Api\\{projectName}.Api.csproj\", \"{apiProjectGuid}\"");
        sb.AppendLine("EndProject");

        // Add React project reference as solution folder if included
        if (includeReact)
        {
            var webFolderGuid = Guid.NewGuid().ToString("B").ToUpperInvariant();
            sb.AppendLine($"Project(\"{{2150E333-8FDC-42A3-9474-1A3956D46DE8}}\") = \"{projectName}.Web\", \"{projectName}.Web\", \"{webFolderGuid}\"");
            sb.AppendLine("EndProject");
        }

        sb.AppendLine("Global");
        sb.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        sb.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
        sb.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");
        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");
        sb.AppendLine($"\t\t{apiProjectGuid}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
        sb.AppendLine($"\t\t{apiProjectGuid}.Debug|Any CPU.Build.0 = Debug|Any CPU");
        sb.AppendLine($"\t\t{apiProjectGuid}.Release|Any CPU.ActiveCfg = Release|Any CPU");
        sb.AppendLine($"\t\t{apiProjectGuid}.Release|Any CPU.Build.0 = Release|Any CPU");
        sb.AppendLine("\tEndGlobalSection");
        sb.AppendLine("EndGlobal");

        var path = Path.Combine(outputPath, $"{projectName}.sln");
        await File.WriteAllTextAsync(path, sb.ToString());
    }

    private static async Task GenerateReadmeAsync(DelphiProject project, ProjectTranslationSummary summary, string projectFolder, TranslationOptions options)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {project.Name}");
        sb.AppendLine();
        sb.AppendLine($"Translated from Delphi to C# on {summary.CompletedAt:yyyy-MM-dd HH:mm:ss UTC}");
        sb.AppendLine();
        sb.AppendLine("## Architecture");
        sb.AppendLine();
        sb.AppendLine("This translation generates:");
        sb.AppendLine($"- **{project.Name}.Api**: ASP.NET Core Web API with Controllers and Repositories");
        if (options.UITarget == UITargetFramework.React)
        {
            sb.AppendLine($"- **{project.Name}.Web**: React TypeScript frontend application");
        }
        sb.AppendLine();
        sb.AppendLine("## Translation Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Total Units:** {summary.TotalUnits}");
        sb.AppendLine($"- **Successful:** {summary.SuccessfulTranslations}");
        sb.AppendLine($"- **Failed:** {summary.FailedTranslations}");
        sb.AppendLine($"- **Skipped:** {summary.SkippedUnits}");
        sb.AppendLine();
        sb.AppendLine("## Database Access Pattern");
        sb.AppendLine();
        sb.AppendLine("Database operations use the Repository pattern with Dapper and Firebird:");
        sb.AppendLine("- Repositories inherit from `BaseRepository`");
        sb.AppendLine("- Transaction support via `FbTransaction` parameter");
        sb.AppendLine("- Async methods throughout");
        sb.AppendLine();
        sb.AppendLine("## API Endpoints");
        sb.AppendLine();
        sb.AppendLine("Run the API project and navigate to `/swagger` to see all available endpoints.");
        sb.AppendLine();
        sb.AppendLine("## Build Instructions");
        sb.AppendLine();
        sb.AppendLine("### API Project");
        sb.AppendLine("```bash");
        sb.AppendLine($"cd {project.Name}.Api");
        sb.AppendLine("dotnet restore");
        sb.AppendLine("dotnet build");
        sb.AppendLine("dotnet run");
        sb.AppendLine("```");

        if (options.UITarget == UITargetFramework.React)
        {
            sb.AppendLine();
            sb.AppendLine("### React Frontend");
            sb.AppendLine("```bash");
            sb.AppendLine($"cd {project.Name}.Web");
            sb.AppendLine("npm install");
            sb.AppendLine("npm run dev");
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("## Configuration");
        sb.AppendLine();
        sb.AppendLine("Update `appsettings.json` with your Firebird connection string:");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine("  \"ConnectionStrings\": {");
        sb.AppendLine("    \"Firebird\": \"Server=localhost;Database=your.fdb;User=SYSDBA;Password=masterkey\"");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("## Notes");
        sb.AppendLine();
        sb.AppendLine("This code was automatically translated and may require manual review:");
        sb.AppendLine("- Verify SQL queries work with your database schema");
        sb.AppendLine("- Check transaction boundaries are correct");
        sb.AppendLine("- Test all API endpoints");
        sb.AppendLine("- Review React component state management");

        var path = Path.Combine(projectFolder, "README.md");
        await File.WriteAllTextAsync(path, sb.ToString());
    }

    private static async Task CreateZipAsync(string outputPath, string zipPath, string projectName)
    {
        // Delete existing zip if present
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        // Create a temp directory to organize files
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            // Copy all project folders to temp
            var apiFolder = Path.Combine(outputPath, $"{projectName}.Api");
            var webFolder = Path.Combine(outputPath, $"{projectName}.Web");
            var scriptsFolder = Path.Combine(outputPath, "scripts");

            if (Directory.Exists(apiFolder))
                CopyDirectory(apiFolder, Path.Combine(tempDir, $"{projectName}.Api"));

            if (Directory.Exists(webFolder))
                CopyDirectory(webFolder, Path.Combine(tempDir, $"{projectName}.Web"));

            if (Directory.Exists(scriptsFolder))
                CopyDirectory(scriptsFolder, Path.Combine(tempDir, "scripts"));

            // Copy solution file
            var slnFile = Path.Combine(outputPath, $"{projectName}.sln");
            if (File.Exists(slnFile))
                File.Copy(slnFile, Path.Combine(tempDir, $"{projectName}.sln"));

            ZipFile.CreateFromDirectory(tempDir, zipPath, CompressionLevel.Optimal, false);
        }
        finally
        {
            // Cleanup temp directory
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            File.Copy(file, destFile, true);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            CopyDirectory(dir, destSubDir);
        }
    }

    private static async Task GeneratePowerShellScriptAsync(string projectName, TranslationOptions options, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Deploy script for {projectName}");
        sb.AppendLine("# Generated by DelphiAnalysisMcpServer");
        sb.AppendLine();
        sb.AppendLine("param(");
        sb.AppendLine("    [string]$Configuration = \"Release\",");
        sb.AppendLine("    [string]$OutputPath = \"./publish\"");
        sb.AppendLine(")");
        sb.AppendLine();
        sb.AppendLine("$ErrorActionPreference = \"Stop\"");
        sb.AppendLine();
        sb.AppendLine($"Write-Host \"Building {projectName}.Api...\" -ForegroundColor Cyan");
        sb.AppendLine();
        sb.AppendLine("# Build API");
        sb.AppendLine($"Push-Location \"./{projectName}.Api\"");
        sb.AppendLine("dotnet restore");
        sb.AppendLine("if ($LASTEXITCODE -ne 0) { throw \"Restore failed\" }");
        sb.AppendLine();
        sb.AppendLine("dotnet build -c $Configuration --no-restore");
        sb.AppendLine("if ($LASTEXITCODE -ne 0) { throw \"Build failed\" }");
        sb.AppendLine();
        sb.AppendLine("dotnet publish -c $Configuration -o \"$OutputPath/api\" --no-build");
        sb.AppendLine("if ($LASTEXITCODE -ne 0) { throw \"Publish failed\" }");
        sb.AppendLine("Pop-Location");

        if (options.UITarget == UITargetFramework.React)
        {
            sb.AppendLine();
            sb.AppendLine("# Build React frontend");
            sb.AppendLine($"Write-Host \"Building {projectName}.Web...\" -ForegroundColor Cyan");
            sb.AppendLine($"Push-Location \"./{projectName}.Web\"");
            sb.AppendLine("npm ci");
            sb.AppendLine("npm run build");
            sb.AppendLine("Copy-Item -Path \"./dist/*\" -Destination \"$OutputPath/web\" -Recurse -Force");
            sb.AppendLine("Pop-Location");
        }

        sb.AppendLine();
        sb.AppendLine("Write-Host \"Deployment complete!\" -ForegroundColor Green");
        sb.AppendLine("Write-Host \"API: $OutputPath/api\" -ForegroundColor Yellow");
        if (options.UITarget == UITargetFramework.React)
        {
            sb.AppendLine("Write-Host \"Web: $OutputPath/web\" -ForegroundColor Yellow");
        }

        await File.WriteAllTextAsync(path, sb.ToString());
    }

    private static async Task GenerateBashScriptAsync(string projectName, TranslationOptions options, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/bash");
        sb.AppendLine($"# Deploy script for {projectName}");
        sb.AppendLine("# Generated by DelphiAnalysisMcpServer");
        sb.AppendLine();
        sb.AppendLine("set -e");
        sb.AppendLine();
        sb.AppendLine("CONFIGURATION=\"${1:-Release}\"");
        sb.AppendLine("OUTPUT_PATH=\"${2:-./publish}\"");
        sb.AppendLine();
        sb.AppendLine($"echo -e \"\\033[36mBuilding {projectName}.Api...\\033[0m\"");
        sb.AppendLine();
        sb.AppendLine("# Build API");
        sb.AppendLine($"pushd \"./{projectName}.Api\"");
        sb.AppendLine("dotnet restore");
        sb.AppendLine("dotnet build -c \"$CONFIGURATION\" --no-restore");
        sb.AppendLine("dotnet publish -c \"$CONFIGURATION\" -o \"$OUTPUT_PATH/api\" --no-build");
        sb.AppendLine("popd");

        if (options.UITarget == UITargetFramework.React)
        {
            sb.AppendLine();
            sb.AppendLine("# Build React frontend");
            sb.AppendLine($"echo -e \"\\033[36mBuilding {projectName}.Web...\\033[0m\"");
            sb.AppendLine($"pushd \"./{projectName}.Web\"");
            sb.AppendLine("npm ci");
            sb.AppendLine("npm run build");
            sb.AppendLine("mkdir -p \"$OUTPUT_PATH/web\"");
            sb.AppendLine("cp -r ./dist/* \"$OUTPUT_PATH/web/\"");
            sb.AppendLine("popd");
        }

        sb.AppendLine();
        sb.AppendLine("echo -e \"\\033[32mDeployment complete!\\033[0m\"");
        sb.AppendLine("echo -e \"\\033[33mAPI: $OUTPUT_PATH/api\\033[0m\"");
        if (options.UITarget == UITargetFramework.React)
        {
            sb.AppendLine("echo -e \"\\033[33mWeb: $OUTPUT_PATH/web\\033[0m\"");
        }

        await File.WriteAllTextAsync(path, sb.ToString());
    }

    private static string ToCamelCase(string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        if (str.Length == 1) return str.ToLowerInvariant();
        return char.ToLowerInvariant(str[0]) + str[1..];
    }
}

public class OutputResult
{
    public string ProjectName { get; set; } = string.Empty;
    public string? FolderPath { get; set; }
    public string? ReactProjectPath { get; set; }
    public string? ZipPath { get; set; }
    public string? PowerShellScriptPath { get; set; }
    public string? BashScriptPath { get; set; }
}

/// <summary>
/// String extension methods for case conversion.
/// </summary>
public static class StringExtensions
{
    public static string ToCamelCase(this string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        if (str.Length == 1) return str.ToLowerInvariant();
        return char.ToLowerInvariant(str[0]) + str[1..];
    }

    public static string ToPascalCase(this string str)
    {
        if (string.IsNullOrEmpty(str)) return str;
        if (str.Length == 1) return str.ToUpperInvariant();
        return char.ToUpperInvariant(str[0]) + str[1..];
    }
}