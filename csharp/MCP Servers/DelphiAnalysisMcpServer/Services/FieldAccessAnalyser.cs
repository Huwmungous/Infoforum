using System.Text;
using System.Text.RegularExpressions;
using DelphiAnalysisMcpServer.Models;

namespace DelphiAnalysisMcpServer.Services;

/// <summary>
/// Analyses Delphi code to extract field accesses from database queries.
/// This is crucial for converting SELECT * queries to specific column lists
/// and generating appropriate DTOs.
/// </summary>
public static partial class FieldAccessAnalyser
{
    #region Generated Regex Patterns

    // Matches: FieldByName('COLUMN_NAME').AsString, .AsInteger, etc.
    [GeneratedRegex(@"FieldByName\s*\(\s*['""](\w+)['""]\s*\)\s*\.\s*(As\w+|Value)", RegexOptions.IgnoreCase)]
    private static partial Regex FieldByNameWithTypeRegex();

    // Matches: Fields[0].AsString, Fields[1].AsInteger (positional access)
    [GeneratedRegex(@"Fields\s*\[\s*(\d+)\s*\]\s*\.\s*(As\w+|Value)", RegexOptions.IgnoreCase)]
    private static partial Regex FieldsIndexRegex();

    // Matches: FieldValues['COLUMN_NAME']
    [GeneratedRegex(@"FieldValues\s*\[\s*['""](\w+)['""]\s*\]", RegexOptions.IgnoreCase)]
    private static partial Regex FieldValuesRegex();

    // Matches: Dataset['COLUMN_NAME'] shorthand syntax
    [GeneratedRegex(@"(?:qr|Query|DataSet|ds)\s*\[\s*['""](\w+)['""]\s*\]", RegexOptions.IgnoreCase)]
    private static partial Regex DatasetIndexerRegex();

    // Matches: if not qr.FieldByName('X').IsNull then - indicates nullable field
    [GeneratedRegex(@"(?:if\s+)?(?:not\s+)?(?:\w+\.)?FieldByName\s*\(\s*['""](\w+)['""]\s*\)\s*\.\s*IsNull", RegexOptions.IgnoreCase)]
    private static partial Regex NullableFieldPatternRegex();

