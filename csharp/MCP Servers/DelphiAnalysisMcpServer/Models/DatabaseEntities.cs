namespace DelphiAnalysisMcpServer.Models;

/// <summary>
/// Represents a scanned directory in the database.
/// </summary>
public class DirectoryEntity
{
    public int Idx { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a Delphi project in the database.
/// </summary>
public class ProjectEntity
{
    public int Idx { get; set; }
    public int DirectoryIdx { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public string? DprFilePath { get; set; }
    public string? DprojFilePath { get; set; }
    public string FrameworkType { get; set; } = "VCL";
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public string AnalysisStatus { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a Delphi unit in the database.
/// </summary>
public class UnitEntity
{
    public int Idx { get; set; }
    public int ProjectIdx { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int LineCount { get; set; }
    public string? AssociatedDfmFile { get; set; }
    public string? DfmFilePath { get; set; }
    public bool IsDataModule { get; set; }
    public bool IsForm { get; set; }
    public bool HasForm { get; set; }
    public string? FormName { get; set; }
    public string? FormType { get; set; }
    public bool IsFromDproj { get; set; }
    public bool IsInDpr { get; set; } = true;
    public string AnalysisStatus { get; set; } = "Pending";
    public string? AnalysisNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a class declaration in the database.
/// </summary>
public class ClassEntity
{
    public int Idx { get; set; }
    public int UnitIdx { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string? ParentClass { get; set; }
    public string DefaultVisibility { get; set; } = "Public";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a method in the database.
/// </summary>
public class MethodEntity
{
    public int Idx { get; set; }
    public int? ClassIdx { get; set; }
    public int? RecordIdx { get; set; }
    public int? UnitIdx { get; set; }
    public string MethodName { get; set; } = string.Empty;
    public string Kind { get; set; } = "Procedure";
    public string? ReturnType { get; set; }
    public string Visibility { get; set; } = "Public";
    public bool IsVirtual { get; set; }
    public bool IsOverride { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsStatic { get; set; }
    public bool IsOverload { get; set; }
    public bool IsStandalone { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a database query detected in Delphi code.
/// </summary>
public class QueryEntity
{
    public int Idx { get; set; }
    public int UnitIdx { get; set; }
    public int? MethodIdx { get; set; }
    public int? ClassIdx { get; set; }
    public string? ContainingClass { get; set; }
    public string? MethodName { get; set; }
    public string QueryComponentType { get; set; } = string.Empty;
    public string? SqlText { get; set; }
    public string OperationType { get; set; } = "Unknown";
    public string? TableName { get; set; }
    public bool IsPartOfTransaction { get; set; }
    public string? TransactionGroupId { get; set; }
    public string? OriginalDelphiCode { get; set; }
    public int? SourceLineNumber { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a query parameter in the database.
/// </summary>
public class QueryParameterEntity
{
    public int Idx { get; set; }
    public int QueryIdx { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public string DelphiType { get; set; } = string.Empty;
    public string CSharpType { get; set; } = string.Empty;
    public string Direction { get; set; } = "Input";
    public int ParameterOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a form in the database.
/// </summary>
public class FormEntity
{
    public int Idx { get; set; }
    public int ProjectIdx { get; set; }
    public int? UnitIdx { get; set; }
    public string FormName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string? ParentClass { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a form component in the database.
/// </summary>
public class FormComponentEntity
{
    public int Idx { get; set; }
    public int FormIdx { get; set; }
    public int? ParentComponentIdx { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public int ComponentOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents an analysis session in the database.
/// </summary>
public class AnalysisSessionEntity
{
    public string Idx { get; set; } = string.Empty;
    public int? DirectoryIdx { get; set; }
    public int? ProjectIdx { get; set; }
    public string Status { get; set; } = "Initialized";
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Project statistics after persistence - actual counts from database.
/// CRITICAL FIX: Used to return accurate statistics instead of zero counts.
/// </summary>
public class ProjectStatistics
{
    public int Units { get; set; }
    public int Forms { get; set; }
    public int SourceFilesLoaded { get; set; }
    public int UnitsProcessed { get; set; }
    public int QueriesFound { get; set; }  // THE CRITICAL FIELD
}
