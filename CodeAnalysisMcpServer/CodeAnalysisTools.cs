using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodeAnalysisMcpServer
{
    public partial class CodeAnalysisTools
    {
        public static Task<object> ParseDelphiFile(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;

            if (!File.Exists(path))
                throw new FileNotFoundException($"File not found: {path}");

            var content = File.ReadAllText(path);
            var analysis = new
            {
                fileName = Path.GetFileName(path),
                filePath = path,
                lineCount = content.Split('\n').Length,
                units = ExtractDelphiUnits(content),
                classes = ExtractDelphiClasses(content),
                procedures = ExtractDelphiProcedures(content),
                functions = ExtractDelphiFunctions(content),
                usesClause = ExtractUsesClause(content)
            };

            return Task.FromResult<object>(new
            {
                success = true,
                analysis
            });
        }

        public static Task<object> ExtractSqlStatements(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;
            var language = args.TryGetProperty("language", out var lang) ? lang.GetString() : "delphi";

            if(!File.Exists(path))
                throw new FileNotFoundException($"File not found: {path}");

            var content = File.ReadAllText(path);
            List<object> sqlStatements;

            if(language?.ToLower() == "delphi")
            {
                sqlStatements = ExtractDelphiSqlStatements(content);
            }
            else
            {
                sqlStatements = ExtractGenericSqlStatements(content);
            }

            return Task.FromResult<object>(new
            {
                success = true,
                fileName = Path.GetFileName(path),
                statementCount = sqlStatements.Count,
                statements = sqlStatements
            });
        }

        public static Task<object> FindDatabaseCalls(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;
            var language = args.TryGetProperty("language", out var lang) ? lang.GetString() : "delphi";

            if (!File.Exists(path))
                throw new FileNotFoundException($"File not found: {path}");

            var content = File.ReadAllText(path);
            var databaseCalls = new List<object>();

            if (language?.ToLower() == "delphi")
            {
                databaseCalls = FindDelphiDatabaseCalls(content);
            }

            return Task.FromResult<object>(new
            {
                success = true,
                fileName = Path.GetFileName(path),
                callCount = databaseCalls.Count,
                databaseCalls
            });
        }

        public static Task<object> ExtractTableReferences(JsonElement args)
        {
            var code = args.GetProperty("code").GetString()!;
            var language = args.TryGetProperty("language", out var lang) ? lang.GetString() : "delphi";

            var tables = new HashSet<string>();

            var fromPattern = @"\bFROM\s+(\w+)";
            var joinPattern = @"\bJOIN\s+(\w+)";
            var intoPattern = @"\bINTO\s+(\w+)";
            var updatePattern = @"\bUPDATE\s+(\w+)";
            var deletePattern = @"\bDELETE\s+FROM\s+(\w+)";

            var patterns = new[] { fromPattern, joinPattern, intoPattern, updatePattern, deletePattern };

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(code, pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        tables.Add(match.Groups[1].Value);
                    }
                }
            }

            return Task.FromResult<object>(new
            {
                success = true,
                tableCount = tables.Count,
                tables = tables.OrderBy(t => t).ToList()
            });
        }

        public static Task<object> AnalyzeProcedureCalls(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;

            if (!File.Exists(path))
                throw new FileNotFoundException($"File not found: {path}");

            var content = File.ReadAllText(path);
            var procedureCalls = new List<object>();

            var execPattern = @"\bEXEC(?:UTE)?\s+(?:PROCEDURE\s+)?(\w+)";
            var matches = Regex.Matches(content, execPattern, RegexOptions.IgnoreCase);

            var lines = content.Split('\n');

            foreach (Match match in matches)
            {
                var procedureName = match.Groups[1].Value;
                var matchLine = GetLineNumber(content, match.Index);

                procedureCalls.Add(new
                {
                    procedureName,
                    line = matchLine,
                    context = GetContextAroundMatch(lines, matchLine)
                });
            }

            return Task.FromResult<object>(new
            {
                success = true,
                fileName = Path.GetFileName(path),
                procedureCallCount = procedureCalls.Count,
                procedureCalls
            });
        }

        public static Task<object> FindPatterns(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;
            var regexPattern = args.GetProperty("regexPattern").GetString()!;

            if (!File.Exists(path))
                throw new FileNotFoundException($"File not found: {path}");

            var content = File.ReadAllText(path);
            var lines = content.Split('\n');
            var matches = new List<object>();

            try
            {
                var regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);
                var regexMatches = regex.Matches(content);

                foreach (Match match in regexMatches)
                {
                    var lineNum = GetLineNumber(content, match.Index);
                    matches.Add(new
                    {
                        match = match.Value,
                        line = lineNum,
                        position = match.Index,
                        context = GetContextAroundMatch(lines, lineNum)
                    });
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Invalid regex pattern: {ex.Message}");
            }

            return Task.FromResult<object>(new
            {
                success = true,
                fileName = Path.GetFileName(path),
                matchCount = matches.Count,
                matches
            });
        }

        public static Task<object> GetCodeMetrics(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;
            var language = args.TryGetProperty("language", out var lang) ? lang.GetString() : "delphi";

            if (!File.Exists(path))
                throw new FileNotFoundException($"File not found: {path}");

            var content = File.ReadAllText(path);
            var lines = content.Split('\n');

            var metrics = new
            {
                totalLines = lines.Length,
                codeLines = lines.Count(l => !string.IsNullOrWhiteSpace(l) && !l.TrimStart().StartsWith("//")),
                commentLines = lines.Count(l => l.TrimStart().StartsWith("//")),
                blankLines = lines.Count(string.IsNullOrWhiteSpace),
                fileSize = new FileInfo(path).Length,
                characterCount = content.Length
            };

            return Task.FromResult<object>(new
            {
                success = true,
                fileName = Path.GetFileName(path),
                language,
                metrics
            });
        }

        public static Task<object> ExtractClassDefinitions(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;
            var language = args.TryGetProperty("language", out var lang) ? lang.GetString() : "delphi";

            if (!File.Exists(path))
                throw new FileNotFoundException($"File not found: {path}");

            var content = File.ReadAllText(path);
            var classes = language?.ToLower() == "delphi" 
                ? ExtractDelphiClasses(content)
                : [];

            return Task.FromResult<object>(new
            {
                success = true,
                fileName = Path.GetFileName(path),
                classCount = classes.Count,
                classes
            });
        }

        public static Task<object> ExtractMethodSignatures(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;
            var language = args.TryGetProperty("language", out var lang) ? lang.GetString() : "delphi";

            if (!File.Exists(path))
                throw new FileNotFoundException($"File not found: {path}");

            var content = File.ReadAllText(path);
            var methods = new List<object>();

            if (language?.ToLower() == "delphi")
            {
                methods.AddRange(ExtractDelphiProcedures(content));
                methods.AddRange(ExtractDelphiFunctions(content));
            }

            return Task.FromResult<object>(new
            {
                success = true,
                fileName = Path.GetFileName(path),
                methodCount = methods.Count,
                methods
            });
        }

        public static Task<object> MapDataStructures(JsonElement args)
        {
            var path = args.GetProperty("path").GetString()!;
            var language = args.TryGetProperty("language", out var lang) ? lang.GetString() : "delphi";

            if (!File.Exists(path))
                throw new FileNotFoundException($"File not found: {path}");

            var content = File.ReadAllText(path);
            var structures = new List<object>();

            if (language?.ToLower() == "delphi")
            {
                structures = ExtractDelphiRecords(content);
            }

            return Task.FromResult<object>(new
            {
                success = true,
                fileName = Path.GetFileName(path),
                structureCount = structures.Count,
                structures
            });
        }



        private static List<string> ExtractDelphiUnits(string content)
        {
            var units = new List<string>();
            var match = Regex.Match(content, @"unit\s+(\w+)\s*;", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                units.Add(match.Groups[1].Value);
            }
            return units;
        }

        private static List<object> ExtractDelphiClasses(string content)
        {
            var classes = new List<object>();
            var pattern = @"(\w+)\s*=\s*class\s*\((\w+)\)";
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                classes.Add(new
                {
                    className = match.Groups[1].Value,
                    baseClass = match.Groups[2].Value,
                    line = GetLineNumber(content, match.Index)
                });
            }

            return classes;
        }

        private static List<object> ExtractDelphiProcedures(string content)
        {
            var procedures = new List<object>();
            var pattern = @"procedure\s+(\w+(?:\.\w+)?)\s*\((.*?)\)\s*;";
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                procedures.Add(new
                {
                    name = match.Groups[1].Value,
                    parameters = match.Groups[2].Value.Trim(),
                    type = "procedure",
                    line = GetLineNumber(content, match.Index)
                });
            }

            return procedures;
        }

        private static List<object> ExtractDelphiFunctions(string content)
        {
            var functions = new List<object>();
            var pattern = @"function\s+(\w+(?:\.\w+)?)\s*\((.*?)\)\s*:\s*(\w+)\s*;";
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                functions.Add(new
                {
                    name = match.Groups[1].Value,
                    parameters = match.Groups[2].Value.Trim(),
                    returnType = match.Groups[3].Value,
                    type = "function",
                    line = GetLineNumber(content, match.Index)
                });
            }

            return functions;
        }

        private static List<string> ExtractUsesClause(string content)
        {
            var units = new List<string>();
            var pattern = @"uses\s+([\w\s,\.]+);";
            var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var usesContent = match.Groups[1].Value;
                units = [.. usesContent.Split(',')
                    .Select(u => u.Trim())
                    .Where(u => !string.IsNullOrWhiteSpace(u))];
            }

            return units;
        }

        private static List<object> ExtractDelphiSqlStatements(string content)
        {
            var statements = new List<object>();
            
            var patterns = new[]
            {
                @"SQL\.(?:Text|Add)\s*:=\s*'([^']+)'",
                @"SQL\.(?:Text|Add)\s*:=\s*""([^""]+)""",
                @"ExecSQL\s*\(\s*'([^']+)'",
                @"CommandText\s*:=\s*'([^']+)'"
            };

            foreach (var pattern in patterns)
            {
                var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
                foreach (Match match in matches)
                {
                    statements.Add(new
                    {
                        sql = match.Groups[1].Value.Trim(),
                        line = GetLineNumber(content, match.Index),
                        type = DetermineSqlType(match.Groups[1].Value)
                    });
                }
            }

            return statements;
        }

        private static List<object> ExtractGenericSqlStatements(string content)
        {
            var statements = new List<object>();
            var sqlKeywords = new[] { "SELECT", "INSERT", "UPDATE", "DELETE", "CREATE", "ALTER", "DROP" };

            foreach (var keyword in sqlKeywords)
            {
                var pattern = $@"\b{keyword}\b[^;]+;?";
                var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

                foreach (Match match in matches)
                {
                    statements.Add(new
                    {
                        sql = match.Value.Trim(),
                        line = GetLineNumber(content, match.Index),
                        type = keyword
                    });
                }
            }

            return statements;
        }

        private static List<object> FindDelphiDatabaseCalls(string content)
        {
            var calls = new List<object>();
            
            var componentPatterns = new Dictionary<string, string>
            {
                { "ADOQuery", @"(\w+)\s*:\s*TADOQuery" },
                { "ADOTable", @"(\w+)\s*:\s*TADOTable" },
                { "ADOStoredProc", @"(\w+)\s*:\s*TADOStoredProc" },
                { "ADOCommand", @"(\w+)\s*:\s*TADOCommand" },
                { "SQLQuery", @"(\w+)\s*:\s*TSQLQuery" },
                { "FDQuery", @"(\w+)\s*:\s*TFDQuery" }
            };

            foreach (var kvp in componentPatterns)
            {
                var matches = Regex.Matches(content, kvp.Value, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    calls.Add(new
                    {
                        componentName = match.Groups[1].Value,
                        componentType = kvp.Key,
                        line = GetLineNumber(content, match.Index)
                    });
                }
            }

            var methodCalls = new[]
            {
                @"\.Open\b",
                @"\.ExecSQL\b",
                @"\.Execute\b",
                @"\.Prepare\b"
            };

            foreach (var pattern in methodCalls)
            {
                var matches = Regex.Matches(content, @"(\w+)" + pattern, RegexOptions.IgnoreCase);
                foreach (Match match in matches)
                {
                    calls.Add(new
                    {
                        componentName = match.Groups[1].Value,
                        methodCall = match.Value,
                        line = GetLineNumber(content, match.Index)
                    });
                }
            }

            return calls;
        }

        private static List<object> ExtractDelphiRecords(string content)
        {
            var records = new List<object>();
            var pattern = @"(\w+)\s*=\s*record\s+(.*?)\s+end\s*;";
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

            foreach (Match match in matches)
            {
                var fields = ExtractRecordFields(match.Groups[2].Value);
                records.Add(new
                {
                    recordName = match.Groups[1].Value,
                    fieldCount = fields.Count,
                    fields,
                    line = GetLineNumber(content, match.Index)
                });
            }

            return records;
        }

        private static List<object> ExtractRecordFields(string recordBody)
        {
            var fields = new List<object>();
            var pattern = @"(\w+)\s*:\s*(\w+)\s*;";
            var matches = Regex.Matches(recordBody, pattern);

            foreach (Match match in matches)
            {
                fields.Add(new
                {
                    fieldName = match.Groups[1].Value,
                    fieldType = match.Groups[2].Value
                });
            }

            return fields;
        }

        private static string DetermineSqlType(string sql)
        {
            var upperSql = sql.TrimStart().ToUpper();
            if (upperSql.StartsWith("SELECT")) return "SELECT";
            if (upperSql.StartsWith("INSERT")) return "INSERT";
            if (upperSql.StartsWith("UPDATE")) return "UPDATE";
            if (upperSql.StartsWith("DELETE")) return "DELETE";
            if (upperSql.StartsWith("CREATE")) return "CREATE";
            if (upperSql.StartsWith("ALTER")) return "ALTER";
            if (upperSql.StartsWith("DROP")) return "DROP";
            return "UNKNOWN";
        }

        private static int GetLineNumber(string content, int position)
        {
            return content[..position].Count(c => c == '\n') + 1;
        }

        private static string GetContextAroundMatch(string[] lines, int lineNumber)
        {
            var startLine = Math.Max(0, lineNumber - 2);
            var endLine = Math.Min(lines.Length - 1, lineNumber + 1);
            
            var context = new StringBuilder();
            for (int i = startLine; i <= endLine; i++)
            {
                context.AppendLine(lines[i]);
            }
            
            return context.ToString().Trim();
        }
    }
}
