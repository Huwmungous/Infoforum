using ConfigManagementMcpServer.Services;
using SfD.Mcp.Protocol.Models;
using System.Text.Json;

namespace ConfigManagementMcpServer.Protocol;

public class McpServer(ConfigurationService configService, ILogger<McpServer> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Returns a list of available tools supported by this server.
    /// </summary>
    public IEnumerable<object> GetAvailableTools()
    {
        return GetTools();
    }

    public async Task<string> HandleRequestStringAsync(string requestJson)
    {
        try
        {
            var request = JsonSerializer.Deserialize<McpRequest>(requestJson, _jsonOptions);
            if (request == null)
            {
                return JsonSerializer.Serialize(new McpResponse
                {
                    Error = new McpError { Code = -32700, Message = "Parse error" }
                }, _jsonOptions);
            }

            var response = await HandleRequestAsync(request);
            return JsonSerializer.Serialize(response, _jsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "JSON parsing error");
            return JsonSerializer.Serialize(new McpResponse
            {
                Error = new McpError { Code = -32700, Message = $"Parse error: {ex.Message}" }
            }, _jsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error handling request");
            return JsonSerializer.Serialize(new McpResponse
            {
                Error = new McpError { Code = -32603, Message = $"Internal error: {ex.Message}" }
            }, _jsonOptions);
        }
    }

    public async Task<McpResponse> HandleRequestAsync(McpRequest request)
    {
        logger.LogInformation("Handling request: {Method}", request.Method);

        try
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
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling request");
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32603, Message = ex.Message }
            };
        }
    }

    private static McpResponse HandleInitialize(McpRequest request)
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

    private static McpResponse HandleToolsList(McpRequest request)
    {
        object[] tools = GetTools();

        return new McpResponse
        {
            Id = request.Id,
            Result = new { tools }
        };
    }

    private static object[] GetTools()
    {
        return [
            new
            {
                name = "read_config",
                description = "Read and parse JSON/XML configuration files.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new
                        {
                            type = "string",
                            description = "Path to configuration file"
                        }
                    },
                    required = new[] { "filePath" }
                }
            },
            new
            {
                name = "write_config",
                description = "Write configuration object to JSON file.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new
                        {
                            type = "string",
                            description = "Path where config will be written"
                        },
                        config = new
                        {
                            type = "object",
                            description = "Configuration object to write"
                        }
                    },
                    required = new[] { "filePath", "config" }
                }
            },
            new
            {
                name = "merge_configs",
                description = "Merge two configuration files with override support.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        baseConfigPath = new
                        {
                            type = "string",
                            description = "Base configuration file path"
                        },
                        overrideConfigPath = new
                        {
                            type = "string",
                            description = "Override configuration file path"
                        },
                        outputPath = new
                        {
                            type = "string",
                            description = "Output file path for merged config"
                        }
                    },
                    required = new[] { "baseConfigPath", "overrideConfigPath", "outputPath" }
                }
            },
            new
            {
                name = "encrypt_connection_string",
                description = "Encrypt connection string using AES-256 for secure storage.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        connectionString = new
                        {
                            type = "string",
                            description = "Connection string to encrypt"
                        },
                        key = new
                        {
                            type = "string",
                            description = "Encryption key (optional, uses default if not provided)"
                        }
                    },
                    required = new[] { "connectionString" }
                }
            },
            new
            {
                name = "decrypt_connection_string",
                description = "Decrypt encrypted connection string.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        encryptedConnectionString = new
                        {
                            type = "string",
                            description = "Encrypted connection string"
                        },
                        key = new
                        {
                            type = "string",
                            description = "Decryption key (optional, uses default if not provided)"
                        }
                    },
                    required = new[] { "encryptedConnectionString" }
                }
            },
            new
            {
                name = "validate_config",
                description = "Validate configuration file structure and required sections.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new
                        {
                            type = "string",
                            description = "Path to configuration file to validate"
                        }
                    },
                    required = new[] { "filePath" }
                }
            },
            new
            {
                name = "transform_config",
                description = "Transform configuration for specific environment (Development/Production).",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        sourceConfigPath = new
                        {
                            type = "string",
                            description = "Source configuration file path"
                        },
                        environment = new
                        {
                            type = "string",
                            description = "Target environment (Development, Production, etc.)"
                        },
                        outputPath = new
                        {
                            type = "string",
                            description = "Output path for transformed config"
                        }
                    },
                    required = new[] { "sourceConfigPath", "environment", "outputPath" }
                }
            }
        ];
    }

    private async Task<McpResponse> HandleToolCallAsync(McpRequest request)
    {
        var arguments = request.Params?.Arguments;

        try
        {
            logger.LogInformation("Executing tool: {ToolName}", request.Params?.Name);

            if (arguments == null)
            {
                throw new ArgumentException("Missing arguments");
            }

            object result = request.Params?.Name switch
            {
                "read_config" => await configService.ReadConfigAsync(
                    GetRequiredArg(arguments, "filePath")),

                "write_config" => await configService.WriteConfigAsync(
                    GetRequiredArg(arguments, "filePath"),
                    GetRequiredConfigArg(arguments, "config")),

                "merge_configs" => await configService.MergeConfigsAsync(
                    GetRequiredArg(arguments, "baseConfigPath"),
                    GetRequiredArg(arguments, "overrideConfigPath"),
                    GetRequiredArg(arguments, "outputPath")),

                "encrypt_connection_string" => configService.EncryptConnectionString(
                    GetRequiredArg(arguments, "connectionString"),
                    GetOptionalArg(arguments, "key")),

                "decrypt_connection_string" => configService.DecryptConnectionString(
                    GetRequiredArg(arguments, "encryptedConnectionString"),
                    GetOptionalArg(arguments, "key")),

                "validate_config" => await configService.ValidateConfigAsync(
                    GetRequiredArg(arguments, "filePath")),

                "transform_config" => await configService.TransformConfigAsync(
                    GetRequiredArg(arguments, "sourceConfigPath"),
                    GetRequiredArg(arguments, "environment"),
                    GetRequiredArg(arguments, "outputPath")),

                _ => throw new Exception($"Unknown tool: {request.Params?.Name}")
            };

            return new McpResponse
            {
                Id = request.Id,
                Result = new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = JsonSerializer.Serialize(result, _jsonOptions)
                        }
                    }
                }
            };
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid arguments");
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32602, Message = $"Invalid params: {ex.Message}" }
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error executing tool");
            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError { Code = -32000, Message = ex.Message }
            };
        }
    }

    private static string GetRequiredArg(JsonElement? arguments, string name)
    {
        if (arguments == null || !arguments.Value.TryGetProperty(name, out var value))
        {
            throw new ArgumentException($"Missing required argument: {name}");
        }

        var stringValue = value.GetString();
        if (string.IsNullOrEmpty(stringValue))
        {
            throw new ArgumentException($"Argument '{name}' cannot be empty");
        }

        return stringValue;
    }

    private static string? GetOptionalArg(JsonElement? arguments, string name)
    {
        if (arguments == null || !arguments.Value.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.GetString();
    }

    private static Dictionary<string, object> GetRequiredConfigArg(JsonElement? arguments, string name)
    {
        if (arguments == null || !arguments.Value.TryGetProperty(name, out var value))
        {
            throw new ArgumentException($"Missing required argument: {name}");
        }

        var config = JsonSerializer.Deserialize<Dictionary<string, object>>(value.GetRawText()) ?? throw new ArgumentException($"Argument '{name}' must be a valid object");
        return config;
    }
}