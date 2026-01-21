namespace CodeAnalysisMcpServer.Tools
{
    // ========== RECORD TYPES ==========

     record TableColumn
    {
        public string Name { get; init; } = "";
        public string DataType { get; init; } = "";
        public bool IsNullable { get; init; }
        public int? MaxLength { get; init; }
        public int? Precision { get; init; }
        public int? Scale { get; init; }
        public string? DefaultValue { get; init; }
    }

     record ForeignKey
    {
        public string ColumnName { get; init; } = "";
        public string ReferencedTable { get; init; } = "";
        public string ReferencedColumn { get; init; } = "";
    }

     record DatabaseCall
    {
        public int LineNumber { get; init; }
        public string Component { get; init; } = "";
        public string Method { get; init; } = "";
        public string Context { get; init; } = "";
    }

     record ProcedureCall
    {
        public string Name { get; init; } = "";
        public int LineNumber { get; init; }
        public List<string> Parameters { get; init; } = [];
        public string Context { get; init; } = "";
    }

     record ClassDefinition
    {
        public string Name { get; init; } = "";
        public string BaseClass { get; init; } = "";
        public int LineNumber { get; init; }
        public List<string> Properties { get; init; } = [];
        public List<string> Methods { get; init; } = [];
    }

     record MethodSignature
    {
        public string Name { get; init; } = "";
        public string ReturnType { get; init; } = "";
        public List<string> Parameters { get; init; } = [];
        public string Visibility { get; init; } = "";
        public int LineNumber { get; init; }
    }

     record DataStructure
    {
        public string Name { get; init; } = "";
        public string Type { get; init; } = "";
        public List<string> Fields { get; init; } = [];
        public int LineNumber { get; init; }
    }
}