    // Matches: SELECT columns FROM
    [GeneratedRegex(@"SELECT\s+(.+?)\s+FROM", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SelectColumnsRegex();

    // Matches: column aliases like COLUMN_NAME AS Alias or COLUMN_NAME Alias
    [GeneratedRegex(@"(\w+)(?:\s+AS\s+|\s+)(\w+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ColumnAliasRegex();

    // Matches: SELECT * FROM
    [GeneratedRegex(@"(SELECT\s+)\*(\s+FROM)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SelectStarRegex();

    #endregion

    #region Type Mappings

    /// <summary>
    /// Maps Delphi field accessor methods to their types.
    /// </summary>
    private static readonly Dictionary<string, (string DelphiType, string CSharpType)> AccessorTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        // String types
        { "AsString", ("String", "string") },
        { "AsWideString", ("WideString", "string") },
        { "AsAnsiString", ("AnsiString", "string") },

        // Integer types
        { "AsInteger", ("Integer", "int") },
        { "AsSmallInt", ("SmallInt", "short") },
        { "AsLargeInt", ("Int64", "long") },
        { "AsLongInt", ("LongInt", "int") },
        { "AsWord", ("Word", "ushort") },
        { "AsByte", ("Byte", "byte") },
        { "AsShortInt", ("ShortInt", "sbyte") },

        // Floating point types
        { "AsFloat", ("Double", "double") },
        { "AsExtended", ("Extended", "decimal") },
        { "AsSingle", ("Single", "float") },
        { "AsCurrency", ("Currency", "decimal") },
        { "AsBCD", ("TBCD", "decimal") },
        { "AsFMTBCD", ("TBCD", "decimal") },

        // Date/Time types
        { "AsDateTime", ("TDateTime", "DateTime") },
        { "AsDate", ("TDate", "DateOnly") },
        { "AsTime", ("TTime", "TimeOnly") },
        { "AsSQLTimeStamp", ("TSQLTimeStamp", "DateTime") },

        // Boolean
        { "AsBoolean", ("Boolean", "bool") },

        // Binary types
        { "AsBytes", ("TBytes", "byte[]") },
        { "AsBlob", ("TBlob", "byte[]") },
        { "AsBlobRef", ("TBlobRef", "byte[]") },

        // GUID
        { "AsGuid", ("TGUID", "Guid") },

        // Variant (fallback)
        { "AsVariant", ("Variant", "object") },
        { "Value", ("Variant", "object") }
    };

    #endregion

    /// <summary>
    /// Extracts all field accesses from a method body with their types.
    /// </summary>
    /// <param name="methodBody">The Delphi method body to analyse.</param>
    /// <returns>List of field accesses with inferred types.</returns>
    public static List<FieldAccess> ExtractFieldAccesses(string methodBody)
    {
        var fieldAccesses = new Dictionary<string, FieldAccess>(StringComparer.OrdinalIgnoreCase);
        var nullableFields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // First pass: identify fields that have null checks (these should be nullable)
        foreach (Match match in NullableFieldPatternRegex().Matches(methodBody))
        {
            nullableFields.Add(match.Groups[1].Value);
        }

        // Extract FieldByName('X').AsType patterns
        foreach (Match match in FieldByNameWithTypeRegex().Matches(methodBody))
        {
            var fieldName = match.Groups[1].Value;
            var accessor = match.Groups[2].Value;

            if (!fieldAccesses.ContainsKey(fieldName))
            {
                var (delphiType, csharpType) = GetTypeFromAccessor(accessor);
                fieldAccesses[fieldName] = new FieldAccess
                {
                    FieldName = fieldName,
                    DelphiAccessor = accessor,
                    DelphiType = delphiType,
                    CSharpType = csharpType,
                    IsNullable = nullableFields.Contains(fieldName)
                };
            }
        }

        // Extract FieldValues['X'] patterns (type unknown, default to object)
        foreach (Match match in FieldValuesRegex().Matches(methodBody))
        {
            var fieldName = match.Groups[1].Value;
            if (!fieldAccesses.ContainsKey(fieldName))
            {
                fieldAccesses[fieldName] = new FieldAccess
                {
                    FieldName = fieldName,
                    DelphiAccessor = "Value",
                    DelphiType = "Variant",
                    CSharpType = "object",
                    IsNullable = true // FieldValues access is typically nullable
                };
            }
        }

        // Extract Dataset['X'] shorthand patterns
        foreach (Match match in DatasetIndexerRegex().Matches(methodBody))
        {
            var fieldName = match.Groups[1].Value;
            if (!fieldAccesses.ContainsKey(fieldName))
            {
                fieldAccesses[fieldName] = new FieldAccess
                {
                    FieldName = fieldName,
                    DelphiAccessor = "Value",
                    DelphiType = "Variant",
                    CSharpType = "object",
                    IsNullable = true
                };
            }
        }

        return [.. fieldAccesses.Values];
    }

    /// <summary>
    /// Refines field types based on additional context analysis.
    /// For example, if a field is used in string concatenation, it's likely a string.
    /// </summary>
    /// <param name="methodBody">The method body to analyse.</param>
    /// <param name="fieldAccesses">The field accesses to refine.</param>
    /// <returns>Refined field accesses with improved type inference.</returns>
    public static List<FieldAccess> RefineFieldTypes(string methodBody, List<FieldAccess> fieldAccesses)
    {
        foreach (var field in fieldAccesses.Where(f => f.CSharpType == "object"))
        {
            // Try to infer type from usage patterns
            var inferredType = InferTypeFromUsage(methodBody, field.FieldName);
            if (inferredType != null)
            {
                field.DelphiType = inferredType.Value.DelphiType;
                field.CSharpType = inferredType.Value.CSharpType;
            }
        }

        return fieldAccesses;
    }

    /// <summary>
    /// Attempts to infer a field's type from how it's used in the code.
    /// </summary>
    private static (string DelphiType, string CSharpType)? InferTypeFromUsage(string methodBody, string fieldName)
    {
        var escapedFieldName = Regex.Escape(fieldName);

        // Check for string operations
        if (Regex.IsMatch(methodBody, $@"(?:Trim|UpperCase|LowerCase|Copy|Pos|Length)\s*\([^)]*{escapedFieldName}", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(methodBody, $@"{escapedFieldName}[^;]*\+\s*['""]", RegexOptions.IgnoreCase))
        {
            return ("String", "string");
        }

        // Check for numeric operations
        if (Regex.IsMatch(methodBody, $@"{escapedFieldName}\s*[\+\-\*\/]\s*\d+", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(methodBody, $@"(?:Inc|Dec|Round|Trunc)\s*\([^)]*{escapedFieldName}", RegexOptions.IgnoreCase))
        {
            return ("Integer", "int");
        }

        // Check for date operations
        if (Regex.IsMatch(methodBody, $@"(?:FormatDateTime|DateToStr|EncodeDate|DecodeDate|DayOf|MonthOf|YearOf)\s*\([^)]*{escapedFieldName}", RegexOptions.IgnoreCase))
        {
            return ("TDateTime", "DateTime");
        }

        // Check for boolean usage
        if (Regex.IsMatch(methodBody, $@"if\s+.*{escapedFieldName}\s+then", RegexOptions.IgnoreCase) ||
            Regex.IsMatch(methodBody, $@"{escapedFieldName}\s*:=\s*(?:True|False)", RegexOptions.IgnoreCase))
        {
            return ("Boolean", "bool");
        }

        return null;
    }

    /// <summary>
    /// Gets the Delphi and C# types from an accessor method name.
    /// </summary>
    private static (string DelphiType, string CSharpType) GetTypeFromAccessor(string accessor)
    {
        if (AccessorTypeMap.TryGetValue(accessor, out var types))
        {
            return types;
        }

        // Unknown accessor - default to object
        return ("Variant", "object");
    }

    /// <summary>
    /// Converts a database column name to a valid C# property name.
    /// E.g., "CUSTOMER_ID" -> "CustomerId"
    /// </summary>
    public static string ColumnNameToPropertyName(string columnName)
    {
        if (string.IsNullOrEmpty(columnName))
            return columnName;

        // Split by underscores and convert to PascalCase
        var parts = columnName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var result = new StringBuilder();

        foreach (var part in parts)
        {
            if (part.Length > 0)
            {
                // First character uppercase, rest lowercase
                result.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                {
                    result.Append(part[1..].ToLowerInvariant());
                }
            }
        }

        var propertyName = result.ToString();

        // Ensure it doesn't start with a digit
        if (propertyName.Length > 0 && char.IsDigit(propertyName[0]))
        {
            propertyName = "_" + propertyName;
        }

        return propertyName;
    }

    /// <summary>
    /// Generates a suggested DTO name from a table name.
    /// E.g., "CUSTOMERS" -> "CustomerDto", "ORDER_ITEMS" -> "OrderItemDto"
    /// </summary>
    public static string TableNameToDtoName(string tableName)
    {
        var baseName = ColumnNameToPropertyName(tableName);

        // Singularise common plural endings
        baseName = Singularise(baseName);

        return baseName + "Dto";
    }

    /// <summary>
    /// Simple singularisation for common English plural patterns.
    /// </summary>
    public static string Singularise(string word)
    {
        if (string.IsNullOrEmpty(word) || word.Length < 3)
            return word;

        // Common patterns
        if (word.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && word.Length > 4)
        {
            return word[..^3] + "y";
        }
        if (word.EndsWith("es", StringComparison.OrdinalIgnoreCase) &&
            (word.EndsWith("sses", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("xes", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("ches", StringComparison.OrdinalIgnoreCase) ||
             word.EndsWith("shes", StringComparison.OrdinalIgnoreCase)))
        {
            return word[..^2];
        }
        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
            !word.EndsWith("us", StringComparison.OrdinalIgnoreCase) &&
            !word.EndsWith("is", StringComparison.OrdinalIgnoreCase))
        {
            return word[..^1];
        }

        return word;
    }

    /// <summary>
    /// Analyses a SQL statement to extract the column list.
    /// Returns null if it's a SELECT * query.
    /// </summary>
    public static List<string>? ExtractSelectColumns(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return null;

        var match = SelectColumnsRegex().Match(sql);
        if (!match.Success)
            return null;

        var columnList = match.Groups[1].Value.Trim();

        // Check for SELECT *
        if (columnList == "*" || columnList.Contains(".*", StringComparison.Ordinal))
            return null;

        // Parse column list (simplified - doesn't handle complex expressions)
        var columns = new List<string>();
        var parts = columnList.Split(',');

        foreach (var part in parts)
        {
            var col = part.Trim();

            // Handle aliases: COLUMN_NAME AS Alias or COLUMN_NAME Alias
            var aliasMatch = ColumnAliasRegex().Match(col);
            if (aliasMatch.Success)
            {
                columns.Add(aliasMatch.Groups[2].Value); // Use the alias
            }
            else
            {
                // Handle table.column format
                var dotIndex = col.LastIndexOf('.');
                if (dotIndex >= 0)
                {
                    columns.Add(col[(dotIndex + 1)..]);
                }
                else
                {
                    columns.Add(col);
                }
            }
        }

        return columns;
    }

    /// <summary>
    /// Rewrites a SELECT * query to use specific columns based on field accesses.
    /// </summary>
    /// <param name="sql">The original SQL with SELECT *</param>
    /// <param name="fieldAccesses">The fields actually accessed in code</param>
    /// <returns>Rewritten SQL with specific columns, or original if no changes needed</returns>
    public static string RewriteSelectStar(string sql, List<FieldAccess> fieldAccesses)
    {
        if (string.IsNullOrWhiteSpace(sql) || fieldAccesses.Count == 0)
            return sql;

        // Check if it's a SELECT * query
        var match = SelectStarRegex().Match(sql);
        if (!match.Success)
            return sql;

        // Build column list from field accesses
        var columns = string.Join(", ", fieldAccesses.Select(f => $"\"{f.FieldName}\""));

        // Replace SELECT * with SELECT columns
        var beforeFrom = sql[..(match.Index + match.Groups[1].Length)];
        var fromOnwards = sql[(match.Index + match.Groups[1].Length + 1)..]; // Skip the *

        return beforeFrom + columns + fromOnwards;
    }
}
