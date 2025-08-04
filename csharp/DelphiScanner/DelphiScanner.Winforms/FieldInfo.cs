using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DelphiScanner.Winforms
{
    // FieldInfo.cs
    public class FieldInfo
    {
        public string FieldName { get; set; } = "";
        public string FieldType { get; set; } = "";
        public Dictionary<string, string> Properties { get; } = new();
    }
}
