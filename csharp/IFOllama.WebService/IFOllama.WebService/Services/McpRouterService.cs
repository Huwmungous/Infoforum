using IFOllama.WebService.Models;
using IFGlobal;
using System.Text.Json;

namespace IFOllama.WebService.Services;

public class McpRouterService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<McpRouterService> _logger;
    private readonly Dictionary<string, string> _serverEndpoints;

    public McpRouterService(HttpClient httpClient, ILogger<McpRouterService> logger, IConfiguration config)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Determine base host based on build configuration
        string baseHost =
#if DEBUG
            "localhost";
#else
            config["Mcp:BaseHost"] ?? "sofia-d";
#endif

        // Use IFGlobal.PortResolver for centralised port management
        _serverEndpoints = new Dictionary<string, string>
        {
            {"BraveSearch",          $"http://{baseHost}:" + PortResolver.GetPort("BraveSearchMcpServer")},
            {"CodeAnalysis",         $"http://{baseHost}:" + PortResolver.GetPort("CodeAnalysisMcpServer")},
            {"CodeFormatter",        $"http://{baseHost}:" + PortResolver.GetPort("CodeFormatterMcpServer")},
            {"ConfigManagement",     $"http://{baseHost}:" + PortResolver.GetPort("ConfigManagementMcpServer")},
            {"DatabaseCompare",      $"http://{baseHost}:" + PortResolver.GetPort("DatabaseCompareMcpServer")},
            {"Documentation",        $"http://{baseHost}:" + PortResolver.GetPort("DocumentationMcpServer")},
            {"DotNetBuild",          $"http://{baseHost}:" + PortResolver.GetPort("DotNetBuildMcpServer")},
            {"FileSystem",           $"http://{baseHost}:" + PortResolver.GetPort("FileSystemMcpServer")},
            {"FileTransfer",         $"http://{baseHost}:" + PortResolver.GetPort("FileTransferMcpServer")},
            {"Firebird",             $"http://{baseHost}:" + PortResolver.GetPort("FirebirdMcpServer")},
            {"Git",                  $"http://{baseHost}:" + PortResolver.GetPort("GitMcpServer")},
            {"Playwright",           $"http://{baseHost}:" + PortResolver.GetPort("PlaywrightMcpServer")},
            {"SqlGenerator",         $"http://{baseHost}:" + PortResolver.GetPort("SqlGeneratorMcpServer")},
            {"Sqlite",               $"http://{baseHost}:" + PortResolver.GetPort("SqliteMcpServer")},
            {"TestGenerator",        $"http://{baseHost}:" + PortResolver.GetPort("TestGeneratorMcpServer")},
            {"UiComponentConverter", $"http://{baseHost}:" + PortResolver.GetPort("UiComponentConverterMcpServer")}
        };

        _logger.LogInformation("Initialised MCP router with {Count} server endpoints", _serverEndpoints.Count);
    }

    public async Task<List<ToolDefinition>> GetAllToolsAsync()
    {
        var allTools = new List<ToolDefinition>();

        foreach(var (serverName, baseUrl) in _serverEndpoints)
        {
            try
            {
                var tools = await GetToolsFromServerAsync(baseUrl, serverName);
                allTools.AddRange(tools);
                _logger.LogInformation("Loaded {Count} tools from {Server}", tools.Count, serverName);
            }
            catch(Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get tools from {Server} at {Url}", serverName, baseUrl);
            }
        }

        return allTools;
    }

    private async Task<List<ToolDefinition>> GetToolsFromServerAsync(string baseUrl, string serverName)
    {
        var endpoint = $"{baseUrl}/rpc";

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/list",
            id = 1
        };

        var response = await _httpClient.PostAsJsonAsync(endpoint, request);
        var rawContent = await response.Content.ReadAsStringAsync();

        if(!(response.Content.Headers.ContentType?.MediaType?.Contains("json") ?? false))
        {
            _logger.LogError("Expected JSON but got: {RawContent}", rawContent);
            throw new InvalidOperationException("Non-JSON response from MCP server.");
        }

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<McpResponse>();

        var tools = new List<ToolDefinition>();

        if(result?.Result != null)
        {
            var toolsJson = JsonSerializer.Serialize(result.Result);
            var resultDoc = JsonDocument.Parse(toolsJson);

            if(resultDoc.RootElement.TryGetProperty("tools", out var toolsList))
            {
                foreach(var tool in toolsList.EnumerateArray())
                {
                    tools.Add(new ToolDefinition
                    {
                        ServerName = serverName,
                        Name = tool.GetProperty("name").GetString()!,
                        Description = tool.GetProperty("description").GetString()!,
                        InputSchema = tool.GetProperty("inputSchema")
                    });
                }
            }
        }

        return tools;
    }

    public async Task<string> CallToolAsync(string serverName, string toolName, JsonElement arguments)
    {
        if(!_serverEndpoints.TryGetValue(serverName, out var baseUrl))
        {
            throw new InvalidOperationException($"Unknown server: {serverName}");
        }

        var endpoint = $"{baseUrl}/rpc";

        var request = new
        {
            jsonrpc = "2.0",
            method = "tools/call",
            @params = new
            {
                name = toolName,
                arguments
            },
            id = 1
        };

        _logger.LogInformation("Calling tool {Tool} on server {Server}", toolName, serverName);

        var response = await _httpClient.PostAsJsonAsync(endpoint, request);
        var rawContent = await response.Content.ReadAsStringAsync();

        if(!(response.Content.Headers.ContentType?.MediaType?.Contains("json") ?? false))
        {
            _logger.LogError("Expected JSON but got: {RawContent}", rawContent);
            throw new InvalidOperationException("Non-JSON response from MCP server.");
        }

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<McpResponse>();

        if(result?.Error != null)
        {
            _logger.LogError("MCP Error from {Server}.{Tool}: Code={Code}, Message={Message}",
                serverName, toolName, result.Error.Code, result.Error.Message);
            throw new InvalidOperationException($"MCP Error: {result.Error.Message}");
        }

        // Extract text content from MCP response
        if(result?.Result != null)
        {
            var resultJson = JsonSerializer.Serialize(result.Result);
            var resultDoc = JsonDocument.Parse(resultJson);

            if(resultDoc.RootElement.TryGetProperty("content", out var content))
            {
                var firstContent = content.EnumerateArray().FirstOrDefault();
                if(firstContent.ValueKind != JsonValueKind.Undefined &&
                    firstContent.TryGetProperty("text", out var text))
                {
                    return text.GetString() ?? "{}";
                }
            }

            // Return the result as JSON if no text content found
            return resultJson;
        }

        return "{}";
    }

    public IReadOnlyDictionary<string, string> GetServerEndpoints() => _serverEndpoints;
}