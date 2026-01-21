using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DelphiAnalysisMcpServer.Models;

namespace DelphiAnalysisMcpServer.Services;

/// <summary>
/// Service for interacting with Ollama for code analysis and translation.
/// </summary>
public partial class OllamaService(HttpClient httpClient, ILogger<OllamaService> logger)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly ILogger<OllamaService> _logger = logger;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions s_indentedJsonOptions = new()
    {
        WriteIndented = true
    };

    #region LoggerMessage Definitions

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse analysis result for {UnitName}")]
    private partial void LogAnalysisParseFailure(Exception ex, string unitName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Translation failed for {UnitName}")]
    private partial void LogTranslationFailure(Exception ex, string unitName);

    [LoggerMessage(Level = LogLevel.Error, Message = "Form translation failed for {FormName}")]
    private partial void LogFormTranslationFailure(Exception ex, string formName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to parse database analysis for {UnitName}")]
    private partial void LogDatabaseAnalysisParseFailure(Exception ex, string unitName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Sending request to {Url} with model {Model}")]
    private partial void LogSendingRequest(string url, string model);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to generate AI description for project {ProjectName}")]
    private partial void LogProjectDescriptionFailure(Exception ex, string projectName);

    #endregion

    #region Generated Regex Patterns

    [GeneratedRegex(@"namespace\s+([\w.]+)")]
    private static partial Regex NamespaceRegex();

    [GeneratedRegex(@"public\s+(?:partial\s+)?class\s+(\w+)")]
    private static partial Regex ClassNameRegex();

    [GeneratedRegex(@":\s*Thread\b")]
    private static partial Regex ThreadInheritanceRegex();

    [GeneratedRegex(@"public\s+class\s+(\w+)\s*:\s*Thread\b")]
    private static partial Regex ThreadClassRegex();

    [GeneratedRegex(@"public\s+override\s+void\s+Run\s*\(")]
    private static partial Regex OverrideRunRegex();

    [GeneratedRegex(@"~(\w+)\s*\(\s*\)")]
    private static partial Regex DestructorRegex();

    [GeneratedRegex(@"\bSqlConnection\b")]
    private static partial Regex SqlConnectionRegex();

    [GeneratedRegex(@"\bSqlCommand\b")]
    private static partial Regex SqlCommandRegex();

    [GeneratedRegex(@"\bSqlTransaction\b")]
    private static partial Regex SqlTransactionRegex();

    [GeneratedRegex(@"\bSqlDataReader\b")]
    private static partial Regex SqlDataReaderRegex();

    [GeneratedRegex(@"\bSqlParameter\b")]
    private static partial Regex SqlParameterRegex();

    [GeneratedRegex(@"\bSqlException\b")]
    private static partial Regex SqlExceptionRegex();

    #endregion

    /// <summary>
    /// Analyzes a Delphi unit and extracts structured information.
    /// </summary>
    public async Task<UnitAnalysisResult> AnalyzeUnitAsync(string sourceCode, string unitName, TranslationOptions options)
    {
        var systemPrompt = BuildAnalysisSystemPrompt();
        var userPrompt = $$"""
            Analyze this Delphi unit and provide a JSON summary of its structure.
            
            Unit Name: {{unitName}}
            
            Source Code:
            ```pascal
            {{sourceCode}}
            ```
            
            Respond with ONLY valid JSON in this format:
            {
                "classes": [{ "name": "...", "parentClass": "...", "methods": ["..."], "properties": ["..."] }],
                "records": [{ "name": "...", "fields": ["..."] }],
                "interfaces": ["..."],
                "standaloneFunctions": ["..."],
                "dependencies": ["..."],
                "complexity": "low|medium|high",
                "notes": "..."
            }
            """;

        var response = await SendChatAsync(options.OllamaBaseUrl, options.OllamaModel, systemPrompt, userPrompt);

        try
        {
            var json = ExtractJson(response);
            var result = JsonSerializer.Deserialize<UnitAnalysisResult>(json, s_jsonOptions);
            return result ?? new UnitAnalysisResult { Notes = "Failed to parse analysis result" };
        }
        catch (Exception ex)
        {
            LogAnalysisParseFailure(ex, unitName);
            return new UnitAnalysisResult { Notes = $"Analysis completed but parsing failed: {ex.Message}" };
        }
    }

    /// <summary>
    /// Generates an AI-powered description of a Delphi project's purpose and functionality.
    /// </summary>
    public async Task<ProjectAiDescription> DescribeProjectAsync(string projectInfo, string projectName, TranslationOptions options)
    {
        var systemPrompt = """
            You are an expert software analyst specializing in Delphi/Pascal applications.
            Your task is to analyze project information and provide a clear, concise description
            of what the application does, its business domain, and key functionality.
            
            Respond with ONLY valid JSON - no markdown, no explanations outside the JSON.
            """;

        var userPrompt = $$"""
            Analyze this Delphi project and describe its purpose and functionality.
            
            Project Name: {{projectName}}
            
            Project Information:
            {{projectInfo}}
            
            Based on the form names, unit names, class names, and code samples provided,
            determine what this application does and respond with ONLY valid JSON in this exact format:
            {
                "purpose": "A brief 1-2 sentence description of what this application does",
                "businessDomain": "The business domain (e.g., Finance, Healthcare, Retail, Manufacturing, Utilities, Education, etc.)",
                "keyFeatures": ["feature1", "feature2", "feature3"],
                "keyEntities": ["entity1", "entity2", "entity3"],
                "technicalSummary": "A brief technical summary of the application architecture and patterns used"
            }
            
            Guidelines:
            - For "purpose": Be specific about what the application does (e.g., "Customer relationship management system for tracking sales leads and customer interactions")
            - For "businessDomain": Choose the most appropriate domain based on the code
            - For "keyFeatures": List 3-7 main features/capabilities you can identify
            - For "keyEntities": List the main data objects/entities (e.g., Customer, Invoice, Product, Order)
            - For "technicalSummary": Mention patterns like MDI, master-detail, client-server, database type, etc.
            
            Respond with ONLY the JSON object, no other text.
            """;

        try
        {
            var response = await SendChatAsync(options.OllamaBaseUrl, options.OllamaModel, systemPrompt, userPrompt);
            var json = ExtractJson(response);
            var result = JsonSerializer.Deserialize<ProjectAiDescription>(json, s_jsonOptions);
            return result ?? new ProjectAiDescription { Purpose = "Unable to analyze project" };
        }
        catch (Exception ex)
        {
            LogProjectDescriptionFailure(ex, projectName);
            return new ProjectAiDescription
            {
                Purpose = $"Analysis failed: {ex.Message}",
                BusinessDomain = "Unknown",
                TechnicalSummary = "Unable to analyze project structure"
            };
        }
    }

    /// <summary>
    /// Translates a Delphi unit to C#.
    /// </summary>
    public async Task<TranslationResult> TranslateUnitAsync(string sourceCode, string unitName, TranslationOptions options)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var systemPrompt = BuildTranslationSystemPrompt(options);
            var userPrompt = $$"""
                Translate this Delphi unit to modern C#.
                
                Original Unit Name: {{unitName}}
                Target Namespace: {{options.BaseNamespace}}
                
                Delphi Source:
                ```pascal
                {{sourceCode}}
                ```
                
                Provide ONLY the translated C# code, no explanations. Use modern C# features.
                """;

            var translatedCode = await SendChatAsync(options.OllamaBaseUrl, options.OllamaModel, systemPrompt, userPrompt);
            translatedCode = CleanCodeResponse(translatedCode);

            stopwatch.Stop();

            return new TranslationResult
            {
                Success = true,
                SourceFile = unitName,
                TranslatedCode = translatedCode,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogTranslationFailure(ex, unitName);
            return new TranslationResult
            {
                Success = false,
                SourceFile = unitName,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Translates a DFM form file to the target UI framework.
    /// </summary>
    public async Task<TranslationResult> TranslateFormAsync(string dfmContent, string formName, TranslationOptions options)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var systemPrompt = options.UITarget == UITargetFramework.React
                ? BuildReactFormTranslationSystemPrompt()
                : BuildFormTranslationSystemPrompt(options);

            var userPrompt = $$"""
                Translate this Delphi DFM form definition to {{options.UITarget}}.
                
                Form Name: {{formName}}
                Target Namespace: {{options.BaseNamespace}}
                
                DFM Content:
                ```
                {{dfmContent}}
                ```
                
                Provide ONLY the translated code, no explanations.
                """;

            var translatedCode = await SendChatAsync(options.OllamaBaseUrl, options.OllamaModel, systemPrompt, userPrompt);
            translatedCode = CleanCodeResponse(translatedCode);

            stopwatch.Stop();

            return new TranslationResult
            {
                Success = true,
                SourceFile = formName,
                TranslatedCode = translatedCode,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogFormTranslationFailure(ex, formName);
            return new TranslationResult
            {
                Success = false,
                SourceFile = formName,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Extracts and analyzes database operations from Delphi code using AI.
    /// </summary>
    public async Task<DatabaseAnalysisResult> AnalyzeDatabaseOperationsAsync(
        string sourceCode,
        string unitName,
        TranslationOptions options)
    {
        var systemPrompt = BuildDatabaseAnalysisSystemPrompt();
        var userPrompt = $$"""
            Analyze this Delphi code and extract ALL database operations.
            
            Unit Name: {{unitName}}
            
            Source Code:
            ```pascal
            {{sourceCode}}
            ```
            
            Respond with ONLY valid JSON:
            {
                "operations": [
                    {
                        "methodName": "...",
                        "className": "...",
                        "sqlStatement": "...",
                        "operationType": "Select|Insert|Update|Delete|StoredProcedure|ExecuteNonQuery",
                        "tableName": "...",
                        "parameters": [
                            { "name": "...", "delphiType": "...", "direction": "Input|Output|InputOutput" }
                        ],
                        "isPartOfTransaction": true,
                        "transactionPeers": ["methodName1", "methodName2"]
                    }
                ],
                "transactionGroups": [
                    {
                        "methodName": "...",
                        "operations": ["op1", "op2"]
                    }
                ],
                "dtoSuggestions": [
                    {
                        "name": "...",
                        "fields": [{ "name": "...", "type": "..." }],
                        "basedOnTable": "..."
                    }
                ]
            }
            """;

        var response = await SendChatAsync(options.OllamaBaseUrl, options.OllamaModel, systemPrompt, userPrompt);

        try
        {
            var json = ExtractJson(response);
            var result = JsonSerializer.Deserialize<DatabaseAnalysisResult>(json, s_jsonOptions);
            return result ?? new DatabaseAnalysisResult();
        }
        catch (Exception ex)
        {
            LogDatabaseAnalysisParseFailure(ex, unitName);
            return new DatabaseAnalysisResult();
        }
    }

    /// <summary>
    /// Generates a repository class from database operations.
    /// </summary>
    public async Task<string> GenerateRepositoryAsync(
        string repositoryName,
        List<DatabaseOperation> operations,
        List<TransactionGroup> transactionGroups,
        TranslationOptions options)
    {
        var systemPrompt = BuildRepositoryGenerationSystemPrompt(options);
        var operationsJson = JsonSerializer.Serialize(operations, s_indentedJsonOptions);
        var transactionsJson = JsonSerializer.Serialize(transactionGroups, s_indentedJsonOptions);

        var userPrompt = $$"""
            Generate a C# repository class that inherits from BaseRepository.
            
            Repository Name: {{repositoryName}}
            Namespace: {{options.BaseNamespace}}.{{options.ApiOptions.RepositoryNamespace}}
            
            Database Operations:
            ```json
            {{operationsJson}}
            ```
            
            Transaction Groups (these operations MUST be in a single method with transaction):
            ```json
            {{transactionsJson}}
            ```
            
            Generate ONLY the C# code. Use Dapper for data access. 
            For transaction groups, wrap all operations in a single method using FbTransaction.
            """;

        var code = await SendChatAsync(options.OllamaBaseUrl, options.OllamaModel, systemPrompt, userPrompt);
        return CleanCodeResponse(code);
    }

    /// <summary>
    /// Generates a controller class that calls repository methods.
    /// </summary>
    public async Task<string> GenerateControllerAsync(
        string controllerName,
        string repositoryName,
        List<DatabaseOperation> operations,
        TranslationOptions options)
    {
        var systemPrompt = BuildControllerGenerationSystemPrompt(options);
        var operationsJson = JsonSerializer.Serialize(operations, s_indentedJsonOptions);

        var userPrompt = $$"""
            Generate a C# ASP.NET Core controller that uses the repository.
            
            Controller Name: {{controllerName}}
            Repository Name: {{repositoryName}}
            Namespace: {{options.BaseNamespace}}.{{options.ApiOptions.ControllerNamespace}}
            API Route Prefix: {{options.ApiOptions.ApiRoutePrefix}}
            
            Operations the repository provides:
            ```json
            {{operationsJson}}
            ```
            
            Generate ONLY the C# code. Include Swagger documentation attributes.
            The controller should inject the repository and call its methods.
            """;

        var code = await SendChatAsync(options.OllamaBaseUrl, options.OllamaModel, systemPrompt, userPrompt);
        return CleanCodeResponse(code);
    }

    /// <summary>
    /// Generates React components from Delphi form code.
    /// </summary>
    public async Task<ReactGenerationResult> GenerateReactComponentAsync(
        string dfmContent,
        string pasContent,
        string formName,
        List<string> apiEndpoints,
        TranslationOptions options)
    {
        var systemPrompt = BuildReactGenerationSystemPrompt();
        var endpointsList = string.Join("\n", apiEndpoints.Select(e => $"- {e}"));

        var userPrompt = $$"""
            Generate a React functional component from this Delphi form.
            
            Form Name: {{formName}}
            
            DFM (UI Definition):
            ```
            {{dfmContent}}
            ```
            
            Pascal (Event Handlers and Logic):
            ```pascal
            {{pasContent}}
            ```
            
            Available API Endpoints (use these instead of direct database calls):
            {{endpointsList}}
            
            Generate:
            1. The main React component (.tsx file)
            2. Any CSS module if needed (.module.css)
            3. TypeScript interfaces for props and state
            
            Respond with JSON:
            {
                "componentCode": "...",
                "cssCode": "...",
                "typesCode": "...",
                "imports": ["..."],
                "apiCalls": ["..."]
            }
            """;

        var response = await SendChatAsync(options.OllamaBaseUrl, options.OllamaModel, systemPrompt, userPrompt);

        try
        {
            var json = ExtractJson(response);
            var result = JsonSerializer.Deserialize<ReactGenerationResult>(json, s_jsonOptions);
            return result ?? new ReactGenerationResult { ComponentCode = response };
        }
        catch
        {
            return new ReactGenerationResult { ComponentCode = CleanCodeResponse(response) };
        }
    }

    /// <summary>
    /// Translates Delphi unit to C# with database calls replaced by API calls.
    /// </summary>
    public async Task<TranslationResult> TranslateUnitWithApiCallsAsync(
        string sourceCode,
        string unitName,
        List<string> apiEndpoints,
        TranslationOptions options)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var systemPrompt = BuildApiAwareTranslationSystemPrompt(options);
            var endpointsList = string.Join("\n", apiEndpoints.Select(e => $"- {e}"));

            var userPrompt = $$"""
                Translate this Delphi unit to modern C#.
                IMPORTANT: Replace ALL database operations with HTTP API calls.
                
                Original Unit Name: {{unitName}}
                Target Namespace: {{options.BaseNamespace}}
                
                Available API Endpoints (use HttpClient to call these):
                {{endpointsList}}
                
                Delphi Source:
                ```pascal
                {{sourceCode}}
                ```
                
                Provide ONLY the translated C# code. 
                - Inject HttpClient via constructor
                - Use async/await for all API calls
                - Add appropriate error handling
                """;

            var translatedCode = await SendChatAsync(options.OllamaBaseUrl, options.OllamaModel, systemPrompt, userPrompt);
            translatedCode = CleanCodeResponse(translatedCode);

            stopwatch.Stop();

            return new TranslationResult
            {
                Success = true,
                SourceFile = unitName,
                TranslatedCode = translatedCode,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            LogTranslationFailure(ex, unitName);
            return new TranslationResult
            {
                Success = false,
                SourceFile = unitName,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    private async Task<string> SendChatAsync(string baseUrl, string model, string systemPrompt, string userPrompt)
    {
        var request = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            stream = false
        };

        var url = $"{baseUrl.TrimEnd('/')}/api/chat";
        LogSendingRequest(url, model);

        var response = await _httpClient.PostAsJsonAsync(url, request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaChatResponse>();
        return result?.Message?.Content ?? string.Empty;
    }

    private static string ExtractJson(string response)
    {
        var jsonStart = response.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (jsonStart >= 0)
        {
            jsonStart = response.IndexOf('\n', jsonStart) + 1;
            var jsonEnd = response.IndexOf("```", jsonStart, StringComparison.Ordinal);
            if (jsonEnd > jsonStart)
            {
                return response[jsonStart..jsonEnd].Trim();
            }
        }

        jsonStart = response.IndexOf('{');
        if (jsonStart >= 0)
        {
            var jsonEnd = response.LastIndexOf('}');
            if (jsonEnd > jsonStart)
            {
                return response[jsonStart..(jsonEnd + 1)];
            }
        }

        return response;
    }

    private static string CleanCodeResponse(string response)
    {
        var lines = response.Split('\n').ToList();
        var cleanedLines = new List<string>();
        var codeBlockStarted = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                codeBlockStarted = !codeBlockStarted;
                continue;
            }

            if (IsCodeLine(trimmed))
            {
                cleanedLines.Add(line);
                continue;
            }

            if (IsExplanatoryText(trimmed))
            {
                var indent = line.Length - line.TrimStart().Length;
                var indentStr = new string(' ', indent);
                cleanedLines.Add($"{indentStr}// {trimmed}");
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                cleanedLines.Add(line);
                continue;
            }

            cleanedLines.Add(line);
        }

        var result = string.Join('\n', cleanedLines).Trim();

        // Truncate at point where degenerate output begins
        result = TruncateDegenerateOutput(result);

        result = FixCommonCodeIssues(result);
        result = ConvertSqlClientToFirebird(result);
        result = FixThreadInheritance(result);
        result = FixMailKitApiUsage(result);

        return result;
    }

    /// <summary>
    /// Detects and truncates code at the point where AI degeneration begins.
    /// </summary>
    private static string TruncateDegenerateOutput(string code)
    {
        // Markers that indicate AI model breakdown/degeneration
        string[] degenerateMarkers =
        [
            "<|im_start|>",
            "<|im_end|>",
            "<|endoftext|>",
            "### 1.",
            "### 2.",
            "### 3.",
            "**Sign up",
            "**Create a",
            "**Get your",
            "- **",
            "Could you please clarify",
            "It seems like you",
            "// It seems like",
            "Let me know if",
            "I hope this helps",
            "Feel free to ask",
            "Here's an example",
            "Below is a simple example",
            "Let's go through",
            "// If you need help",
            "// Could you",
            "// Thank you",
            "// Sure!",
            "```csharp",
            "```json",
            "```sql",
            "```",
            "However, there are no",
            "there was some confusion",
            "What I'm looking for",
            "you're absolutely right",
            "// Note:",
            "// TODO:",
            "1. How to",
            "2. How to",
            "3. Any best",
        ];

        var truncateIndex = code.Length;

        foreach (var marker in degenerateMarkers)
        {
            var index = code.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index > 100 && index < truncateIndex)  // Must be after initial code (100+ chars)
            {
                truncateIndex = index;
            }
        }

        if (truncateIndex < code.Length)
        {
            // Truncate at the marker
            code = code[..truncateIndex];

            // Try to find a clean break point - walk back to find last complete statement
            var lines = code.Split('\n').ToList();

            // Remove trailing incomplete lines
            while (lines.Count > 0)
            {
                var lastLine = lines[^1].Trim();
                if (string.IsNullOrWhiteSpace(lastLine) ||
                    lastLine == "}" ||
                    lastLine.EndsWith(';') ||
                    lastLine.EndsWith('{') ||
                    lastLine.StartsWith("//"))
                {
                    break;
                }
                lines.RemoveAt(lines.Count - 1);
            }

            code = string.Join('\n', lines);

            // Count open vs close braces to ensure we have balanced code
            var openBraces = code.Count(c => c == '{');
            var closeBraces = code.Count(c => c == '}');

            // Add missing close braces
            while (closeBraces < openBraces)
            {
                code += "\n}";
                closeBraces++;
            }
        }

        return code;
    }

    /// <summary>
    /// Fixes incorrect MailKit API usage from AI translation.
    /// </summary>
    private static string FixMailKitApiUsage(string code)
    {
        if (!code.Contains("MailKit") && !code.Contains("MimeKit") && !code.Contains("SmtpClient") &&
            !code.Contains("clSmtp") && !code.Contains("clMailMessage") && !code.Contains("ClMailMessage"))
        {
            return code;
        }

        // Fix: Remove problematic event subscriptions and property assignments
        var lines = code.Split('\n')
            .Where(l => !l.Contains(".Progress +=") || !l.Contains("clSmtp"))
            .Where(l => !l.Contains(".SendCommand +=") || !l.Contains("clSmtp"))
            .Where(l => !l.Contains(".BodyEncoding ="))
            .Where(l => !l.Contains(".SubjectEncoding ="))
            .ToList();
        code = string.Join('\n', lines);

        // Fix: Convert Delphi-style email field names to proper C# property names
        // clSmtp → Client (SmtpClient)
        code = code.Replace("private SmtpClient clSmtp", "public SmtpClient? Client");
        code = code.Replace("private SmtpClient? clSmtp", "public SmtpClient? Client");
        code = code.Replace("internal SmtpClient clSmtp", "public SmtpClient? Client");
        code = code.Replace("internal SmtpClient ClSmtp", "public SmtpClient? Client");
        code = code.Replace("public SmtpClient clSmtp", "public SmtpClient? Client");
        code = code.Replace("public SmtpClient ClSmtp", "public SmtpClient? Client");

        // clMailMessage → Message (MimeMessage)
        code = code.Replace("private MimeMessage clMailMessage", "public MimeMessage? Message");
        code = code.Replace("private MimeMessage? clMailMessage", "public MimeMessage? Message");
        code = code.Replace("internal MimeMessage clMailMessage", "public MimeMessage? Message");
        code = code.Replace("internal MimeMessage ClMailMessage", "public MimeMessage? Message");
        code = code.Replace("public MimeMessage clMailMessage", "public MimeMessage? Message");
        code = code.Replace("public MimeMessage ClMailMessage", "public MimeMessage? Message");

        // Update all references to use new property names
        code = code.Replace(".clSmtp", ".Client");
        code = code.Replace(".ClSmtp", ".Client");
        code = code.Replace("this.clSmtp", "this.Client");
        code = code.Replace("this.ClSmtp", "this.Client");
        code = code.Replace("_email.clSmtp", "_email.Client");
        code = code.Replace("_email.ClSmtp", "_email.Client");

        code = code.Replace(".clMailMessage", ".Message");
        code = code.Replace(".ClMailMessage", ".Message");
        code = code.Replace("this.clMailMessage", "this.Message");
        code = code.Replace("this.ClMailMessage", "this.Message");
        code = code.Replace("_email.clMailMessage", "_email.Message");
        code = code.Replace("_email.ClMailMessage", "_email.Message");

        // Fix: Headers.Date should be message.Date
        code = code.Replace(".Headers.Date", ".Date");

        // Fix: MimeMessage.Load should be MimeMessage.LoadAsync
        code = code.Replace(".Load(ms)", " = await MimeMessage.LoadAsync(ms)");
        code = code.Replace("await _email.Message.Load", "_email.Message = await MimeMessage.LoadAsync");

        // Remove non-existent event handler methods entirely
        code = RemoveMethodBySignature(code, "SmtpProgressEventArgs");
        code = RemoveMethodBySignature(code, "SendCommandEventArgs");
        code = RemoveMethodBySignature(code, "SaveAttachmentEventArgs");

        // Remove duplicate Gateway class if present (should use Classes/Gateway.cs)
        code = RemoveDuplicateGatewayClass(code);

        // Fix: RootPath without Gateway prefix
        code = FixRootPathReferences(code);

        return code;
    }

    /// <summary>
    /// Removes a method that has a specific parameter type in its signature.
    /// </summary>
    private static string RemoveMethodBySignature(string code, string parameterType)
    {
        var lines = code.Split('\n').ToList();
        var result = new List<string>();
        var skipUntilCloseBrace = false;
        var braceDepth = 0;

        foreach (var line in lines)
        {
            if (line.Contains(parameterType) && line.Contains("void") && line.Contains('('))
            {
                skipUntilCloseBrace = true;
                braceDepth = 0;
                result.Add($"    // Method with {parameterType} removed - type doesn't exist in MailKit");
                continue;
            }

            if (skipUntilCloseBrace)
            {
                braceDepth += line.Count(c => c == '{');
                braceDepth -= line.Count(c => c == '}');

                if (braceDepth <= 0 && line.Trim() == "}")
                {
                    skipUntilCloseBrace = false;
                }
                continue;
            }

            result.Add(line);
        }

        return string.Join('\n', result);
    }

    /// <summary>
    /// Removes duplicate Gateway class definitions from translated code.
    /// </summary>
    private static string RemoveDuplicateGatewayClass(string code)
    {
        var lines = code.Split('\n').ToList();
        var result = new List<string>();
        var skipUntilCloseBrace = false;
        var braceDepth = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Detect start of duplicate Gateway class
            if ((trimmed.StartsWith("public static class Gateway") ||
                 trimmed.StartsWith("public class Gateway")) && !skipUntilCloseBrace)
            {
                skipUntilCloseBrace = true;
                braceDepth = 0;
                result.Add("// Duplicate Gateway class removed - using ConvertedApp.Classes.Gateway instead");
                continue;
            }

            if (skipUntilCloseBrace)
            {
                braceDepth += line.Count(c => c == '{');
                braceDepth -= line.Count(c => c == '}');

                if (braceDepth <= 0 && trimmed == "}")
                {
                    skipUntilCloseBrace = false;
                }
                continue;
            }

            result.Add(line);
        }

        return string.Join('\n', result);
    }

    /// <summary>
    /// Fixes RootPath references to use Gateway.RootPath.
    /// </summary>
    private static string FixRootPathReferences(string code)
    {
        // Fix bare RootPath at start of expressions
        var lines = code.Split('\n').ToList();
        var result = new List<string>();

        foreach (var line in lines)
        {
            var fixedLine = line;

            // Match RootPath that's not already prefixed with Gateway. or part of another word
            if (fixedLine.Contains("RootPath") && !fixedLine.Contains("Gateway.RootPath"))
            {
                // Check if it's a standalone reference (not a property definition)
                if (!fixedLine.Contains("public") && !fixedLine.Contains("private") &&
                    !fixedLine.Contains("static string RootPath"))
                {
                    fixedLine = fixedLine.Replace("RootPath +", "Gateway.RootPath +");
                    fixedLine = fixedLine.Replace("RootPath;", "Gateway.RootPath;");
                    fixedLine = fixedLine.Replace("= RootPath", "= Gateway.RootPath");
                }
            }

            result.Add(fixedLine);
        }

        return string.Join('\n', result);
    }

    private static string FixCommonCodeIssues(string code)
    {
        var lines = code.Split('\n').ToList();
        var fixedLines = new List<string>();
        var insideNamespace = false;
        var namespaceDepth = 0;
        string? outerNamespace = null;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();

            if (trimmed.StartsWith("file-scoped namespace ", StringComparison.OrdinalIgnoreCase))
            {
                var nsName = trimmed.Replace("file-scoped namespace ", "", StringComparison.OrdinalIgnoreCase)
                                   .TrimEnd(';');
                if (outerNamespace != null)
                {
                    continue;
                }

                fixedLines.Add($"namespace {nsName};");
                continue;
            }

            if (trimmed.StartsWith("namespace ", StringComparison.Ordinal) && !trimmed.EndsWith(';'))
            {
                var nsMatch = NamespaceRegex().Match(trimmed);
                if (nsMatch.Success)
                {
                    outerNamespace = nsMatch.Groups[1].Value;
                    insideNamespace = true;
                    fixedLines.Add(line);
                    continue;
                }
            }

            if (insideNamespace)
            {
                if (trimmed == "{")
                    namespaceDepth++;
                else if (trimmed == "}")
                {
                    namespaceDepth--;
                    if (namespaceDepth == 0)
                    {
                        insideNamespace = false;
                        outerNamespace = null;
                    }
                }
            }

            if (insideNamespace && namespaceDepth == 1 &&
                trimmed.StartsWith("namespace ", StringComparison.Ordinal) && trimmed.EndsWith(';'))
            {
                fixedLines.Add($"    // WARNING: Invalid nested namespace removed: {trimmed}");
                continue;
            }

            if (trimmed.Contains("protected override void Finalize()"))
            {
                var indent = line.Length - line.TrimStart().Length;
                var indentStr = new string(' ', indent);
                fixedLines.Add($"{indentStr}// Note: Converted from Finalize() - consider using IDisposable pattern instead");
                fixedLines.Add(line.Replace("protected override void Finalize()", "~EmailThread()"));
                continue;
            }

            if (trimmed.Contains("base.Finalize()"))
            {
                fixedLines.Add(line.Replace("base.Finalize();", "// base destructor called automatically"));
                continue;
            }

            if (trimmed == "public override void Run()")
            {
                fixedLines.Add(line.Replace("public override void Run()", "protected void Execute()"));
                fixedLines.Add("    // Note: Call this.Execute() from constructor or use Task.Run(() => Execute())");
                continue;
            }

            fixedLines.Add(line);
        }

        return string.Join('\n', fixedLines);
    }

    private static string ConvertSqlClientToFirebird(string code)
    {
        if (!code.Contains("SqlConnection") && !code.Contains("SqlCommand") &&
            !code.Contains("System.Data.SqlClient"))
        {
            return code;
        }

        code = code.Replace("using System.Data.SqlClient;", "using FirebirdSql.Data.FirebirdClient;");
        code = code.Replace("using Microsoft.Data.SqlClient;", "using FirebirdSql.Data.FirebirdClient;");

        code = SqlConnectionRegex().Replace(code, "FbConnection");
        code = SqlCommandRegex().Replace(code, "FbCommand");
        code = SqlTransactionRegex().Replace(code, "FbTransaction");
        code = SqlDataReaderRegex().Replace(code, "FbDataReader");
        code = SqlParameterRegex().Replace(code, "FbParameter");
        code = SqlExceptionRegex().Replace(code, "FbException");

        return code;
    }

    private static string FixThreadInheritance(string code)
    {
        if (!ThreadInheritanceRegex().IsMatch(code))
        {
            return code;
        }

        var lines = code.Split('\n').ToList();
        var fixedLines = new List<string>();
        string? currentClassName = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var classMatch = ThreadClassRegex().Match(trimmed);
            if (classMatch.Success)
            {
                currentClassName = classMatch.Groups[1].Value;
                break;
            }
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (currentClassName != null && ThreadClassRegex().IsMatch(trimmed))
            {
                var fixedLine = ThreadInheritanceRegex().Replace(line, ": IDisposable");
                fixedLines.Add(fixedLine);
                fixedLines.Add("    // NOTE: Converted from TThread - Thread is sealed in .NET");
                fixedLines.Add("    private Task? _executionTask;");
                fixedLines.Add("    private CancellationTokenSource _cts = new();");
                continue;
            }

            if (trimmed.Contains("IsBackground = "))
            {
                fixedLines.Add("        // IsBackground not applicable - using Task");
                continue;
            }

            if (OverrideRunRegex().IsMatch(trimmed))
            {
                fixedLines.Add(line.Replace("public override void Run()", "private void Execute()"));
                continue;
            }

            if (currentClassName != null)
            {
                var destructorMatch = DestructorRegex().Match(trimmed);
                if (destructorMatch.Success && destructorMatch.Groups[1].Value != currentClassName)
                {
                    fixedLines.Add(line.Replace($"~{destructorMatch.Groups[1].Value}()", $"~{currentClassName}()"));
                    continue;
                }
            }

            if (trimmed.Contains("protected override void Finalize()"))
            {
                if (currentClassName != null)
                {
                    fixedLines.Add(line.Replace("protected override void Finalize()", $"~{currentClassName}()"));
                }
                else
                {
                    fixedLines.Add("    // WARNING: Finalize() method - consider IDisposable pattern");
                    fixedLines.Add(line);
                }
                continue;
            }

            if (trimmed.Contains("base.Finalize()"))
            {
                fixedLines.Add(line.Replace("base.Finalize();", "// base destructor called automatically"));
                continue;
            }

            fixedLines.Add(line);
        }

        return string.Join('\n', fixedLines);
    }

    private static bool IsCodeLine(string trimmedLine)
    {
        if (string.IsNullOrWhiteSpace(trimmedLine))
            return false;

        if (trimmedLine.StartsWith("//", StringComparison.Ordinal)) return true;
        if (trimmedLine.StartsWith("/*", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("*/", StringComparison.Ordinal) ||
            trimmedLine.StartsWith('*')) return true;
        if (trimmedLine.StartsWith("using ", StringComparison.Ordinal)) return true;
        if (trimmedLine.StartsWith("namespace ", StringComparison.Ordinal)) return true;
        if (trimmedLine.StartsWith("public ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("private ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("protected ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("internal ", StringComparison.Ordinal)) return true;
        if (trimmedLine.StartsWith("static ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("async ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("await ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("return ", StringComparison.Ordinal)) return true;
        if (trimmedLine.StartsWith("var ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("new ", StringComparison.Ordinal)) return true;
        if (trimmedLine.StartsWith("if ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("if(", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("else", StringComparison.Ordinal)) return true;
        if (trimmedLine.StartsWith("for ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("for(", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("foreach ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("foreach(", StringComparison.Ordinal)) return true;
        if (trimmedLine.StartsWith("while ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("while(", StringComparison.Ordinal)) return true;
        if (trimmedLine.StartsWith("try", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("catch", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("finally", StringComparison.Ordinal)) return true;
        if (trimmedLine.StartsWith("throw ", StringComparison.Ordinal)) return true;
        if (trimmedLine.StartsWith("class ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("interface ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("record ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("struct ", StringComparison.Ordinal) ||
            trimmedLine.StartsWith("enum ", StringComparison.Ordinal)) return true;
        if (trimmedLine.StartsWith('[') ||
            trimmedLine.StartsWith('#') ||
            trimmedLine.StartsWith("/// ", StringComparison.Ordinal)) return true;
        if (trimmedLine is "{" or "}" or "};") return true;
        if (trimmedLine.EndsWith(';') ||
            trimmedLine.EndsWith('{') ||
            trimmedLine.EndsWith('}') ||
            trimmedLine.EndsWith(',')) return true;
        if (trimmedLine.Contains(" => ") ||
            trimmedLine.Contains("?.") ||
            trimmedLine.Contains("??")) return true;

        return false;
    }

    private static bool IsExplanatoryText(string trimmedLine)
    {
        if (string.IsNullOrWhiteSpace(trimmedLine))
            return false;

        string[] explanatoryStarts =
        [
            "Note:", "NOTE:", "Warning:", "WARNING:", "Important:", "IMPORTANT:",
            "Todo:", "TODO:", "Fixme:", "FIXME:", "Hint:", "HINT:",
            "This code", "This method", "This class", "This file",
            "The above", "The following", "The code",
            "Please note", "Please ensure", "Make sure", "Be sure",
            "Remember to", "Don't forget", "Adjustments", "Adjustment",
            "You may need", "You might need", "You will need", "You should", "You must",
            "Ensure that", "Ensure the", "Replace", "Modify", "Update", "Change",
            "See also", "Refer to", "Reference:", "Example:", "Examples:",
            "Usage:", "How to use", "Summary:", "Overview:", "Description:",
            "Parameters:", "Returns:", "Remarks:", "Assumes", "Assuming",
            "Based on", "Depending on",
        ];

        foreach (var start in explanatoryStarts)
        {
            if (trimmedLine.StartsWith(start, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (char.IsUpper(trimmedLine[0]) &&
            trimmedLine.Contains(' ') &&
            !trimmedLine.Contains('(') &&
            !trimmedLine.Contains('{') &&
            !trimmedLine.Contains(';') &&
            !trimmedLine.Contains('=') &&
            (trimmedLine.EndsWith('.') || trimmedLine.EndsWith(':') || trimmedLine.EndsWith('!')))
        {
            return true;
        }

        return false;
    }

    #region System Prompts

    private static string BuildAnalysisSystemPrompt() =>
        """
        You are an expert Delphi code analyzer. Your task is to analyze Delphi/Object Pascal source code 
        and provide structured information about its contents.
        
        Focus on:
        - Class definitions and their inheritance
        - Record types
        - Interface declarations
        - Standalone procedures and functions
        - Unit dependencies (uses clause)
        - Overall complexity assessment
        
        Always respond with valid JSON only - no markdown, no explanations outside the JSON structure.
        """;

    private static string BuildTranslationSystemPrompt(TranslationOptions options)
    {
        var features = new List<string>();
        if (options.UseNullableReferenceTypes) features.Add("nullable reference types");
        if (options.UsePrimaryConstructors) features.Add("primary constructors where appropriate");
        if (options.UseRecordsForSimpleTypes) features.Add("records for simple data types");
        if (options.GenerateAsyncMethods) features.Add("async/await for I/O operations");
        if (options.GenerateXmlDocumentation) features.Add("XML documentation comments");

        return $$"""
            You are an expert code translator specializing in converting Delphi/Object Pascal to modern C#.
            
            Translation Guidelines:
            - Target .NET version: {{options.TargetFramework}}
            - Use modern C# features: {{string.Join(", ", features)}}
            - Preserve the original logic and structure where sensible
            - Convert Delphi types to appropriate C# equivalents
            - Handle property getters/setters appropriately
            - Convert Delphi events to C# events
            - Replace 'inherited' with 'base.'
            - Convert constructor/destructor patterns to IDisposable where appropriate
            
            CRITICAL NAMESPACE RULES:
            - Use ONE namespace per file at the TOP of the file
            - File-scoped namespace syntax: "namespace MyApp.Module;" (with semicolon, NO braces after it)
            - NEVER write the words "file-scoped namespace" in the output - that is NOT C# syntax
            - NEVER nest namespaces inside other namespaces
            - The namespace should combine the base namespace with the unit name
            
            CORRECT namespace example:
            ```
            using System;
            
            namespace ConvertedApp.uEmail;
            
            public class MyClass { }
            ```
            
            Type Mappings:
            - String → string
            - Integer → int
            - Boolean → bool
            - Real, Double → double
            - Currency → decimal
            - TStringList → List<string>
            - TDateTime → DateTime
            - TThread → DO NOT inherit from Thread (sealed). Use Task-based pattern with CancellationToken.
            - TService → Console application with async Main
            
            WINDOWS SERVICE CONVERSION - CRITICAL:
            Convert Windows Service (TService, ServiceBase) to a CONSOLE APPLICATION:
            - DO NOT use ServiceBase, ServiceName, BackgroundService, or IHostedService
            - DO NOT create OnStart/OnStop methods - put logic directly in Main
            - REMOVE any ServiceBase.Run calls
            - REMOVE any ServiceName = "..." assignments
            
            Pattern for Windows Service to Console App conversion:
            ```csharp
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            
            public class Program
            {
                private static readonly CancellationTokenSource _cts = new();
                private static bool _isRunning = true;
                
                public static async Task Main(string[] args)
                {
                    Console.CancelKeyPress += (s, e) =>
                    {
                        e.Cancel = true;
                        _cts.Cancel();
                        _isRunning = false;
                    };
                    
                    AppDomain.CurrentDomain.ProcessExit += (s, e) =>
                    {
                        _cts.Cancel();
                        _isRunning = false;
                    };
                    
                    Console.WriteLine("Service starting...");
                    
                    // === Put OnStart logic here ===
                    Gateway.Start();
                    
                    Console.WriteLine("Service started. Press Ctrl+C to stop.");
                    
                    try
                    {
                        while (_isRunning && !_cts.Token.IsCancellationRequested)
                        {
                            // Main processing loop
                            await Task.Delay(100, _cts.Token);
                        }
                    }
                    catch (OperationCanceledException) { }
                    
                    // === Put OnStop logic here ===
                    Gateway.Stop();
                    
                    Console.WriteLine("Service stopped.");
                }
            }
            ```
            DO NOT include: OnStart, OnStop, ServiceName, ServiceBase, BackgroundService, IHostedService
            
            EMAIL/SMTP COMPONENT CONVERSION - CRITICAL:
            Convert ALL Delphi email components to MailKit. DO NOT leave any Delphi component references:
            - TIdSMTP, TclSmtp, clSmtp, FSmtp → SmtpClient (from MailKit.Net.Smtp)
            - TIdMessage, TclMailMessage, clMailMessage, ClMailMessage, FMailMessage → MimeMessage (from MimeKit)
            - TIdAttachment → MimePart with ContentDisposition.Attachment
            - TIdText → TextPart
            - Remove ALL .Progress, .SendCommand, .BodyEncoding, .SubjectEncoding event handlers
            
            DO NOT create properties like "public MimeMessage ClMailMessage" - convert the entire pattern.
            
            Pattern for email class:
            ```csharp
            using MailKit.Net.Smtp;
            using MailKit.Security;
            using MimeKit;
            
            public class EmailService : IDisposable
            {
                private SmtpClient? _client;
                private MimeMessage? _message;
                
                public string? SmtpHost { get; set; }
                public int SmtpPort { get; set; } = 587;
                public string? Username { get; set; }
                public string? Password { get; set; }
                
                public async Task SendEmailAsync(string from, string to, string subject, string body)
                {
                    _message = new MimeMessage();
                    _message.From.Add(MailboxAddress.Parse(from));
                    _message.To.Add(MailboxAddress.Parse(to));
                    _message.Subject = subject;
                    _message.Body = new TextPart("plain") { Text = body };
                    
                    _client = new SmtpClient();
                    await _client.ConnectAsync(SmtpHost, SmtpPort, SecureSocketOptions.StartTls);
                    if (!string.IsNullOrEmpty(Username))
                        await _client.AuthenticateAsync(Username, Password);
                    await _client.SendAsync(_message);
                    await _client.DisconnectAsync(true);
                }
                
                public void Dispose() => _client?.Dispose();
            }
            ```
            
            READONLY FIELD RULES:
            - NEVER assign to readonly fields outside of constructors
            - If a field needs to be reassigned, do NOT mark it as readonly
            - Use "private SqlConnection? _connection;" NOT "private readonly SqlConnection _connection;"
            
            NULLABLE REFERENCE TYPES:
            - Database connections should be nullable: "private FbConnection? _connection;"
            - Check for null before using: "if (_connection != null)"
            - Use null-coalescing: "path ?? string.Empty"
            
            THREAD CONVERSION - CRITICAL:
            Thread is SEALED in .NET. Convert TThread to:
            ```csharp
            public class EmailSender : IDisposable
            {
                private readonly CancellationTokenSource _cts = new();
                private Task? _task;
                
                public void Start() => _task = Task.Run(ExecuteAsync);
                private async Task ExecuteAsync() { /* logic */ }
                public void Dispose() { _cts.Cancel(); _task?.Wait(); _cts.Dispose(); }
            }
            ```
            
            DATABASE CONVERSION - FIREBIRD ONLY:
            NEVER use SqlConnection/SqlCommand. Always use FirebirdSql.Data.FirebirdClient:
            - TADOConnection → FbConnection
            - TADOQuery → FbCommand with Dapper
            
            Output ONLY valid C# code, no explanations or markdown.
            """;
    }

    private static string BuildFormTranslationSystemPrompt(TranslationOptions options)
    {
        var targetDescription = options.UITarget switch
        {
            UITargetFramework.React => "React functional component with TypeScript",
            UITargetFramework.Blazor => "Blazor component",
            UITargetFramework.WinForms => "Windows Forms",
            UITargetFramework.WPF => "WPF XAML",
            UITargetFramework.MAUI => ".NET MAUI",
            _ => "C# code"
        };

        return $"""
            You are an expert UI code translator for converting Delphi VCL forms to {targetDescription}.
            Convert components appropriately and preserve layout. Output ONLY valid code, no explanations.
            """;
    }

    private static string BuildReactFormTranslationSystemPrompt() =>
        """
        You are an expert UI translator converting Delphi VCL forms to React with TypeScript.
        Use functional components with hooks. Replace database calls with fetch API calls.
        Output ONLY valid TypeScript React code (.tsx), no explanations.
        """;

    private static string BuildDatabaseAnalysisSystemPrompt() =>
        """
        You are an expert Delphi database code analyzer. Extract ALL database operations from Delphi code.
        Look for TQuery, SQL.Text, ExecSQL, transactions, ParamByName, FieldByName.
        Always respond with valid JSON only - no markdown, no explanations.
        """;

    private static string BuildRepositoryGenerationSystemPrompt(TranslationOptions options) =>
        $$"""
        You are an expert C# developer generating repository classes for Firebird using Dapper.
        Start with: namespace {{options.BaseNamespace}}.{{options.ApiOptions.RepositoryNamespace}};
        Inherit from BaseRepository. Use async methods. Output ONLY valid C# code.
        """;

    private static string BuildControllerGenerationSystemPrompt(TranslationOptions options) =>
        $$"""
        You are an expert C# developer generating ASP.NET Core API controllers.
        Start with: namespace {{options.BaseNamespace}}.{{options.ApiOptions.ControllerNamespace}};
        Use [ApiController], async actions, Swagger attributes. Output ONLY valid C# code.
        """;

    private static string BuildReactGenerationSystemPrompt() =>
        """
        You are an expert React/TypeScript developer converting Delphi forms to React.
        Use functional components with hooks. Replace database calls with fetch API.
        Respond with JSON: { "componentCode": "...", "cssCode": "...", "typesCode": "...", "imports": [...], "apiCalls": [...] }
        """;

    private static string BuildApiAwareTranslationSystemPrompt(TranslationOptions options)
    {
        var features = new List<string>();
        if (options.UseNullableReferenceTypes) features.Add("nullable reference types");
        if (options.UsePrimaryConstructors) features.Add("primary constructors");
        if (options.UseRecordsForSimpleTypes) features.Add("records for DTOs");
        if (options.GenerateAsyncMethods) features.Add("async/await");

        return $$"""
            Convert Delphi to C# replacing ALL database operations with HttpClient API calls.
            Target: {{options.TargetFramework}}. Features: {{string.Join(", ", features)}}.
            Use file-scoped namespace. Inject HttpClient. Output ONLY valid C# code.
            """;
    }

    #endregion
}

internal class OllamaChatResponse
{
    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; set; }
}

internal class OllamaMessage
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class UnitAnalysisResult
{
    public List<ClassInfo> Classes { get; set; } = [];
    public List<RecordInfo> Records { get; set; } = [];
    public List<string> Interfaces { get; set; } = [];
    public List<string> StandaloneFunctions { get; set; } = [];
    public List<string> Dependencies { get; set; } = [];
    public string Complexity { get; set; } = "medium";
    public string Notes { get; set; } = string.Empty;
}

public class ClassInfo
{
    public string Name { get; set; } = string.Empty;
    public string? ParentClass { get; set; }
    public List<string> Methods { get; set; } = [];
    public List<string> Properties { get; set; } = [];
}

public class RecordInfo
{
    public string Name { get; set; } = string.Empty;
    public List<string> Fields { get; set; } = [];
}

public class DatabaseAnalysisResult
{
    [JsonPropertyName("operations")]
    public List<AnalyzedDbOperation> Operations { get; set; } = [];

    [JsonPropertyName("transactionGroups")]
    public List<AnalyzedTransactionGroup> TransactionGroups { get; set; } = [];

    [JsonPropertyName("dtoSuggestions")]
    public List<DtoSuggestion> DtoSuggestions { get; set; } = [];
}

public class AnalyzedDbOperation
{
    [JsonPropertyName("methodName")]
    public string MethodName { get; set; } = "";

    [JsonPropertyName("className")]
    public string ClassName { get; set; } = "";

    [JsonPropertyName("sqlStatement")]
    public string? SqlStatement { get; set; }

    [JsonPropertyName("operationType")]
    public string OperationType { get; set; } = "Unknown";

    [JsonPropertyName("tableName")]
    public string? TableName { get; set; }

    [JsonPropertyName("parameters")]
    public List<AnalyzedParameter> Parameters { get; set; } = [];

    [JsonPropertyName("isPartOfTransaction")]
    public bool IsPartOfTransaction { get; set; }

    [JsonPropertyName("transactionPeers")]
    public List<string> TransactionPeers { get; set; } = [];
}

public class AnalyzedParameter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("delphiType")]
    public string DelphiType { get; set; } = "";

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "Input";
}

public class AnalyzedTransactionGroup
{
    [JsonPropertyName("methodName")]
    public string MethodName { get; set; } = "";

    [JsonPropertyName("operations")]
    public List<string> Operations { get; set; } = [];
}

public class DtoSuggestion
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("fields")]
    public List<DtoFieldSuggestion> Fields { get; set; } = [];

    [JsonPropertyName("basedOnTable")]
    public string? BasedOnTable { get; set; }
}

public class DtoFieldSuggestion
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

public class ReactGenerationResult
{
    [JsonPropertyName("componentCode")]
    public string ComponentCode { get; set; } = "";

    [JsonPropertyName("cssCode")]
    public string? CssCode { get; set; }

    [JsonPropertyName("typesCode")]
    public string? TypesCode { get; set; }

    [JsonPropertyName("imports")]
    public List<string> Imports { get; set; } = [];

    [JsonPropertyName("apiCalls")]
    public List<string> ApiCalls { get; set; } = [];
}

/// <summary>
/// AI-generated description of a Delphi project's purpose and functionality.
/// </summary>
public class ProjectAiDescription
{
    /// <summary>
    /// Brief description of the application's primary purpose.
    /// </summary>
    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = string.Empty;

    /// <summary>
    /// The business domain the application operates in (e.g., Finance, Healthcare, Retail).
    /// </summary>
    [JsonPropertyName("businessDomain")]
    public string BusinessDomain { get; set; } = string.Empty;

    /// <summary>
    /// Key features and functionality identified in the application.
    /// </summary>
    [JsonPropertyName("keyFeatures")]
    public List<string> KeyFeatures { get; set; } = [];

    /// <summary>
    /// Main data entities/objects the application works with.
    /// </summary>
    [JsonPropertyName("keyEntities")]
    public List<string> KeyEntities { get; set; } = [];

    /// <summary>
    /// Technical summary of the application architecture.
    /// </summary>
    [JsonPropertyName("technicalSummary")]
    public string TechnicalSummary { get; set; } = string.Empty;
}