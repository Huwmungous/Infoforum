namespace DelphiAnalysisMcpServer.Models;

/// <summary>
/// Represents a database operation detected in Delphi code.
/// </summary>
public class DatabaseOperation
{
    public string MethodName { get; set; } = string.Empty;
    public string ContainingClass { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public DatabaseOperationType OperationType { get; set; }
    public string? SqlStatement { get; set; }
    public List<SqlParameter> Parameters { get; set; } = [];
    public string? TableName { get; set; }
    public bool IsPartOfTransaction { get; set; }
    public string? TransactionGroupId { get; set; }
    public List<string> RelatedOperations { get; set; } = [];
    public string OriginalDelphiCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Source line number where this query was found.
    /// Used for linking queries to methods.
    /// </summary>
    public int? SourceLineNumber { get; set; }

    /// <summary>
    /// Field accesses extracted from the method body (e.g., FieldByName('NAME').AsString).
    /// These are used to determine which columns are actually needed from SELECT * queries.
    /// </summary>
    public List<FieldAccess> FieldAccesses { get; set; } = [];
}

/// <summary>
/// Represents a field access extracted from Delphi code.
/// E.g., qr.FieldByName('CUSTOMER_NAME').AsString
/// </summary>
public class FieldAccess
{
    /// <summary>
    /// The field/column name accessed (e.g., "CUSTOMER_NAME").
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// The Delphi type accessor used (e.g., "AsString", "AsInteger", "AsDateTime").
    /// </summary>
    public string DelphiAccessor { get; set; } = string.Empty;

    /// <summary>
    /// The inferred Delphi type (e.g., "String", "Integer", "TDateTime").
    /// </summary>
    public string DelphiType { get; set; } = string.Empty;

    /// <summary>
    /// The corresponding C# type (e.g., "string", "int", "DateTime").
    /// </summary>
    public string CSharpType { get; set; } = string.Empty;

    /// <summary>
    /// Whether this field can be null based on context.
    /// </summary>
    public bool IsNullable { get; set; }
}

/// <summary>
/// Represents a SQL parameter extracted from Delphi code.
/// </summary>
public class SqlParameter
{
    public string Name { get; set; } = string.Empty;
    public string DelphiType { get; set; } = string.Empty;
    public string CSharpType { get; set; } = string.Empty;
    public ParameterDirection Direction { get; set; } = ParameterDirection.Input;
}

public enum ParameterDirection
{
    Input,
    Output,
    InputOutput
}

public enum DatabaseOperationType
{
    Select,
    Insert,
    Update,
    Delete,
    StoredProcedure,
    ExecuteScalar,
    ExecuteNonQuery,
    Transaction,
    DDL,
    Unknown
}

/// <summary>
/// Represents a transaction group - multiple operations that must be atomic.
/// </summary>
public class TransactionGroup
{
    public string GroupId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string MethodName { get; set; } = string.Empty;
    public string ContainingClass { get; set; } = string.Empty;
    public List<DatabaseOperation> Operations { get; set; } = [];
    public string OriginalDelphiCode { get; set; } = string.Empty;
}

/// <summary>
/// Represents a repository method to be generated.
/// </summary>
public class RepositoryMethod
{
    public string Name { get; set; } = string.Empty;
    public string ReturnType { get; set; } = "Task";
    public List<MethodParameter> Parameters { get; set; } = [];
    public string SqlStatement { get; set; } = string.Empty;
    public DatabaseOperationType OperationType { get; set; }
    public bool UsesTransaction { get; set; }
    public string? TransactionGroupId { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The DTO type returned by this method (for SELECT operations).
    /// </summary>
    public string? ReturnDtoType { get; set; }

    /// <summary>
    /// Whether this method returns a collection.
    /// </summary>
    public bool ReturnsCollection { get; set; }

    /// <summary>
    /// Delphi source units that contributed to this method.
    /// </summary>
    public List<string> SourceUnits { get; set; } = [];

    /// <summary>
    /// Original Delphi method name for traceability.
    /// </summary>
    public string OriginalDelphiMethod { get; set; } = string.Empty;
}

/// <summary>
/// Represents a controller action to be generated.
/// </summary>
public class ControllerAction
{
    public string Name { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = "GET";
    public string Route { get; set; } = string.Empty;
    public string ReturnType { get; set; } = "Task<IActionResult>";
    public List<MethodParameter> Parameters { get; set; } = [];
    public List<string> RepositoryMethodCalls { get; set; } = [];
    public bool RequiresTransaction { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Delphi source units that contributed to this action.
    /// </summary>
    public List<string> SourceUnits { get; set; } = [];
}

/// <summary>
/// Parameter for a method.
/// </summary>
public class MethodParameter
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
    public bool IsFromBody { get; set; }
    public bool IsFromQuery { get; set; }
    public bool IsFromRoute { get; set; }
}

/// <summary>
/// Represents a complete API specification for a translated project.
/// </summary>
public class ApiSpecification
{
    public string ProjectName { get; set; } = string.Empty;
    public string BaseNamespace { get; set; } = string.Empty;
    public List<RepositoryDefinition> Repositories { get; set; } = [];
    public List<ControllerDefinition> Controllers { get; set; } = [];
    public List<DtoDefinition> Dtos { get; set; } = [];
}

/// <summary>
/// Represents a repository class to be generated.
/// </summary>
public class RepositoryDefinition
{
    public string Name { get; set; } = string.Empty;
    public string InterfaceName => $"I{Name}";
    public string Description { get; set; } = string.Empty;
    public List<RepositoryMethod> Methods { get; set; } = [];
    public List<string> RequiredUsings { get; set; } = [];

