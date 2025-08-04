namespace DelphiScanner.Winforms
{
    public class QueryInfo
    {
        public List<string> SqlText { get; } = new();
        public string ObjectName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string FormName { get; set; } = "";
        public Dictionary<string, string> Properties { get; } = new();
        public List<FieldInfo> Fields { get; } = new();

        public string GetFullSql() => string.Join(Environment.NewLine, SqlText);
    }

}