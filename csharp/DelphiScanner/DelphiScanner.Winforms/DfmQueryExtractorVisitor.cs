// DfmQueryExtractorVisitor.cs (improved version)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DelphiScanner.Winforms
{
    public class DfmQueryExtractorVisitor : DelphiDfmBaseVisitor<object>
    {
        public Dictionary<string, QueryInfo> Queries { get; } = [];
        private readonly Stack<string> _contextStack = new();
        private string _currentFormName = "";
        private QueryInfo? _currentQuery = null;

        public override object VisitDfmFile(DelphiDfmParser.DfmFileContext context)
        {
            Console.WriteLine($"[ENTRY] Visiting DFM file with {context.ChildCount} top-level items");
            return base.VisitChildren(context);
        }

        public override object VisitObjectDeclaration(DelphiDfmParser.ObjectDeclarationContext context)
        {
            var className = context.className.Text;
            var objectName = context.objectName.Text;
            var contextPath = string.Join(".", _contextStack.Concat([objectName]));

            Console.WriteLine($"[VISIT] Object: {objectName} ({className}) at path: {contextPath}");

            // Track context for nested objects
            _contextStack.Push(objectName);

            try
            {
                // Handle top-level forms/datamodules
                if(IsTopLevelContainer(className))
                {
                    _currentFormName = objectName;
                    Console.WriteLine($"[INFO] Set current form/datamodule: {_currentFormName}");
                }

                // Handle query objects
                if(IsQueryObject(className))
                {
                    ProcessQueryObject(context, objectName, className);
                }

                // Handle field objects (nested in queries)
                if(IsFieldObject(className) && _currentQuery != null)
                {
                    ProcessFieldObject(context, objectName, className);
                }

                // Continue visiting children
                return base.VisitChildren(context);
            }
            finally
            {
                _contextStack.Pop();

                // Clear current query context when leaving a query object
                if(IsQueryObject(className))
                {
                    _currentQuery = null;
                }
            }
        }

        public override object VisitSqlProperty(DelphiDfmParser.SqlPropertyContext context)
        {
            Console.WriteLine("[SQL] Found SQL.Strings property");

            if(_currentQuery != null && context.queryText?.stringListItems() != null)
            {
                var lines = context.queryText.stringListItems().STRING();
                if(lines != null)
                {
                    foreach(var line in lines)
                    {
                        var unquotedLine = Unquote(line.GetText());
                        _currentQuery.SqlText.Add(unquotedLine);
                        Console.WriteLine($"[SQL] Added line: {unquotedLine}");
                    }
                }
            }

            return base.VisitChildren(context);
        }

        public override object VisitGenericProperty(DelphiDfmParser.GenericPropertyContext context)
        {
            var propertyName = context.IDENTIFIER()?.GetText() ?? "Unknown";
            var propertyValue = context.value()?.GetText() ?? "null";

            Console.WriteLine($"[PROPERTY] {propertyName} = {propertyValue}");

            // Store properties in current query if we're in one
            if(_currentQuery != null && !string.IsNullOrEmpty(propertyName) && propertyValue != "null")
            {
                _currentQuery.Properties[propertyName] = propertyValue;
            }

            return base.VisitChildren(context);
        }

        public override object VisitArrayValueType(DelphiDfmParser.ArrayValueTypeContext context)
        {
            Console.WriteLine($"[ARRAY] Found array value: {context.GetText()}");
            return base.VisitChildren(context);
        }

        private void ProcessQueryObject(DelphiDfmParser.ObjectDeclarationContext context, string objectName, string className)
        {
            var queryInfo = new QueryInfo
            {
                ObjectName = objectName,
                ClassName = className,
                FormName = _currentFormName
            };

            var key = GetQueryKey(objectName);
            Queries[key] = queryInfo;
            _currentQuery = queryInfo; // Set context for nested processing

            Console.WriteLine($"[QUERY] Created query object: {key}");
        }

        private void ProcessFieldObject(DelphiDfmParser.ObjectDeclarationContext context, string objectName, string className)
        {
            var fieldInfo = new FieldInfo
            {
                FieldName = objectName,
                FieldType = className
            };

            _currentQuery!.Fields.Add(fieldInfo);
            Console.WriteLine($"[FIELD] Added field: {objectName} ({className})");
        }

        private string GetQueryKey(string objectName)
        {
            if(string.IsNullOrEmpty(_currentFormName))
                return objectName;
            return $"{_currentFormName}.{objectName}";
        }

        private static bool IsTopLevelContainer(string className)
        {
            return className.Equals("TForm", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("TDM", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("TDataModule", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsQueryObject(string className)
        {
            // Add more query types as needed
            return className.Equals("TPgQuery", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("TQuery", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("TADOQuery", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("TClientDataSet", StringComparison.OrdinalIgnoreCase) ||
                   className.Equals("TFDQuery", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFieldObject(string className)
        {
            // Common field types in Delphi
            return className.EndsWith("Field", StringComparison.OrdinalIgnoreCase);
        }

        private static string Unquote(string s)
        {
            if(string.IsNullOrWhiteSpace(s))
                return "";

            if(s.StartsWith('\'') && s.EndsWith('\'') && s.Length >= 2)
                s = s[1..^1];

            // Handle escaped quotes
            return s.Replace("''", "'");
        }

        // Utility method to get summary of extracted queries
        public void PrintSummary()
        {
            Console.WriteLine("\n=== EXTRACTION SUMMARY ===");
            Console.WriteLine($"Found {Queries.Count} query objects:");

            foreach(var kvp in Queries)
            {
                var query = kvp.Value;
                Console.WriteLine($"\n[{kvp.Key}]");
                Console.WriteLine($"  Class: {query.ClassName}");
                Console.WriteLine($"  SQL Lines: {query.SqlText.Count}");
                Console.WriteLine($"  Properties: {query.Properties.Count}");
                Console.WriteLine($"  Fields: {query.Fields.Count}");

                if(query.SqlText.Count != 0)
                {
                    Console.WriteLine("  SQL Preview:");
                    var preview = query.GetFullSql();
                    if(preview.Length > 200)
                        preview = string.Concat(preview.AsSpan(0, 200), "...");
                    Console.WriteLine($"    {preview.Replace(Environment.NewLine, " ")}");
                }
            }
        }
    }
}