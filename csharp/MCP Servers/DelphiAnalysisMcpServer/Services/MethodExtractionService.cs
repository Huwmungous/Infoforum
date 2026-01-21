using System.Text.RegularExpressions;
using DelphiAnalysisMcpServer.Models;

namespace DelphiAnalysisMcpServer.Services;

/// <summary>
/// Service for extracting method implementations from Delphi source code.
/// </summary>
public partial class MethodExtractionService
{
    // Regex for method headers: procedure/function ClassName.MethodName or standalone
    [GeneratedRegex(
        @"^\s*(?<kind>procedure|function|constructor|destructor)\s+(?:(?<class>\w+)\.)?(?<n>\w+)\s*(?:\((?<params>[^)]*)\))?\s*(?::\s*(?<return>[^;]+?))?\s*;",
        RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex MethodHeaderRegex();

    // Regex for implementation section
    [GeneratedRegex(@"\bimplementation\b", RegexOptions.IgnoreCase)]
    private static partial Regex ImplementationSectionRegex();

    // Regex for unit end
    [GeneratedRegex(@"\bend\s*\.\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex UnitEndMarkerRegex();

    // Regex for parameter parsing
    [GeneratedRegex(
        @"(?<modifier>var|const|out)?\s*(?<names>[\w,\s]+)\s*:\s*(?<type>[^;,\)]+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex ParameterRegex();

    /// <summary>
    /// Extracts all method implementations from Delphi source code.
    /// </summary>
    public static List<ExtractedMethod> ExtractAllMethods(string sourceCode)
    {
        var methods = new List<ExtractedMethod>();

        if (string.IsNullOrWhiteSpace(sourceCode))
            return methods;

        // Find the implementation section
        var implMatch = ImplementationSectionRegex().Match(sourceCode);
        if (!implMatch.Success)
            return methods;

        var implStart = implMatch.Index + implMatch.Length;

        // Find the unit end
        var endMatch = UnitEndMarkerRegex().Match(sourceCode);
        var implEnd = endMatch.Success ? endMatch.Index : sourceCode.Length;

        var implementationSection = sourceCode[implStart..implEnd];

        // Find all method bodies
        methods = FindMethodBodies(implementationSection);

        return methods;
    }

    /// <summary>
    /// Finds all method bodies in the implementation section.
    /// </summary>
    private static List<ExtractedMethod> FindMethodBodies(string implementation)
    {
        var methods = new List<ExtractedMethod>();
        var headerRegex = MethodHeaderRegex();
        var matches = headerRegex.Matches(implementation);

        for (int i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var nextStart = i + 1 < matches.Count ? matches[i + 1].Index : implementation.Length;

            // Extract the method body from header to next method or end
            var methodSection = implementation[match.Index..nextStart];

            // Find the actual end of this method (matching begin/end)
            var bodyEnd = FindMethodEnd(methodSection, match.Length);
            var fullMethod = methodSection[..bodyEnd].TrimEnd();

            var kind = match.Groups["kind"].Value.ToLowerInvariant() switch
            {
                "procedure" => MethodKind.Procedure,
                "function" => MethodKind.Function,
                "constructor" => MethodKind.Constructor,
                "destructor" => MethodKind.Destructor,
                _ => MethodKind.Procedure
            };

            var extracted = new ExtractedMethod
            {
                Name = match.Groups["n"].Value,
                Kind = kind,
                ContainingClass = match.Groups["class"].Success ? match.Groups["class"].Value : null,
                ReturnType = match.Groups["return"].Success ? match.Groups["return"].Value.Trim() : null,
                Parameters = ParseParameters(match.Groups["params"].Value),
                IsStandalone = !match.Groups["class"].Success,
                SourceCode = fullMethod
            };

            methods.Add(extracted);
        }

        return methods;
    }

    /// <summary>
    /// Finds the end of a method by matching begin/end pairs.
    /// </summary>
    private static int FindMethodEnd(string methodSection, int startAfterHeader)
    {
        int depth = 0;
        bool foundBegin = false;
        int pos = startAfterHeader;
        int lastEndPos = methodSection.Length;

        while (pos < methodSection.Length)
        {
            // Skip whitespace
            while (pos < methodSection.Length && char.IsWhiteSpace(methodSection[pos]))
                pos++;

            if (pos >= methodSection.Length)
                break;

            // Skip comments
            if (pos + 1 < methodSection.Length)
            {
                // Line comment
                if (methodSection[pos] == '/' && methodSection[pos + 1] == '/')
                {
                    while (pos < methodSection.Length && methodSection[pos] != '\n')
                        pos++;
                    continue;
                }

                // Block comment { }
                if (methodSection[pos] == '{')
                {
                    pos++;
                    while (pos < methodSection.Length && methodSection[pos] != '}')
                        pos++;
                    pos++;
                    continue;
                }

                // Block comment (* *)
                if (methodSection[pos] == '(' && methodSection[pos + 1] == '*')
                {
                    pos += 2;
                    while (pos + 1 < methodSection.Length && !(methodSection[pos] == '*' && methodSection[pos + 1] == ')'))
                        pos++;
                    pos += 2;
                    continue;
                }
            }

            // Skip strings
            if (methodSection[pos] == '\'')
            {
                pos++;
                while (pos < methodSection.Length)
                {
                    if (methodSection[pos] == '\'')
                    {
                        if (pos + 1 < methodSection.Length && methodSection[pos + 1] == '\'')
                            pos += 2; // Escaped quote
                        else
                        {
                            pos++;
                            break;
                        }
                    }
                    else
                        pos++;
                }
                continue;
            }

            // Check for keywords
            if (char.IsLetter(methodSection[pos]))
            {
                int wordStart = pos;
                while (pos < methodSection.Length && (char.IsLetterOrDigit(methodSection[pos]) || methodSection[pos] == '_'))
                    pos++;

                var word = methodSection[wordStart..pos].ToLowerInvariant();

                // Keywords that increase depth
                if (word is "begin" or "try" or "case" or "asm")
                {
                    depth++;
                    foundBegin = true;
                }
                // Keywords that decrease depth
                else if (word == "end")
                {
                    if (foundBegin)
                    {
                        depth--;
                        if (depth == 0)
                        {
                            // Found the matching end - include up to the semicolon
                            while (pos < methodSection.Length && methodSection[pos] != ';')
                                pos++;
                            if (pos < methodSection.Length)
                                pos++; // Include the semicolon
                            return pos;
                        }
                    }
                }
                continue;
            }

            pos++;
        }

        return lastEndPos;
    }

    /// <summary>
    /// Parses method parameters from the parameter string.
    /// </summary>
    private static List<DelphiParameter> ParseParameters(string paramsStr)
    {
        var parameters = new List<DelphiParameter>();

        if (string.IsNullOrWhiteSpace(paramsStr))
            return parameters;

        var paramRegex = ParameterRegex();
        var matches = paramRegex.Matches(paramsStr);

        foreach (Match match in matches)
        {
            var modifier = match.Groups["modifier"].Value.ToLowerInvariant() switch
            {
                "var" => ParameterModifier.Var,
                "const" => ParameterModifier.Const,
                "out" => ParameterModifier.Out,
                _ => ParameterModifier.None
            };

            var names = match.Groups["names"].Value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var type = match.Groups["type"].Value.Trim();

            foreach (var name in names)
            {
                parameters.Add(new DelphiParameter
                {
                    Name = name.Trim(),
                    DelphiType = type,
                    Modifier = modifier
                });
            }
        }

        return parameters;
    }
}

/// <summary>
/// Represents an extracted method from Delphi source code.
/// </summary>
public class ExtractedMethod
{
    public string Name { get; set; } = string.Empty;
    public MethodKind Kind { get; set; }
    public string? ContainingClass { get; set; }
    public string? ReturnType { get; set; }
    public List<DelphiParameter> Parameters { get; set; } = [];
    public bool IsStandalone { get; set; }
    public string SourceCode { get; set; } = string.Empty;

    /// <summary>
    /// Converts to a DelphiMethod instance for saving to database.
    /// </summary>
    public DelphiMethod ToDelphiMethod()
    {
        return new DelphiMethod
        {
            Name = Name,
            Kind = Kind,
            ReturnType = ReturnType,
            Parameters = Parameters,
            IsOverload = false,
            IsVirtual = false,
            IsOverride = false,
            IsAbstract = false,
            IsStatic = false,
            SourceCode = SourceCode,
            ContainingClass = ContainingClass
        };
    }
}
