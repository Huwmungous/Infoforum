using ConfigManagementMcpServer.Models;
using ConfigManagementMcpServer.Services;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ConfigManagementMcpServer.Protocol;

public class McpServer
{
    private readonly ConfigurationService _configService;
    private readonly ILogger<McpServer> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public McpServer(ConfigurationService configService, ILogger<McpServer> logger)
    {
        _configService = configService;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Configuration Management MCP Server starting");
        
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await Console.In.ReadLineAsync(cancellationToken);
                if (string.IsNullOrEmpty(line)) break;

                try
                {
                    var request = JsonSerializer.Deserialize<McpRequest>(line, _jsonOptions);
                    if (request == null) continue;

                    var response = await HandleRequestAsync(request);
                    var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
                    
                    await Console.Out.WriteLineAsync(responseJson);
                    await Console.Out.FlushAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Request error");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Shutdown requested");
        }
    }

    public async Task<McpResponse> HandleRequestAsync(McpRequest request)
    {
        return request.Method switch
        {
            "initialize" => HandleInitialize(request),
            "tools/list" => HandleToolsList(request),
            "tools/call" => await HandleToolCallAsync(request),
            _ => new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32601, Message = "Method not found" }
            }
        };
    }

    private McpResponse HandleInitialize(McpRequest request)
    {
        return new McpResponse
        {
            Id = request.Id,
            Result = new
            {
                protocolVersion = "2024-11-05",
                serverInfo = new { name = "config-management-mcp", version = "1.0.0" },
                capabilities = new { tools = new { } }
            }
        };
    }

    private McpResponse HandleToolsList(McpRequest request)
    {
        var tools = new[]
        {
            new ToolInfo
            {
                Name = "read_config",
                Description = "Read configuration file",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string" }
                    },
                    required = new[] { "filePath" }
                }
            },
            new ToolInfo
            {
                Name = "write_config",
                Description = "Write configuration to file",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string" },
                        config = new { type = "object" }
                    },
                    required = new[] { "filePath", "config" }
                }
            },
            new ToolInfo
            {
                Name = "merge_configs",
                Description = "Merge two configuration files",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        baseConfigPath = new { type = "string" },
                        overrideConfigPath = new { type = "string" },
                        outputPath = new { type = "string" }
                    },
                    required = new[] { "baseConfigPath", "overrideConfigPath", "outputPath" }
                }
            },
            new ToolInfo
            {
                Name = "encrypt_connection_string",
                Description = "Encrypt connection string using AES-256",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        connectionString = new { type = "string" },
                        key = new { type = "string" }
                    },
                    required = new[] { "connectionString" }
                }
            },
            new ToolInfo
            {
                Name = "decrypt_connection_string",
                Description = "Decrypt connection string",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        encryptedConnectionString = new { type = "string" },
                        key = new { type = "string" }
                    },
                    required = new[] { "encryptedConnectionString" }
                }
            },
            new ToolInfo
            {
                Name = "validate_config",
                Description = "Validate configuration file",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new { type = "string" }
                    },
                    required = new[] { "filePath" }
                }
            },
            new ToolInfo
            {
                Name = "transform_config",
                Description = "Transform config for environment",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourceConfigPath = new { type = "string" },
                        environment = new { type = "string" },
                        outputPath = new { type = "string" }
                    },
                    required = new[] { "sourceConfigPath", "environment", "outputPath" }
                }
            }
        };

        return new McpResponse { Id = request.Id, Result = new { tools } };
    }

    private async Task<McpResponse> HandleToolCallAsync(McpRequest request)
    {
        var paramsElement = (JsonElement)request.Params!;
        var toolName = paramsElement.GetProperty("name").GetString()!;
        var arguments = paramsElement.GetProperty("arguments");

        try
        {
            object result = toolName switch
            {
                "read_config" => await _configService.ReadConfigAsync(
                    arguments.GetProperty("filePath").GetString()!
                ),
                "write_config" => await _configService.WriteConfigAsync(
                    arguments.GetProperty("filePath").GetString()!,
                    JsonSerializer.Deserialize<Dictionary<string, object>>(arguments.GetProperty("config"))!
                ),
                "merge_configs" => await _configService.MergeConfigsAsync(
                    arguments.GetProperty("baseConfigPath").GetString()!,
                    arguments.GetProperty("overrideConfigPath").GetString()!,
                    arguments.GetProperty("outputPath").GetString()!
                ),
                "encrypt_connection_string" => _configService.EncryptConnectionString(
                    arguments.GetProperty("connectionString").GetString()!,
                    arguments.TryGetProperty("key", out var key) ? key.GetString() : null
                ),
                "decrypt_connection_string" => _configService.DecryptConnectionString(
                    arguments.GetProperty("encryptedConnectionString").GetString()!,
                    arguments.TryGetProperty("key", out var key2) ? key2.GetString() : null
                ),
                "validate_config" => await _configService.ValidateConfigAsync(
                    arguments.GetProperty("filePath").GetString()!
                ),
                "transform_config" => await _configService.TransformConfigAsync(
                    arguments.GetProperty("sourceConfigPath").GetString()!,
                    arguments.GetProperty("environment").GetString()!,
                    arguments.GetProperty("outputPath").GetString()!
                ),
                _ => throw new InvalidOperationException("Unknown tool")
            };

            return new McpResponse
            {
                Id = request.Id,
                Result = new
                {
                    content = new[]
                    {
                        new { type = "text", text = JsonSerializer.Serialize(result, _jsonOptions) }
                    }
                }
            };
        }
        catch (Exception ex)
        {
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32000, Message = ex.Message }
            };
        }
    }
}