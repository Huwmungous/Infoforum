namespace DelphiAnalysisMcpServer.Models;

/// <summary>
/// Configuration for how translations should be performed.
/// </summary>
public class TranslationOptions
{
    public string TargetFramework { get; set; } = "net10.0";
    public bool UseFileScopedNamespaces { get; set; } = true;
    public bool UseNullableReferenceTypes { get; set; } = true;
    public bool UsePrimaryConstructors { get; set; } = true;
    public bool UseRecordsForSimpleTypes { get; set; } = true;
    public bool GenerateAsyncMethods { get; set; } = true;
    public string BaseNamespace { get; set; } = "TranslatedApp";
    public UITargetFramework UITarget { get; set; } = UITargetFramework.Blazor;
    public bool PreserveComments { get; set; } = true;
    public bool GenerateXmlDocumentation { get; set; } = true;
    public string OllamaModel { get; set; } = "qwen2.5-coder:32b";
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";
    public ApiGenerationOptions ApiOptions { get; set; } = new();

    /// <summary>
    /// SfD.Global library integration options.
    /// </summary>
    public SfdGlobalOptions SfdGlobal { get; set; } = new();
}

/// <summary>
/// Options for SfD.Global library integration.
/// </summary>
public class SfdGlobalOptions
{
    /// <summary>
    /// Whether to use SfD.Global for configuration, authentication, and logging.
    /// When enabled, generated services will use ConfigService, ServiceAuthenticator,
    /// SfdLogger, and PortResolver from SfD.Global.
    /// </summary>
    public bool UseSfdGlobal { get; set; } = true;

    /// <summary>
    /// Path to the SfD.Global project for project reference.
    /// Example: "../SfD.Global/SfD.Global.csproj"
    /// </summary>
    public string? SfdGlobalProjectPath { get; set; }

    /// <summary>
    /// Path to the SfDApi.Classes project containing BaseRepository.
    /// Example: "../SfDApi.Classes/SfDApi.Classes.csproj"
    /// Generated repositories will extend BaseRepository from this project.
    /// </summary>
    public string? SfDApiClassesProjectPath { get; set; }

    /// <summary>
    /// The application type for ConfigService (Service, User, etc.)
    /// </summary>
    public string AppType { get; set; } = "Service";

    /// <summary>
    /// Whether to fetch database configuration from ConfigService.
    /// </summary>
    public bool FetchDatabaseConfig { get; set; } = true;

    /// <summary>
    /// The config key for database configuration (e.g., "firebirddb").
    /// </summary>
    public string DatabaseConfigKey { get; set; } = "firebirddb";
}

/// <summary>
/// Options for @sfd/web-common React library integration.
/// </summary>
public class SfdWebCommonOptions
{
    /// <summary>
    /// Whether to use @sfd/web-common for authentication and initialization.
    /// When enabled, generated React apps will use AppInitializer and AuthProvider.
    /// </summary>
    public bool UseSfdWebCommon { get; set; } = true;

    /// <summary>
    /// The application type for AppInitializer (user, service, etc.)
    /// </summary>
    public string AppType { get; set; } = "user";

    /// <summary>
    /// Whether the app requires authentication (uses ProtectedRoute).
    /// </summary>
    public bool RequiresAuth { get; set; } = true;

    /// <summary>
    /// Custom scopes for authentication (default: "openid profile email").
    /// </summary>
    public string AuthScopes { get; set; } = "openid profile email";

    /// <summary>
    /// NPM registry URL for @sfd/web-common package.
    /// Leave null for default registry.
    /// </summary>
    public string? NpmRegistryUrl { get; set; }
}

/// <summary>
/// Target UI framework for form translations.
/// </summary>
public enum UITargetFramework
{
    React,
    Blazor,
    WinForms,
    WPF,
    MAUI,
    None // For non-UI projects
}

/// <summary>
/// Options for API/Repository generation.
/// </summary>
public class ApiGenerationOptions
{
    public bool GenerateRepositories { get; set; } = true;
    public bool GenerateControllers { get; set; } = true;
    public bool GenerateDtos { get; set; } = true;
    public string RepositoryBaseClass { get; set; } = "BaseRepository";
    public string RepositoryNamespace { get; set; } = "Repositories";
    public string ControllerNamespace { get; set; } = "Controllers";
    public string DtoNamespace { get; set; } = "Dtos";
    public bool UseAsyncMethods { get; set; } = true;
    public bool GenerateSwaggerDocs { get; set; } = true;
    public string ApiRoutePrefix { get; set; } = "api";

    /// <summary>
    /// Options for @sfd/web-common React library integration.
    /// </summary>
    public SfdWebCommonOptions SfdWebCommon { get; set; } = new();
}

/// <summary>
/// Configuration for output generation.
/// </summary>
public class OutputOptions
{
    public OutputFormat Format { get; set; } = OutputFormat.Folder;
    public string OutputPath { get; set; } = "./output";
    public bool GenerateSolution { get; set; } = true;
    public bool GenerateProjectFile { get; set; } = true;
    public bool IncludeOriginalAsComments { get; set; } = false;
    public bool GenerateDeploymentScripts { get; set; } = false;
    public ScriptFormat ScriptFormat { get; set; } = ScriptFormat.Both;
}

public enum OutputFormat
{
    Folder,
    Zip,
    Scripts
}

public enum ScriptFormat
{
    PowerShell,
    Bash,
    Both
}

/// <summary>
/// Result of a translation operation.
/// </summary>
public class TranslationResult
{
    public bool Success { get; set; }
    public string SourceFile { get; set; } = string.Empty;
    public string? OutputFile { get; set; }
    public string? TranslatedCode { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
    public int TokensUsed { get; set; }
    public List<string> Warnings { get; set; } = [];

    /// <summary>
    /// List of Delphi source units that contributed to this translation.
    /// </summary>
    public List<string> SourceUnits { get; set; } = [];
}

/// <summary>
/// Summary of a complete project translation.
/// </summary>
public class ProjectTranslationSummary
{
    public string ProjectName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public int TotalUnits { get; set; }
    public int SuccessfulTranslations { get; set; }
    public int FailedTranslations { get; set; }
    public int SkippedUnits { get; set; }
    public List<TranslationResult> Results { get; set; } = [];
    public string? OutputPath { get; set; }
    public string? ZipFilePath { get; set; }
}

/// <summary>
/// Session state for tracking ongoing analysis/translation work.
/// </summary>
public class AnalysisSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString("N");
    public DelphiProject? Project { get; set; }
    public TranslationOptions TranslationOptions { get; set; } = new();
    public OutputOptions OutputOptions { get; set; } = new();
    public ApiSpecification ApiSpecification { get; set; } = new();
    public List<ReactComponentDefinition> ReactComponents { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public SessionStatus Status { get; set; } = SessionStatus.Initialized;
    public int CurrentUnitIndex { get; set; }
    public List<string> Log { get; set; } = [];
}

public enum SessionStatus
{
    Initialized,
    Scanning,
    Analyzing,
    Translating,
    GeneratingOutput,
    Completed,
    Failed
}