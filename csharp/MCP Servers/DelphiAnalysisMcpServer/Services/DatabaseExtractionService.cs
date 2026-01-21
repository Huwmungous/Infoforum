using System.Text.RegularExpressions;
using DelphiAnalysisMcpServer.Models;

namespace DelphiAnalysisMcpServer.Services;

/// <summary>
/// Service for detecting and extracting database operations from Delphi code.
/// </summary>
public partial class DatabaseExtractionService(ILogger<DatabaseExtractionService> logger)
{
    private readonly ILogger<DatabaseExtractionService> _logger = logger;

    #region Generated Regex Patterns

    [GeneratedRegex(@"\b(TQuery|TIBQuery|TADOQuery|TFDQuery|TZQuery|TSQLQuery|TIBDataSet|TADODataSet|TFDMemTable|TClientDataSet|TSQLDataSet)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QueryComponentRegex();

    [GeneratedRegex(@"\b(TDatabase|TIBDatabase|TADOConnection|TFDConnection|TZConnection|TSQLConnection)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DatabaseConnectionRegex();

    [GeneratedRegex(@"\.SQL\s*\.\s*(Clear|Add|Text)\s*[(:=]", RegexOptions.IgnoreCase)]
    private static partial Regex SqlPropertyRegex();

    [GeneratedRegex(@"\.ExecSQL\b|\.Execute\b|\.Open\b|\.ExecQuery\b", RegexOptions.IgnoreCase)]
    private static partial Regex ExecMethodRegex();

    [GeneratedRegex(@"StartTransaction|Commit|Rollback|InTransaction", RegexOptions.IgnoreCase)]
    private static partial Regex TransactionRegex();

    [GeneratedRegex(@"'([^']*(?:''[^']*)*)'", RegexOptions.IgnoreCase)]
    private static partial Regex SqlStringLiteralRegex();

    [GeneratedRegex(@":\s*(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex SqlParameterRegex();

    [GeneratedRegex(@"ParamByName\s*\(\s*['\""](\w+)['\""]\\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex ParamByNameRegex();

    [GeneratedRegex(@"Params\s*\[\s*['\""](\w+)['\""]?\s*\]", RegexOptions.IgnoreCase)]
    private static partial Regex ParamsIndexerRegex();

    [GeneratedRegex(@"FieldByName\s*\(\s*['\""](\w+)['\""]\\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex FieldByNameRegex();

    [GeneratedRegex(@"(\w+)\s*:\s*(TQuery|TIBQuery|TADOQuery|TFDQuery|TZQuery|TSQLQuery|TIBDataSet|TADODataSet|TFDMemTable|TClientDataSet|TSQLDataSet)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QueryVariableDeclarationRegex();

    [GeneratedRegex(@"(\w+)\s*:=\s*(TQuery|TIBQuery|TADOQuery|TFDQuery|TZQuery|TSQLQuery|TIBDataSet|TADODataSet|TFDMemTable|TClientDataSet|TSQLDataSet)\.Create", RegexOptions.IgnoreCase)]
    private static partial Regex QueryCreationRegex();

    [GeneratedRegex(@"^\s*F?(\w+)\s*:\s*(TQuery|TIBQuery|TADOQuery|TFDQuery|TZQuery|TSQLQuery|TIBDataSet|TADODataSet|TFDMemTable|TClientDataSet|TSQLDataSet)\s*;", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex QueryFieldDeclarationRegex();

    [GeneratedRegex(@"\b(TIBStoredProc|TADOStoredProc|TFDStoredProc|TZStoredProc)\b", RegexOptions.IgnoreCase)]
    private static partial Regex StoredProcComponentRegex();

    [GeneratedRegex(@"(\w+)\.SQL\.(Text|Add|Clear)", RegexOptions.IgnoreCase)]
    private static partial Regex SqlAssignPatternRegex();

    // Match method implementation headers (procedure/function ClassName.MethodName)
    [GeneratedRegex(@"^\s*(procedure|function)\s+(\w+)\.(\w+)(?:\([^)]*\))?\s*(?::\s*\w+)?\s*;", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex MethodImplementationHeaderRegex();

    [GeneratedRegex(@"\.SQL\.Text\s*:=\s*'([^']*(?:''[^']*)*)'", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SqlTextAssignRegex();

    [GeneratedRegex(@"\.SQL\.Add\s*\(\s*'([^']*(?:''[^']*)*)'\s*\)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SqlAddRegex();

    [GeneratedRegex(@"(?:SQL\s*:=|:=\s*SQL\s*\+)\s*'([^']*(?:''[^']*)*)'", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SqlConcatRegex();

    // Fixed: Now handles quoted identifiers like "TELEPHONE" and schema-qualified names
    [GeneratedRegex(@"\bFROM\s+(?:""?\w+""?\.)?""?(\w+)""?", RegexOptions.IgnoreCase)]
    private static partial Regex TableFromRegex();

    // Fixed: Now handles quoted identifiers like "TELEPHONE"
    [GeneratedRegex(@"\bINTO\s+""?(\w+)""?", RegexOptions.IgnoreCase)]
    private static partial Regex TableIntoRegex();

    // Fixed: Now handles quoted identifiers and UPDATE OR INSERT INTO syntax
    [GeneratedRegex(@"\bUPDATE\s+(?:OR\s+INSERT\s+INTO\s+)?""?(\w+)""?", RegexOptions.IgnoreCase)]
    private static partial Regex TableUpdateRegex();

    // Fixed: Now handles quoted identifiers like "TELEPHONE"
    [GeneratedRegex(@"\bDELETE\s+FROM\s+""?(\w+)""?", RegexOptions.IgnoreCase)]
    private static partial Regex TableDeleteRegex();

    // For SET GENERATOR statements (Firebird specific)
    [GeneratedRegex(@"\bSET\s+GENERATOR\s+""?(\w+)""?\s+TO", RegexOptions.IgnoreCase)]
    private static partial Regex SetGeneratorRegex();

    // For CREATE/ALTER TABLE statements
    [GeneratedRegex(@"\bTABLE\s+""?(\w+)""?", RegexOptions.IgnoreCase)]
    private static partial Regex CreateTableRegex();

    // For CREATE INDEX statements (matches ON table_name)
    [GeneratedRegex(@"\bON\s+""?(\w+)""?\s*\(", RegexOptions.IgnoreCase)]
    private static partial Regex CreateIndexOnRegex();

    // FIX #1: Improved regex to capture full concatenated SQL assignment including variables
    [GeneratedRegex(@"\.SQL\.Text\s*:=\s*([^;]+);", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ConcatenatedSqlAssignRegex();

    [GeneratedRegex(@"\.SQL\.Add\s*\(\s*([^)]+)\s*\)", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ConcatenatedSqlAddRegex();

    // FIX #2: Pattern to detect SQL.Clear for splitting query blocks
    [GeneratedRegex(@"\.SQL\.Clear\b", RegexOptions.IgnoreCase)]
    private static partial Regex SqlClearRegex();

    // FIX #3: Patterns for direct ExecuteQuery/ExecSQL function calls
    [GeneratedRegex(@"\b(ExecuteQuery|ExecuteSQL|ExecQuery)\s*\(\s*\w+\s*,\s*'([^']*(?:''[^']*)*)'", RegexOptions.IgnoreCase)]
    private static partial Regex DirectExecuteWithConnRegex();

    [GeneratedRegex(@"\b(ExecuteQuery|ExecuteSQL|ExecQuery|ExecProc|ExecSQL)\s*\(\s*'([^']*(?:''[^']*)*)'", RegexOptions.IgnoreCase)]
    private static partial Regex DirectExecuteNoConnRegex();

    [GeneratedRegex(@"'(?<literal>[^']*(?:''[^']*)*)'|\+\s*(?<variable>[\w.]+(?:\s*\([^)]*\))?)", RegexOptions.IgnoreCase)]
    private static partial Regex ConcatPartRegex();

    [GeneratedRegex(@"^\w+\s*\(\s*(\w+)\s*\)$", RegexOptions.IgnoreCase)]
    private static partial Regex FunctionWrapperRegex();

    [GeneratedRegex(@"^(\w+)\.(\w+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PropertyAccessRegex();

    // FIX #4: Patterns for QueryValue helper functions (QueryValueAsInteger, QueryValueAsString, etc.)
    [GeneratedRegex(@"\b(QueryValueAs(?:Integer|String|Float|Boolean|DateTime|Variant)|QueryValue|GetFieldValue|LookupValue)\s*\(\s*'([^']*(?:''[^']*)*)'", RegexOptions.IgnoreCase)]
    private static partial Regex QueryValueFunctionRegex();

    // FIX #5: Pattern for SQL stored in variables - captures variable assignments with SQL
    [GeneratedRegex(@"\b(\w+)\s*:=\s*'((?:SELECT|INSERT|UPDATE|DELETE|EXEC)[^']*(?:''[^']*)*)'\s*;", RegexOptions.IgnoreCase)]
    private static partial Regex SqlVariableAssignRegex();

    // FIX #6: Pattern for SQL variable with concatenation
    [GeneratedRegex(@"\b(\w+)\s*:=\s*'((?:SELECT|INSERT|UPDATE|DELETE|EXEC)[^;]*)'(?:\s*\+[^;]+)?;", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SqlVariableConcatAssignRegex();

    // FIX #7: Pattern for function calls with variable (ExecuteQuery(S), ExecSQL(MySql), etc.)
    [GeneratedRegex(@"\b(ExecuteQuery|ExecuteSQL|ExecQuery|ExecProc|ExecSQL)\s*\(\s*(\w+)\s*\)", RegexOptions.IgnoreCase)]
    private static partial Regex ExecuteWithVariableRegex();

    // FIX #8: Pattern for QueryValue functions with concatenated SQL
    [GeneratedRegex(@"\b(QueryValueAs(?:Integer|String|Float|Boolean|DateTime|Variant)|QueryValue|GetFieldValue)\s*\(\s*([^,]+),", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex QueryValueConcatRegex();

    #endregion

    /// <summary>
    /// List of known query component types for detection.
    /// </summary>
    private static readonly string[] KnownQueryTypes =
    [
        "TQuery",
        "TIBQuery",
        "TADOQuery",
        "TFDQuery",
        "TZQuery",
        "TSQLQuery",
        "TIBDataSet",
        "TADODataSet",
        "TFDMemTable",
        "TClientDataSet",
        "TSQLDataSet",
        "TIBStoredProc",
        "TADOStoredProc",
        "TFDStoredProc",
        "TZStoredProc"
    ];

    /// <summary>
    /// Analyzes Delphi source code to detect if it contains database operations.
    /// Updated to include direct ExecuteQuery calls (FIX #3) and QueryValue functions (FIX #4).
    /// </summary>
    public static bool ContainsDatabaseOperations(string sourceCode)
    {
        return QueryComponentRegex().IsMatch(sourceCode) ||
               DatabaseConnectionRegex().IsMatch(sourceCode) ||
               SqlPropertyRegex().IsMatch(sourceCode) ||
               TransactionRegex().IsMatch(sourceCode) ||
               StoredProcComponentRegex().IsMatch(sourceCode) ||
               DirectExecuteWithConnRegex().IsMatch(sourceCode) ||  // FIX #3
               DirectExecuteNoConnRegex().IsMatch(sourceCode) ||    // FIX #3
               QueryValueFunctionRegex().IsMatch(sourceCode) ||     // FIX #4
               SqlVariableAssignRegex().IsMatch(sourceCode);        // FIX #5
    }

    /// <summary>
    /// Extracts all query component types found in the source code.
    /// </summary>
    public static List<string> ExtractQueryComponentTypes(string sourceCode)
    {
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var queryType in KnownQueryTypes)
        {
            var pattern = new Regex($@"\b{Regex.Escape(queryType)}\b", RegexOptions.IgnoreCase);
            if (pattern.IsMatch(sourceCode))
            {
                types.Add(queryType);
            }
        }

        return [.. types];
    }

    /// <summary>
    /// Extracts query variable declarations and their types.
    /// Returns a dictionary mapping variable name to component type.
    /// </summary>
    public static Dictionary<string, string> ExtractQueryVariables(string sourceCode)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in QueryVariableDeclarationRegex().Matches(sourceCode))
        {
            var varName = match.Groups[1].Value;
            var typeName = match.Groups[2].Value;
            variables[varName] = NormalizeQueryType(typeName);
        }

        foreach (Match match in QueryCreationRegex().Matches(sourceCode))
        {
            var varName = match.Groups[1].Value;
            var typeName = match.Groups[2].Value;
            variables[varName] = NormalizeQueryType(typeName);
        }

        foreach (Match match in QueryFieldDeclarationRegex().Matches(sourceCode))
        {
            var varName = match.Groups[1].Value;
            var typeName = match.Groups[2].Value;
            variables[varName] = NormalizeQueryType(typeName);
        }

        return variables;
    }

    /// <summary>
    /// Normalizes a query type name to its canonical form.
    /// </summary>
    private static string NormalizeQueryType(string typeName)
    {
        foreach (var known in KnownQueryTypes)
        {
            if (string.Equals(typeName, known, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }
        return typeName;
    }

    /// <summary>
    /// Determines the query component type from the context of a SQL statement.
    /// </summary>
    public static string? DetermineQueryTypeFromContext(string methodBody, Dictionary<string, string> queryVariables)
    {
        var matches = SqlAssignPatternRegex().Matches(methodBody);

        foreach (Match match in matches)
        {
            var varName = match.Groups[1].Value;
            if (queryVariables.TryGetValue(varName, out var queryType))
            {
                return queryType;
            }
        }

        if (queryVariables.Count == 1)
        {
            return queryVariables.Values.First();
        }

        return null;
    }

    /// <summary>
    /// Extracts all database operations with their component types from Delphi source code.
    /// Now uses the robust SqlExtractionService.
    /// </summary>
    public static List<(DatabaseOperation Operation, string ComponentType)> ExtractOperationsWithTypes(string sourceCode, string unitName)
    {
        var results = new List<(DatabaseOperation Operation, string ComponentType)>();

        var queryVariables = ExtractQueryVariables(sourceCode);
        var componentTypes = ExtractQueryComponentTypes(sourceCode);
        var defaultType = componentTypes.Count > 0 ? componentTypes[0] : "TQuery";

        // Find all method implementation headers
        var headerMatches = MethodImplementationHeaderRegex().Matches(sourceCode);

        foreach (Match headerMatch in headerMatches)
        {
            var className = headerMatch.Groups[2].Value;
            var methodName = headerMatch.Groups[3].Value;
            var headerEndIndex = headerMatch.Index + headerMatch.Length;

            // Extract the method body by finding the matching 'end;'
            var (methodBody, bodyEndIndex) = ExtractMethodBody(sourceCode, headerEndIndex);

            if (string.IsNullOrEmpty(methodBody))
                continue;

            // Strip comments before analyzing for database operations
            var uncommentedBody = StripDelphiComments(methodBody);

            if (!ContainsDatabaseOperations(uncommentedBody))
                continue;

            // Calculate method start line for absolute line number calculation
            var methodStartLine = CountLines(sourceCode, 0, headerMatch.Index);

            // Use the new robust SQL extraction service
            var extractedQueries = SqlExtractionService.ExtractQueriesFromMethod(methodBody, methodStartLine);

            if (extractedQueries.Count == 0)
                continue;

            var isTransaction = TransactionRegex().IsMatch(uncommentedBody);
            var transactionGroupId = isTransaction ? Guid.NewGuid().ToString("N")[..8] : null;

            // Extract field accesses for DTO generation
            var fieldAccesses = FieldAccessAnalyser.ExtractFieldAccesses(uncommentedBody);
            fieldAccesses = FieldAccessAnalyser.RefineFieldTypes(uncommentedBody, fieldAccesses);

            foreach (var query in extractedQueries)
            {
                var componentType = DetermineQueryTypeFromContext(uncommentedBody, queryVariables) ?? defaultType;

                // For dynamic SQL, we store "Dynamic SQL" in the SqlStatement field
                var sqlText = query.SqlText;

                // Only try to extract metadata if it's not dynamic SQL
                DatabaseOperationType operationType = DatabaseOperationType.Unknown;
                string? tableName = null;
                List<SqlParameter> parameters = [];

                if (!query.IsDynamic)
                {
                    // Normalize SQL: remove unnecessary quotes, uppercase keywords, PascalCase parameters
                    sqlText = NormalizeSqlQuotes(sqlText);
                    sqlText = NormalizeSqlKeywords(sqlText);
                    sqlText = NormalizeParameterCase(sqlText);

                    // Quote reserved words in the SQL before storing
                    sqlText = SqlQuoter.QuoteReservedWords(sqlText);
                    operationType = DetermineOperationType(sqlText);
                    tableName = ExtractTableName(sqlText);
                    parameters = ExtractParameters(uncommentedBody, sqlText);

                    // Rewrite SELECT * to use specific columns if we have field accesses
                    if (operationType == DatabaseOperationType.Select && fieldAccesses.Count > 0)
                    {
                        var rewritten = FieldAccessAnalyser.RewriteSelectStar(sqlText, fieldAccesses);
                        if (rewritten != sqlText)
                        {
                            // Rewritten successfully
                            sqlText = rewritten;
                        }
                    }
                }

                var operation = new DatabaseOperation
                {
                    MethodName = methodName,
                    ContainingClass = className,
                    UnitName = unitName,
                    SqlStatement = sqlText,
                    OperationType = operationType,
                    TableName = tableName,
                    Parameters = parameters,
                    IsPartOfTransaction = isTransaction,
                    TransactionGroupId = transactionGroupId,
                    OriginalDelphiCode = methodBody,  // Keep original for reference
                    FieldAccesses = fieldAccesses,
                    SourceLineNumber = query.LineNumber
                };

                results.Add((operation, componentType));
            }
        }

        return results;
    }

    /// <summary>
    /// Extracts method body starting from the position after the method header.
    /// Handles nested begin/end blocks properly.
    /// </summary>
    private static (string methodBody, int endPosition) ExtractMethodBody(string source, int startIndex)
    {
        if (startIndex >= source.Length)
            return (string.Empty, source.Length);

        // Find the first 'begin'
        int beginCount = 0;
        int endCount = 0;
        int i = startIndex;

        // Skip to first 'begin'
        while (i < source.Length - 4)
        {
            if (source.Substring(i, 5).Equals("begin", StringComparison.OrdinalIgnoreCase))
            {
                // Check if this is a word boundary (not part of another word)
                if (i == 0 || !char.IsLetterOrDigit(source[i - 1]))
                {
                    if (i + 5 >= source.Length || !char.IsLetterOrDigit(source[i + 5]))
                    {
                        beginCount++;
                        i += 5;
                        break;
                    }
                }
            }
            i++;
        }

        if (beginCount == 0)
        {
            // No begin found - this might be a forward declaration
            return (string.Empty, startIndex);
        }

        // Now scan for matching 'end;'
        while (i < source.Length - 2)
        {
            // Check for 'begin'
            if (i + 5 <= source.Length && source.Substring(i, 5).Equals("begin", StringComparison.OrdinalIgnoreCase))
            {
                // Check word boundaries
                if (i == 0 || !char.IsLetterOrDigit(source[i - 1]))
                {
                    if (i + 5 >= source.Length || !char.IsLetterOrDigit(source[i + 5]))
                    {
                        beginCount++;
                        i += 5;
                        continue;
                    }
                }
            }

            // Check for 'end'
            if (i + 3 <= source.Length && source.Substring(i, 3).Equals("end", StringComparison.OrdinalIgnoreCase))
            {
                // Check word boundaries
                if (i == 0 || !char.IsLetterOrDigit(source[i - 1]))
                {
                    endCount++;

                    // Check if this 'end' is followed by ';'
                    int j = i + 3;
                    while (j < source.Length && char.IsWhiteSpace(source[j]))
                        j++;

                    if (j < source.Length && source[j] == ';')
                    {
                        // If beginCount == endCount, this is our method's end
                        if (beginCount == endCount)
                        {
                            string body = source.Substring(startIndex, j - startIndex + 1);
                            return (body, j + 1);
                        }
                    }

                    i += 3;
                    continue;
                }
            }

            i++;
        }

        // Couldn't find matching end, return what we have
        return (source[startIndex..], source.Length);
    }

    /// <summary>
    /// Strips Delphi comments from source code.
    /// Handles: // line comments, { } block comments, (* *) block comments
    /// </summary>
    private static string StripDelphiComments(string source)
    {
        var result = new System.Text.StringBuilder(source.Length);
        int i = 0;
        bool inString = false;

        while (i < source.Length)
        {
            // Handle string literals - don't strip inside strings
            if (source[i] == '\'' && !inString)
            {
                inString = true;
                result.Append(source[i]);
                i++;
                continue;
            }

            if (source[i] == '\'' && inString)
            {
                // Check for escaped quote ''
                if (i + 1 < source.Length && source[i + 1] == '\'')
                {
                    result.Append("''");
                    i += 2;
                    continue;
                }
                inString = false;
                result.Append(source[i]);
                i++;
                continue;
            }

            if (inString)
            {
                result.Append(source[i]);
                i++;
                continue;
            }

            // Handle // line comments
            if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '/')
            {
                // Skip to end of line
                while (i < source.Length && source[i] != '\n')
                {
                    i++;
                }
                // Keep the newline
                if (i < source.Length)
                {
                    result.Append(source[i]);
                    i++;
                }
                continue;
            }

            // Handle { } block comments
            if (source[i] == '{')
            {
                // Skip until closing }
                while (i < source.Length && source[i] != '}')
                {
                    i++;
                }
                // Skip the closing }
                if (i < source.Length)
                {
                    i++;
                }
                continue;
            }

            // Handle (* *) block comments
            if (i + 1 < source.Length && source[i] == '(' && source[i + 1] == '*')
            {
                i += 2;
                // Skip until closing *)
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == ')'))
                {
                    i++;
                }
                // Skip the closing *)
                if (i + 1 < source.Length)
                {
                    i += 2;
                }
                continue;
            }

            // Regular character - keep it
            result.Append(source[i]);
            i++;
        }

        return result.ToString();
    }

    /// <summary>
    /// Extracts all database operations from Delphi source code.
    /// Now uses the robust SqlExtractionService.
    /// </summary>
    public static List<DatabaseOperation> ExtractOperations(string sourceCode, string unitName)
    {
        var operations = new List<DatabaseOperation>();

        // Find all method implementation headers
        var headerMatches = MethodImplementationHeaderRegex().Matches(sourceCode);

        foreach (Match headerMatch in headerMatches)
        {
            var className = headerMatch.Groups[2].Value;
            var methodName = headerMatch.Groups[3].Value;
            var headerEndIndex = headerMatch.Index + headerMatch.Length;

            // Extract the method body by finding the matching 'end;'
            var (methodBody, bodyEndIndex) = ExtractMethodBody(sourceCode, headerEndIndex);

            if (string.IsNullOrEmpty(methodBody))
                continue;

            // Strip comments before analyzing for database operations
            var uncommentedBody = StripDelphiComments(methodBody);

            if (!ContainsDatabaseOperations(uncommentedBody))
                continue;

            // Calculate method start line for absolute line number calculation
            var methodStartLine = CountLines(sourceCode, 0, headerMatch.Index);

            // Use the new robust SQL extraction service
            var extractedQueries = SqlExtractionService.ExtractQueriesFromMethod(methodBody, methodStartLine);

            if (extractedQueries.Count == 0)
                continue;

            var isTransaction = TransactionRegex().IsMatch(uncommentedBody);
            var transactionGroupId = isTransaction ? Guid.NewGuid().ToString("N")[..8] : null;

            // Extract field accesses for DTO generation
            var fieldAccesses = FieldAccessAnalyser.ExtractFieldAccesses(uncommentedBody);
            fieldAccesses = FieldAccessAnalyser.RefineFieldTypes(uncommentedBody, fieldAccesses);

            foreach (var query in extractedQueries)
            {
                // For dynamic SQL, we store "Dynamic SQL" in the SqlStatement field
                var sqlText = query.SqlText;

                // Only try to extract metadata if it's not dynamic SQL
                DatabaseOperationType operationType = DatabaseOperationType.Unknown;
                string? tableName = null;
                List<SqlParameter> parameters = [];

                if (!query.IsDynamic)
                {
                    // Normalize SQL: remove unnecessary quotes, uppercase keywords, PascalCase parameters
                    sqlText = NormalizeSqlQuotes(sqlText);
                    sqlText = NormalizeSqlKeywords(sqlText);
                    sqlText = NormalizeParameterCase(sqlText);

                    operationType = DetermineOperationType(sqlText);
                    tableName = ExtractTableName(sqlText);
                    parameters = ExtractParameters(uncommentedBody, sqlText);

                    // Rewrite SELECT * to use specific columns if we have field accesses
                    if (operationType == DatabaseOperationType.Select && fieldAccesses.Count > 0)
                    {
                        var rewritten = FieldAccessAnalyser.RewriteSelectStar(sqlText, fieldAccesses);
                        if (rewritten != sqlText)
                        {
                            // Rewritten successfully
                            sqlText = rewritten;
                        }
                    }
                }

                var operation = new DatabaseOperation
                {
                    MethodName = methodName,
                    ContainingClass = className,
                    UnitName = unitName,
                    SqlStatement = sqlText,
                    OperationType = operationType,
                    TableName = tableName,
                    Parameters = parameters,
                    IsPartOfTransaction = isTransaction,
                    TransactionGroupId = transactionGroupId,
                    OriginalDelphiCode = methodBody,
                    FieldAccesses = fieldAccesses,
                    SourceLineNumber = query.LineNumber
                };

                operations.Add(operation);
            }
        }

        return operations;
    }

    /// <summary>
    /// Groups database operations into transaction groups.
    /// </summary>
    public static List<TransactionGroup> GroupByTransaction(List<DatabaseOperation> operations)
    {
        var groups = new List<TransactionGroup>();

        var transactionOps = operations
            .Where(o => o.IsPartOfTransaction && o.TransactionGroupId != null)
            .GroupBy(o => o.TransactionGroupId);

        foreach (var group in transactionOps)
        {
            var first = group.First();
            groups.Add(new TransactionGroup
            {
                GroupId = group.Key!,
                MethodName = first.MethodName,
                ContainingClass = first.ContainingClass,
                Operations = [.. group],
                OriginalDelphiCode = first.OriginalDelphiCode
            });
        }

        return groups;
    }

    /// <summary>
    /// Extracts field names accessed in the code (for DTO generation).
    /// </summary>
    public static List<string> ExtractFieldNames(string sourceCode)
    {
        var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var matches = FieldByNameRegex().Matches(sourceCode);
        foreach (Match match in matches)
        {
            fields.Add(match.Groups[1].Value);
        }

        return [.. fields];
    }

    /// <summary>
    /// Extracts SQL statements from a method body.
    /// UPDATED: Now handles SQL.Clear blocks (FIX #2), direct ExecuteQuery calls (FIX #3),
    /// and improved concatenation parsing (FIX #1).
    /// </summary>
    private static List<(string Statement, int Position)> ExtractSqlStatements(string methodBody)
    {
        var statements = new List<(string Statement, int Position)>();

        // FIX #1: Handle concatenated SQL assignments with variables
        // Pattern: .SQL.Text := 'part1' + Variable + 'part2' + ...
        var concatAssignMatches = ConcatenatedSqlAssignRegex().Matches(methodBody);
        foreach (Match match in concatAssignMatches)
        {
            var expression = match.Groups[1].Value;
            var startPos = match.Index;

            // Check if this is a simple literal or a concatenated expression
            if (expression.Contains('+'))
            {
                // Concatenated - parse and convert variables to parameters
                var sql = ParseConcatenatedSql(expression);
                if (!string.IsNullOrWhiteSpace(sql) && !statements.Exists(s => s.Statement == sql))
                {
                    sql = NormalizeSqlQuotes(sql);
                    sql = NormalizeSqlKeywords(sql);
                    sql = NormalizeParameterCase(sql);
                    statements.Add((sql, startPos));
                }
            }
            else
            {
                // Simple literal assignment - extract the string content
                var literalMatch = SqlStringLiteralRegex().Match(expression);
                if (literalMatch.Success)
                {
                    var sql = literalMatch.Groups[1].Value.Replace("''", "'");
                    sql = NormalizeSqlQuotes(sql);
                    sql = NormalizeSqlKeywords(sql);
                    sql = NormalizeParameterCase(sql);
                    if (!string.IsNullOrWhiteSpace(sql) && !statements.Exists(s => s.Statement == sql))
                    {
                        statements.Add((sql, startPos));
                    }
                }
            }
        }

        // FIX #2: Handle SQL.Add blocks - split by SQL.Clear
        var clearMatches = SqlClearRegex().Matches(methodBody);
        var allAddMatches = SqlAddRegex().Matches(methodBody).Cast<Match>().ToList();

        if (clearMatches.Count > 0 && allAddMatches.Count > 0)
        {
            // Get positions of all SQL.Clear calls
            var clearPositions = clearMatches.Cast<Match>()
                .Select(m => m.Index)
                .OrderBy(p => p)
                .ToList();

            // Add end of method as final boundary
            clearPositions.Add(methodBody.Length);

            // Group SQL.Add calls by which SQL.Clear block they belong to
            for (int i = 0; i < clearPositions.Count - 1; i++)
            {
                var startBoundary = clearPositions[i];
                var endBoundary = clearPositions[i + 1];

                // Find SQL.Add calls between this Clear and the next Clear (or end)
                var addCallsInBlock = allAddMatches
                    .Where(m => m.Index > startBoundary && m.Index < endBoundary)
                    .ToList();

                if (addCallsInBlock.Count > 0)
                {
                    var currentSql = new System.Text.StringBuilder();
                    var startPos = addCallsInBlock[0].Index;

                    foreach (var addMatch in addCallsInBlock)
                    {
                        var line = addMatch.Groups[1].Value.Replace("''", "'");
                        if (currentSql.Length > 0)
                            currentSql.AppendLine();
                        currentSql.Append(line);
                    }

                    var sqlText = currentSql.ToString().Trim();
                    sqlText = NormalizeSqlQuotes(sqlText);
                    sqlText = NormalizeSqlKeywords(sqlText);
                    sqlText = NormalizeParameterCase(sqlText);
                    if (!string.IsNullOrWhiteSpace(sqlText) &&
                        !statements.Exists(s => s.Statement == sqlText))
                    {
                        statements.Add((sqlText, startPos));
                    }
                }
            }
        }
        else if (allAddMatches.Count > 0)
        {
            // No SQL.Clear found - treat all Add calls as one statement (original behavior)
            var currentSql = new System.Text.StringBuilder();
            var startPos = allAddMatches[0].Index;

            foreach (var match in allAddMatches)
            {
                var line = match.Groups[1].Value.Replace("''", "'");
                if (currentSql.Length > 0)
                    currentSql.AppendLine();
                currentSql.Append(line);
            }

            var sqlText = currentSql.ToString().Trim();
            sqlText = NormalizeSqlQuotes(sqlText);
            sqlText = NormalizeSqlKeywords(sqlText);
            sqlText = NormalizeParameterCase(sqlText);
            if (!string.IsNullOrWhiteSpace(sqlText) &&
                !statements.Exists(s => s.Statement == sqlText))
            {
                statements.Add((sqlText, startPos));
            }
        }

        // Handle concatenated SQL.Add patterns (with variable interpolation)
        var concatAddMatches = ConcatenatedSqlAddRegex().Matches(methodBody);
        foreach (Match match in concatAddMatches)
        {
            var expression = match.Groups[1].Value;
            if (expression.Contains('+'))
            {
                var sql = ParseConcatenatedSql(expression);
                sql = NormalizeSqlQuotes(sql);
                sql = NormalizeSqlKeywords(sql);
                sql = NormalizeParameterCase(sql);
                if (!string.IsNullOrWhiteSpace(sql) && !statements.Exists(s => s.Statement.Contains(sql)))
                {
                    statements.Add((sql, match.Index));
                }
            }
        }

        // Handle SQL := or := SQL + patterns
        foreach (Match match in SqlConcatRegex().Matches(methodBody))
        {
            var sql = match.Groups[1].Value.Replace("''", "'");
            sql = NormalizeSqlQuotes(sql);
            sql = NormalizeSqlKeywords(sql);
            sql = NormalizeParameterCase(sql);
            if (!string.IsNullOrWhiteSpace(sql) && !statements.Exists(s => s.Statement == sql))
            {
                statements.Add((sql, match.Index));
            }
        }

        // FIX #3: Handle direct ExecuteQuery/ExecSQL function calls
        foreach (Match match in DirectExecuteWithConnRegex().Matches(methodBody))
        {
            var sql = match.Groups[2].Value.Replace("''", "'");
            sql = NormalizeSqlQuotes(sql);
            sql = NormalizeSqlKeywords(sql);
            sql = NormalizeParameterCase(sql);
            if (!string.IsNullOrWhiteSpace(sql) && !statements.Exists(s => s.Statement == sql))
            {
                statements.Add((sql, match.Index));
            }
        }

        foreach (Match match in DirectExecuteNoConnRegex().Matches(methodBody))
        {
            var sql = match.Groups[2].Value.Replace("''", "'");
            sql = NormalizeSqlQuotes(sql);
            sql = NormalizeSqlKeywords(sql);
            sql = NormalizeParameterCase(sql);
            if (!string.IsNullOrWhiteSpace(sql) && !statements.Exists(s => s.Statement == sql))
            {
                statements.Add((sql, match.Index));
            }
        }

        // FIX #4: Handle QueryValue helper functions
        foreach (Match match in QueryValueFunctionRegex().Matches(methodBody))
        {
            var sql = match.Groups[2].Value.Replace("''", "'");
            sql = NormalizeSqlQuotes(sql);
            sql = NormalizeSqlKeywords(sql);
            sql = NormalizeParameterCase(sql);
            if (!string.IsNullOrWhiteSpace(sql) && !statements.Exists(s => s.Statement == sql))
            {
                statements.Add((sql, match.Index));
            }
        }

        return statements;
    }

    /// <summary>
    /// Parses concatenated SQL expressions and converts Delphi variables to SQL parameters.
    /// Handles patterns like: 'SELECT * FROM ' + TableName + ' WHERE ID = ' + IntToStr(ID)
    /// </summary>
    private static string ParseConcatenatedSql(string expression)
    {
        var result = new System.Text.StringBuilder();

        // Match all parts: string literals and variables/expressions
        var matches = ConcatPartRegex().Matches(expression);

        foreach (Match match in matches)
        {
            if (match.Groups["literal"].Success)
            {
                // It's a string literal - append it (replacing '' with ')
                var literal = match.Groups["literal"].Value.Replace("''", "'");
                result.Append(literal);
            }
            else if (match.Groups["variable"].Success)
            {
                // It's a variable or expression - convert to parameter
                var variable = match.Groups["variable"].Value.Trim();

                // Extract the core variable name from expressions like IntToStr(ID) or QuotedStr(Name)
                var coreVar = ExtractCoreVariableName(variable);

                // Use : prefix for Firebird/Interbase style parameters
                result.Append($":{coreVar}");
            }
        }

        return result.ToString().Trim();
    }

    /// <summary>
    /// Extracts the core variable name from expressions like IntToStr(ID), QuotedStr(Name), etc.
    /// </summary>
    private static string ExtractCoreVariableName(string expression)
    {
        // Check for function wrappers like IntToStr(VarName), QuotedStr(VarName), etc.
        var funcMatch = FunctionWrapperRegex().Match(expression);
        if (funcMatch.Success)
        {
            return funcMatch.Groups[1].Value;
        }

        // Check for simple variable access like MyObject.PropertyName
        var dotMatch = PropertyAccessRegex().Match(expression);
        if (dotMatch.Success)
        {
            // Return just the property name part
            return dotMatch.Groups[2].Value;
        }

        // Return as-is (simple variable name)
        return expression.Trim();
    }

    /// <summary>
    /// Removes unnecessary double quotes from SQL identifiers.
    /// Keeps quotes only for SQL reserved words.
    /// </summary>
    private static string NormalizeSqlQuotes(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return sql;

        // Common SQL reserved words (add more as needed)
        var reservedWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE", "INTO", "VALUES",
            "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "ON", "AND", "OR", "NOT", "IN",
            "AS", "ORDER", "BY", "GROUP", "HAVING", "UNION", "ALL", "DISTINCT",
            "CREATE", "ALTER", "DROP", "TABLE", "INDEX", "VIEW", "DATABASE",
            "SET", "DEFAULT", "NULL", "PRIMARY", "KEY", "FOREIGN", "REFERENCES",
            "USER", "CURRENT", "DATE", "TIME", "TIMESTAMP", "YEAR", "MONTH", "DAY",
            "CHAR", "VARCHAR", "INTEGER", "DECIMAL", "NUMERIC", "FLOAT", "DOUBLE",
            "BOOLEAN", "BLOB", "TEXT", "VALUE", "POSITION", "SIZE", "LEVEL", "OPTION"
        };

        // Pattern to match quoted identifiers: "IDENTIFIER"
        // Handles both single identifiers and qualified ones like "TABLE"."COLUMN"
        var pattern = @"""([A-Za-z_][A-Za-z0-9_]*)""";

        return Regex.Replace(sql, pattern, match =>
        {
            var identifier = match.Groups[1].Value;

            // Keep quotes if it's a reserved word
            if (reservedWords.Contains(identifier))
                return match.Value; // Keep the quotes

            // Remove quotes for regular identifiers
            return identifier;
        });
    }

    /// <summary>
    /// Normalizes SQL by uppercasing all SQL keywords.
    /// </summary>
    private static string NormalizeSqlKeywords(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return sql;

        // List of SQL keywords to uppercase
        var keywords = new[]
        {
            "SELECT", "FROM", "WHERE", "INSERT", "UPDATE", "DELETE", "INTO", "VALUES",
            "JOIN", "LEFT", "RIGHT", "INNER", "OUTER", "FULL", "CROSS", "ON", "USING",
            "AND", "OR", "NOT", "IN", "EXISTS", "BETWEEN", "LIKE", "IS", "NULL",
            "AS", "ORDER", "BY", "GROUP", "HAVING", "UNION", "ALL", "DISTINCT", "TOP",
            "CREATE", "ALTER", "DROP", "TABLE", "INDEX", "VIEW", "DATABASE", "SCHEMA",
            "SET", "DEFAULT", "PRIMARY", "KEY", "FOREIGN", "REFERENCES", "CONSTRAINT",
            "CHECK", "UNIQUE", "CASCADE", "RESTRICT", "NO", "ACTION",
            "BEGIN", "COMMIT", "ROLLBACK", "TRANSACTION", "SAVEPOINT",
            "CASE", "WHEN", "THEN", "ELSE", "END",
            "CAST", "CONVERT", "COALESCE", "NULLIF", "IIF",
            "COUNT", "SUM", "AVG", "MIN", "MAX", "FIRST", "LAST",
            "LIMIT", "OFFSET", "FETCH", "ROWS", "ONLY", "NEXT",
            "WITH", "RECURSIVE", "CTE",
            "OVER", "PARTITION", "WINDOW", "ROW_NUMBER", "RANK", "DENSE_RANK",
            "ASC", "DESC",
            "FOR", "EXECUTE", "PROCEDURE", "BLOCK", "RETURNS", "RETURNING"
        };

        foreach (var keyword in keywords)
        {
            // Use word boundaries to avoid partial matches
            // Case-insensitive matching, replace with uppercase
            var pattern = $@"\b{Regex.Escape(keyword)}\b";
            sql = Regex.Replace(sql, pattern, keyword.ToUpperInvariant(), RegexOptions.IgnoreCase);
        }

        return sql;
    }

    /// <summary>
    /// Normalizes parameter names to PascalCase.
    /// Converts :PATIENT_ID to :PatientId
    /// </summary>
    private static string NormalizeParameterCase(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return sql;

        // Match parameter patterns like :PARAMETER_NAME or :ParameterName
        var pattern = @":([A-Z_][A-Z0-9_]*)";

        return Regex.Replace(sql, pattern, match =>
        {
            var paramName = match.Groups[1].Value;

            // Convert to PascalCase
            var pascalCase = ConvertToPascalCase(paramName);
            return $":{pascalCase}";
        }, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Converts a string to PascalCase.
    /// Examples: PATIENT_ID -> PatientId, patient_id -> PatientId, PatientID -> PatientId
    /// </summary>
    private static string ConvertToPascalCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        // Split by underscore
        var parts = input.Split('_', StringSplitOptions.RemoveEmptyEntries);

        var result = new System.Text.StringBuilder();

        foreach (var part in parts)
        {
            if (part.Length == 0)
                continue;

            // Capitalize first letter, lowercase the rest
            result.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1)
            {
                result.Append(part.Substring(1).ToLowerInvariant());
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Counts the number of newlines in a string from start to end position.
    /// Used to calculate line numbers from character positions.
    /// </summary>
    private static int CountLines(string text, int start, int end)
    {
        if (string.IsNullOrEmpty(text) || end <= start || start < 0)
            return 1;

        end = Math.Min(end, text.Length);
        int lineCount = 1;

        for (int i = start; i < end; i++)
        {
            if (text[i] == '\n')
                lineCount++;
        }

        return lineCount;
    }

    private static DatabaseOperationType DetermineOperationType(string sql)
    {
        var trimmed = sql.TrimStart().ToUpperInvariant();

        if (trimmed.StartsWith("SELECT")) return DatabaseOperationType.Select;
        if (trimmed.StartsWith("INSERT")) return DatabaseOperationType.Insert;
        if (trimmed.StartsWith("UPDATE")) return DatabaseOperationType.Update;
        if (trimmed.StartsWith("DELETE")) return DatabaseOperationType.Delete;
        if (trimmed.StartsWith("CREATE")) return DatabaseOperationType.DDL;
        if (trimmed.StartsWith("ALTER")) return DatabaseOperationType.DDL;
        if (trimmed.StartsWith("DROP")) return DatabaseOperationType.DDL;
        if (trimmed.StartsWith("EXEC") || trimmed.StartsWith("CALL")) return DatabaseOperationType.StoredProcedure;

        return DatabaseOperationType.Unknown;
    }

    private static string? ExtractTableName(string sql)
    {
        var fromMatch = TableFromRegex().Match(sql);
        if (fromMatch.Success) return fromMatch.Groups[1].Value.ToUpperInvariant();

        var intoMatch = TableIntoRegex().Match(sql);
        if (intoMatch.Success) return intoMatch.Groups[1].Value.ToUpperInvariant();

        var updateMatch = TableUpdateRegex().Match(sql);
        if (updateMatch.Success) return updateMatch.Groups[1].Value.ToUpperInvariant();

        var deleteMatch = TableDeleteRegex().Match(sql);
        if (deleteMatch.Success) return deleteMatch.Groups[1].Value.ToUpperInvariant();

        // For SET GENERATOR statements (Firebird specific)
        var setGeneratorMatch = SetGeneratorRegex().Match(sql);
        if (setGeneratorMatch.Success) return setGeneratorMatch.Groups[1].Value.ToUpperInvariant();

        // For CREATE/ALTER TABLE statements
        var createTableMatch = CreateTableRegex().Match(sql);
        if (createTableMatch.Success) return createTableMatch.Groups[1].Value.ToUpperInvariant();

        // For CREATE INDEX statements
        var createIndexMatch = CreateIndexOnRegex().Match(sql);
        if (createIndexMatch.Success) return createIndexMatch.Groups[1].Value.ToUpperInvariant();

        return null;
    }

    private static List<SqlParameter> ExtractParameters(string methodBody, string sql)
    {
        var parameters = new List<SqlParameter>();
        var paramNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var sqlParams = SqlParameterRegex().Matches(sql);
        foreach (Match match in sqlParams)
        {
            paramNames.Add(match.Groups[1].Value);
        }

        var paramByName = ParamByNameRegex().Matches(methodBody);
        foreach (Match match in paramByName)
        {
            paramNames.Add(match.Groups[1].Value);
        }

        var paramsIndexer = ParamsIndexerRegex().Matches(methodBody);
        foreach (Match match in paramsIndexer)
        {
            paramNames.Add(match.Groups[1].Value);
        }

        foreach (var paramName in paramNames)
        {
            var (delphiType, csharpType) = InferParameterType(methodBody, paramName);
            parameters.Add(new SqlParameter
            {
                Name = paramName,
                DelphiType = delphiType,
                CSharpType = csharpType
            });
        }

        return parameters;
    }

    private static (string delphiType, string csharpType) InferParameterType(string methodBody, string paramName)
    {
        var typePatterns = new Dictionary<string, (string delphi, string csharp)>
        {
            { $@"ParamByName\s*\(\s*['""{paramName}['""]\s*\)\s*\.AsString", ("String", "string") },
            { $@"ParamByName\s*\(\s*['""{paramName}['""]\s*\)\s*\.AsInteger", ("Integer", "int") },
            { $@"ParamByName\s*\(\s*['""{paramName}['""]\s*\)\s*\.AsFloat", ("Double", "double") },
            { $@"ParamByName\s*\(\s*['""{paramName}['""]\s*\)\s*\.AsBoolean", ("Boolean", "bool") },
            { $@"ParamByName\s*\(\s*['""{paramName}['""]\s*\)\s*\.AsDateTime", ("TDateTime", "DateTime") },
            { $@"ParamByName\s*\(\s*['""{paramName}['""]\s*\)\s*\.AsCurrency", ("Currency", "decimal") },
            { $@"ParamByName\s*\(\s*['""{paramName}['""]\s*\)\s*\.AsBlob", ("TBlob", "byte[]") },
            { $@"ParamByName\s*\(\s*['""{paramName}['""]\s*\)\s*\.Value", ("Variant", "object") }
        };

        foreach (var pattern in typePatterns)
        {
            if (Regex.IsMatch(methodBody, pattern.Key, RegexOptions.IgnoreCase))
            {
                return pattern.Value;
            }
        }

        return ("Variant", "object");
    }
}