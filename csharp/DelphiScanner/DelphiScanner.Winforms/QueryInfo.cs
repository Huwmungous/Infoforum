namespace DelphiScanner.Winforms
{
    public class QueryInfo
    {
        public List<string> SqlText { get; } = [];
        public string ObjectName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string UnitFileName { get; set; } = "";
        public Dictionary<string, string> Properties { get; } = [];
        public List<FieldInfo> Fields { get; } = [];

        public string GetFullSql() => string.Join(Environment.NewLine, SqlText);

        public string Form => Path.GetFileNameWithoutExtension(UnitFileName);

        public List<string> Usage = [];
    }

}