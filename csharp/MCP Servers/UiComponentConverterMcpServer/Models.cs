namespace UiComponentConverterMcpServer;

public record DelphiForm
{
    public string FormName { get; init; } = string.Empty;
    public string ClassName { get; init; } = string.Empty;
    public List<DelphiComponent> Components { get; init; } = [];
}

public class DelphiComponent
{
    public string Name { get; set; } = string.Empty;
    public string ComponentType { get; set; } = string.Empty;
    public List<string> EventHandlers { get; set; } = [];
    public List<DelphiComponent> Children { get; set; } = [];
}

public class ComponentAnalysis
{
    public int TotalComponents { get; set; }
    public Dictionary<string, int> ComponentTypeCounts { get; set; } = [];
    public List<string> EventHandlers { get; set; } = [];
    public List<string> DataBoundComponents { get; set; } = [];
}

public class GeneratedComponent
{
    public bool Success { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string StyleCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string[] Warnings { get; set; } = [];
}

public class ToolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object? InputSchema { get; set; }
}