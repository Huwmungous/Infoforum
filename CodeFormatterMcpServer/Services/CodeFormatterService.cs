using CodeFormatterMcpServer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using System.Text;

namespace CodeFormatterMcpServer.Services;

public class CodeFormatterService
{
    private readonly ILogger<CodeFormatterService> _logger;

    public CodeFormatterService(ILogger<CodeFormatterService> logger)
    {
        _logger = logger;
    }

    public FormatResult FormatCSharp(string code)
    {
        try
        {
            var tree = CSharpSyntaxTree.ParseText(code);
            var root = tree.GetRoot();

            var workspace = new AdhocWorkspace();
            var formattedRoot = Formatter.Format(root, workspace);
            var formattedCode = formattedRoot.ToFullString();

            var diagnostics = tree.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.GetMessage())
                .ToArray();

            return new FormatResult
            {
                Success = true,
                FormattedCode = formattedCode,
                Message = diagnostics.Length > 0 ? "Formatted with syntax errors" : "Formatted successfully",
                Errors = diagnostics.Length > 0 ? diagnostics : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting C#");
            return new FormatResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public FormatResult FormatDelphi(string code)
    {
        try
        {
            var lines = code.Split('\n');
            var formatted = new StringBuilder();
            var indentLevel = 0;
            var indentString = "  ";

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line))
                {
                    formatted.AppendLine();
                    continue;
                }

                var lowerLine = line.ToLower();

                if (lowerLine.StartsWith("end") || lowerLine.StartsWith("until") || 
                    lowerLine.StartsWith("except") || lowerLine.StartsWith("finally"))
                {
                    indentLevel = Math.Max(0, indentLevel - 1);
                }

                formatted.Append(new string(' ', indentLevel * indentString.Length));
                formatted.AppendLine(line);

                if (lowerLine.StartsWith("begin") || lowerLine.StartsWith("repeat") ||
                    lowerLine.StartsWith("try") || lowerLine.Contains(" then") ||
                    lowerLine.Contains(" else") || lowerLine.Contains(" do"))
                {
                    if (!lowerLine.Contains("end;"))
                    {
                        indentLevel++;
                    }
                }

                if (lowerLine.StartsWith("case "))
                {
                    indentLevel++;
                }

                if ((lowerLine.StartsWith("else") || lowerLine.Contains("else:")) && !lowerLine.Contains("begin"))
                {
                    indentLevel = Math.Max(0, indentLevel - 1);
                    formatted.Clear();
                    formatted.Append(new string(' ', indentLevel * indentString.Length));
                    formatted.AppendLine(line);
                    indentLevel++;
                }
            }

            return new FormatResult
            {
                Success = true,
                FormattedCode = formatted.ToString(),
                Message = "Delphi code formatted"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting Delphi");
            return new FormatResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public FormatResult FormatSql(string code)
    {
        try
        {
            var lines = code.Split('\n');
            var formatted = new StringBuilder();
            var indentLevel = 0;
            var indentString = "    ";

            var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "SELECT", "FROM", "WHERE", "JOIN", "LEFT JOIN", "RIGHT JOIN", "INNER JOIN",
                "ORDER BY", "GROUP BY", "HAVING", "INSERT INTO", "UPDATE", "DELETE FROM",
                "VALUES", "SET", "AND", "OR", "UNION", "UNION ALL"
            };

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var upperLine = line.ToUpper();

                if (upperLine.Contains("SELECT") && !upperLine.StartsWith("--"))
                {
                    formatted.Append(new string(' ', indentLevel * indentString.Length));
                    formatted.AppendLine(line);
                    indentLevel++;
                }
                else if (upperLine.StartsWith("FROM") || upperLine.StartsWith("WHERE") ||
                         upperLine.StartsWith("ORDER BY") || upperLine.StartsWith("GROUP BY"))
                {
                    indentLevel = Math.Max(0, indentLevel - 1);
                    formatted.Append(new string(' ', indentLevel * indentString.Length));
                    formatted.AppendLine(line);
                    indentLevel++;
                }
                else if (upperLine.Contains("JOIN"))
                {
                    formatted.Append(new string(' ', indentLevel * indentString.Length));
                    formatted.AppendLine(line);
                }
                else if (line.Contains("("))
                {
                    formatted.Append(new string(' ', indentLevel * indentString.Length));
                    formatted.AppendLine(line);
                    indentLevel++;
                }
                else if (line.Contains(")"))
                {
                    indentLevel = Math.Max(0, indentLevel - 1);
                    formatted.Append(new string(' ', indentLevel * indentString.Length));
                    formatted.AppendLine(line);
                }
                else
                {
                    formatted.Append(new string(' ', indentLevel * indentString.Length));
                    formatted.AppendLine(line);
                }
            }

            return new FormatResult
            {
                Success = true,
                FormattedCode = formatted.ToString(),
                Message = "SQL formatted"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting SQL");
            return new FormatResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public FormatResult FormatFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new FormatResult
                {
                    Success = false,
                    Message = $"File not found: {filePath}"
                };
            }

            var code = File.ReadAllText(filePath);
            var extension = Path.GetExtension(filePath).ToLower();

            return extension switch
            {
                ".cs" => FormatCSharp(code),
                ".pas" => FormatDelphi(code),
                ".dpr" => FormatDelphi(code),
                ".sql" => FormatSql(code),
                _ => new FormatResult
                {
                    Success = false,
                    Message = $"Unsupported file type: {extension}"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error formatting file");
            return new FormatResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }
}