    /// <summary>
    /// Delphi source units that contributed to this repository.
    /// </summary>
    public List<string> SourceUnits { get; set; } = [];
}

/// <summary>
/// Represents a controller class to be generated.
/// </summary>
public class ControllerDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ControllerAction> Actions { get; set; } = [];
    public List<string> RequiredRepositories { get; set; } = [];

    /// <summary>
    /// Delphi source units that contributed to this controller.
    /// </summary>
    public List<string> SourceUnits { get; set; } = [];
}

/// <summary>
/// Represents a DTO to be generated.
/// </summary>
public class DtoDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<DtoProperty> Properties { get; set; } = [];
    public bool UseRecord { get; set; } = true;

    /// <summary>
    /// Delphi source units that contributed to this DTO.
    /// </summary>
    public List<string> SourceUnits { get; set; } = [];

    /// <summary>
    /// The table this DTO primarily represents.
    /// </summary>
    public string? SourceTable { get; set; }
}

/// <summary>
/// Property in a DTO.
/// </summary>
public class DtoProperty
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public string? Description { get; set; }

    /// <summary>
    /// The original database column name (may differ from C# property name).
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;
}

/// <summary>
/// React component definition.
/// </summary>
public class ReactComponentDefinition
{
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public ComponentType ComponentType { get; set; } = ComponentType.Functional;
    public List<ReactProp> Props { get; set; } = [];
    public List<ReactStateVariable> StateVariables { get; set; } = [];
    public List<string> ApiEndpoints { get; set; } = [];
    public string OriginalFormName { get; set; } = string.Empty;
    public List<ReactChildComponent> Children { get; set; } = [];

    /// <summary>
    /// Delphi source units (forms) that contributed to this component.
    /// </summary>
    public List<string> SourceUnits { get; set; } = [];
}

public enum ComponentType
{
    Functional,
    Page,
    Layout,
    Modal,
    Form
}

public class ReactProp
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
}

public class ReactStateVariable
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? InitialValue { get; set; }
}

public class ReactChildComponent
{
    public string ComponentName { get; set; } = string.Empty;
    public string OriginalVclClass { get; set; } = string.Empty;
    public Dictionary<string, string> Props { get; set; } = [];
}

/// <summary>
/// Result of code generation operation.
/// </summary>
public class CodeGenerationResult
{
    public string ProjectName { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public List<string> GeneratedFiles { get; set; } = [];
    public int DtoCount { get; set; }
    public int RepositoryCount { get; set; }
    public int ControllerCount { get; set; }
    public int TotalMethodCount { get; set; }
}

/// <summary>
/// Configuration used for code generation, stored with the project.
/// </summary>
public class GenerationConfig
{
    public string BaseNamespace { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public int OperationCount { get; set; }
    public int DtoCount { get; set; }
    public int RepositoryCount { get; set; }
    public int ControllerCount { get; set; }
}

/// <summary>
/// Basic project information retrieved from database.
/// </summary>
public class ProjectInfo
{
    public int Idx { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? RootPath { get; set; }
    public string? DprFilePath { get; set; }
    public string? DprojFilePath { get; set; }
    public string? FrameworkType { get; set; }
}
