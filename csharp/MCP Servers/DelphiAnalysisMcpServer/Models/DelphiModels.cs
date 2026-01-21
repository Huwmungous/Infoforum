namespace DelphiAnalysisMcpServer.Models;

using DelphiAnalysisMcpServer.Services;

/// <summary>
/// Represents a complete Delphi project parsed from a .dpr/.dproj file or folder scan.
/// </summary>
public class DelphiProject
{
    public string Name { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public string? DprFilePath { get; set; }
    public string? DprojFilePath { get; set; }
    public List<DelphiUnit> Units { get; set; } = [];
    public List<DelphiForm> Forms { get; set; } = [];
    public List<string> SearchPaths { get; set; } = [];
    public List<string> CompilerDefines { get; set; } = [];
    public string FrameworkType { get; set; } = "VCL"; // VCL, FMX, Console
    public List<string> Warnings { get; set; } = [];
    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Metadata from .dproj file if available.
    /// </summary>
    public DprojMetadata? DprojMetadata { get; set; }

    /// <summary>
    /// Gets all unit names that will be compiled (from .dpr uses clause).
    /// </summary>
    public HashSet<string> GetCompiledUnitNames() =>
        new(Units.Where(u => !u.IsFromDproj || u.IsInDpr)
                .Select(u => u.UnitName), StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Represents a single Delphi unit (.pas file).
/// </summary>
public class DelphiUnit
{
    public string UnitName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int LineCount { get; set; }
    public List<string> UsesInterface { get; set; } = [];
    public List<string> UsesImplementation { get; set; } = [];
    public List<DelphiClass> Classes { get; set; } = [];
    public List<DelphiRecord> Records { get; set; } = [];
    public List<DelphiProcedure> StandaloneProcedures { get; set; } = [];
    public string? AssociatedFormFile { get; set; }
    public bool IsDataModule { get; set; }
    public bool IsForm { get; set; }
    public AnalysisStatus AnalysisStatus { get; set; } = AnalysisStatus.Pending;
    public TranslationStatus TranslationStatus { get; set; } = TranslationStatus.Pending;
    public string? TranslatedCode { get; set; }
    public string? AnalysisNotes { get; set; }

    // .dproj integration properties
    /// <summary>
    /// True if this unit was added from .dproj but not in .dpr uses clause.
    /// </summary>
    public bool IsFromDproj { get; set; }

    /// <summary>
    /// True if this unit appears in the .dpr uses clause (will be compiled).
    /// </summary>
    public bool IsInDpr { get; set; } = true;

    /// <summary>
    /// True if this unit has an associated form (.dfm/.fmx).
    /// </summary>
    public bool HasForm { get; set; }

    /// <summary>
    /// Form name (e.g., "TForm1") from .dproj metadata.
    /// </summary>
    public string? FormName { get; set; }

    /// <summary>
    /// Form type/design class from .dproj (e.g., "TForm", "TDataModule", "TFrame").
    /// </summary>
    public string? FormType { get; set; }

    /// <summary>
    /// Path to the .dfm file if this unit has a form.
    /// </summary>
    public string? DfmFilePath { get; set; }
}

/// <summary>
/// Represents a Delphi form file (.dfm).
/// </summary>
public class DelphiForm
{
    public string FormName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string? ParentClass { get; set; }
    public List<DelphiComponent> Components { get; set; } = [];
    public long FileSizeBytes { get; set; }
    public TranslationStatus TranslationStatus { get; set; } = TranslationStatus.Pending;
    public string? TranslatedCode { get; set; }
}

/// <summary>
/// Represents a component within a DFM file.
/// </summary>
public class DelphiComponent
{
    public string Name { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public Dictionary<string, string> Properties { get; set; } = [];
    public List<DelphiComponent> Children { get; set; } = [];
}

/// <summary>
/// Represents a Delphi class declaration.
/// </summary>
public class DelphiClass
{
    public string ClassName { get; set; } = string.Empty;
    public string? ParentClass { get; set; }
    public List<string> Interfaces { get; set; } = [];
    public List<DelphiField> Fields { get; set; } = [];
    public List<DelphiProperty> Properties { get; set; } = [];
    public List<DelphiMethod> Methods { get; set; } = [];
    public ClassVisibility DefaultVisibility { get; set; } = ClassVisibility.Public;
}

/// <summary>
/// Represents a Delphi record type.
/// </summary>
public class DelphiRecord
{
    public string RecordName { get; set; } = string.Empty;
    public List<DelphiField> Fields { get; set; } = [];
    public List<DelphiMethod> Methods { get; set; } = [];
    public bool IsAdvanced { get; set; } // Has methods, properties, etc.
}

/// <summary>
/// Represents a field in a class or record.
/// </summary>
public class DelphiField
{
    public string Name { get; set; } = string.Empty;
    public string DelphiType { get; set; } = string.Empty;
    public ClassVisibility Visibility { get; set; } = ClassVisibility.Private;
}

/// <summary>
/// Represents a property in a class.
/// </summary>
public class DelphiProperty
{
    public string Name { get; set; } = string.Empty;
    public string DelphiType { get; set; } = string.Empty;
    public string? ReadAccessor { get; set; }
    public string? WriteAccessor { get; set; }
    public ClassVisibility Visibility { get; set; } = ClassVisibility.Public;
    public bool IsDefault { get; set; }
}

/// <summary>
/// Represents a method in a class or record.
/// </summary>
public class DelphiMethod
{
    public string Name { get; set; } = string.Empty;
    public MethodKind Kind { get; set; } = MethodKind.Procedure;
    public string? ReturnType { get; set; }
    public List<DelphiParameter> Parameters { get; set; } = [];
    public ClassVisibility Visibility { get; set; } = ClassVisibility.Public;
    public bool IsVirtual { get; set; }
    public bool IsOverride { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsStatic { get; set; }
    public bool IsOverload { get; set; }
    
    /// <summary>
    /// The source code of the method implementation (body).
    /// </summary>
    public string? SourceCode { get; set; }
    
    /// <summary>
    /// The containing class name (for linking purposes).
    /// </summary>
    public string? ContainingClass { get; set; }
}

/// <summary>
/// Represents a standalone procedure or function.
/// </summary>
public class DelphiProcedure
{
    public string Name { get; set; } = string.Empty;
    public MethodKind Kind { get; set; } = MethodKind.Procedure;
    public string? ReturnType { get; set; }
    public List<DelphiParameter> Parameters { get; set; } = [];
    
    /// <summary>
    /// The source code of the procedure/function implementation (body).
    /// </summary>
    public string? SourceCode { get; set; }
}

/// <summary>
/// Represents a parameter in a method or procedure.
/// </summary>
public class DelphiParameter
{
    public string Name { get; set; } = string.Empty;
    public string DelphiType { get; set; } = string.Empty;
    public ParameterModifier Modifier { get; set; } = ParameterModifier.None;
    public string? DefaultValue { get; set; }
}

public enum AnalysisStatus
{
    Pending,
    InProgress,
    Completed,
    Failed
}

public enum TranslationStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Skipped
}

public enum ClassVisibility
{
    Private,
    Protected,
    Public,
    Published
}

public enum MethodKind
{
    Procedure,
    Function,
    Constructor,
    Destructor
}

public enum ParameterModifier
{
    None,
    Var,
    Const,
    Out
}