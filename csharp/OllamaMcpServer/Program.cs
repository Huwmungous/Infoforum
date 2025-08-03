using IFGlobal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization; 

namespace OllamaMcpServer
{

    // MCP Server Implementation
    public class OllamaMcpServer
    {
        private readonly HttpClient httpClient;
        private readonly string ollamaBaseUrl;
        private readonly JsonSerializerOptions jsonOptions;

        public OllamaMcpServer(string ollamaUrl = "http://localhost:11434")
        {
            httpClient = new HttpClient();
            ollamaBaseUrl = ollamaUrl.TrimEnd('/');
            jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        public async Task RunAsync()
        {
            Console.Error.WriteLine("Starting Ollama MCP Server...");

            while (true)
            {
                try
                {
                    var line = await Console.In.ReadLineAsync();
                    if (line == null) break;

                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var request = JsonSerializer.Deserialize<McpRequest>(line, jsonOptions);
                    if (request != null)
                    {
                        var response = await HandleRequestAsync(request);

                        var responseJson = JsonSerializer.Serialize(response, jsonOptions);
                        Console.WriteLine(responseJson);
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error processing request: {ex.Message}");
                    var errorResponse = new McpResponse
                    {
                        Error = new McpError
                        {
                            Code = -32603,
                            Message = "Internal error",
                            Data = ex.Message
                        }
                    };
                    Console.WriteLine(JsonSerializer.Serialize(errorResponse, jsonOptions));
                }
            }
        }

        private async Task<McpResponse> HandleRequestAsync(McpRequest request)
        {
            try
            {
                return request.Method switch
                {
                    "initialize" => await HandleInitializeAsync(request),
                    "tools/list" => await HandleToolsListAsync(request),
                    "tools/call" => await HandleToolCallAsync(request),
                    "resources/list" => await HandleResourcesListAsync(request),
                    "resources/read" => await HandleResourceReadAsync(request),
                    _ => new McpResponse
                    {
                        Id = request.Id,
                        Error = new McpError
                        {
                            Code = -32601,
                            Message = $"Method not found: {request.Method}"
                        }
                    }
                };
            }
            catch (Exception ex)
            {
                return new McpResponse
                {
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32603,
                        Message = "Internal error",
                        Data = ex.Message
                    }
                };
            }
        }

        private static Task<McpResponse> HandleInitializeAsync(McpRequest request)
        {
            return Task.FromResult(new McpResponse
            {
                Id = request.Id,
                Result = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new
                    {
                        tools = new { },
                        resources = new { }
                    },
                    serverInfo = new
                    {
                        name = "ollama-mcp-server",
                        version = "1.0.0"
                    }
                }
            });
        }

        private static Task<McpResponse> HandleToolsListAsync(McpRequest request)
        {
            var tools = new object[]
            {
                new
                {
                    name = "generate_text",
                    description = "Generate text using Ollama AI models",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            model = new
                            {
                                type = "string",
                                description = "The Ollama model to use"
                            },
                            prompt = new
                            {
                                type = "string",
                                description = "The prompt to send to the model"
                            },
                            temperature = new
                            {
                                type = "number",
                                description = "Temperature for text generation (0.0 to 1.0)",
                                minimum = 0.0,
                                maximum = 1.0
                            }
                        },
                        required = new[] { "model", "prompt" }
                    }
                },
                new
                {
                    name = "list_models",
                    description = "List all available Ollama models",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new { }
                    }
                }
            };

            return Task.FromResult(new McpResponse
            {
                Id = request.Id,
                Result = new
                {
                    tools
                }
            });
        }

        private async Task<McpResponse> HandleToolCallAsync(McpRequest request)
        {
            if (request.Params == null)
            {
                return new McpResponse
                {
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32602,
                        Message = "Invalid params"
                    }
                };
            }

            var paramsElement = (JsonElement)request.Params;
            var toolName = paramsElement.GetProperty("name").GetString();
            var arguments = paramsElement.GetProperty("arguments");

            return toolName switch
            {
                "generate_text" => await HandleGenerateTextAsync(request.Id, arguments),
                "list_models" => await HandleListModelsAsync(request.Id),
                _ => new McpResponse
                {
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32601,
                        Message = $"Unknown tool: {toolName}"
                    }
                }
            };
        }

        private async Task<McpResponse> HandleGenerateTextAsync(object? requestId, JsonElement arguments)
        {
            var model = arguments.GetProperty("model").GetString() ?? "";
            var prompt = arguments.GetProperty("prompt").GetString() ?? "";

            var options = new Dictionary<string, object>();
            if (arguments.TryGetProperty("temperature", out var tempElement))
            {
                options["temperature"] = tempElement.GetDouble();
            }

            var ollamaRequest = new OllamaGenerateRequest
            {
                Model = model,
                Prompt = prompt,
                Stream = false,
                Options = options.Count > 0 ? options : null
            };

            var requestJson = JsonSerializer.Serialize(ollamaRequest, jsonOptions);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync($"{ollamaBaseUrl}/api/generate", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new McpResponse
                {
                    Id = requestId,
                    Error = new McpError
                    {
                        Code = -32603,
                        Message = "Ollama API error",
                        Data = responseContent
                    }
                };
            }

            var ollamaResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseContent, jsonOptions);

            return new McpResponse
            {
                Id = requestId,
                Result = new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = ollamaResponse?.Response ?? ""
                        }
                    }
                }
            };
        }

        private async Task<McpResponse> HandleListModelsAsync(object? requestId)
        {
            // Call Ollama API to list models
            var response = await httpClient.GetAsync($"{ollamaBaseUrl}/api/tags");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new McpResponse
                {
                    Id = requestId,
                    Error = new McpError
                    {
                        Code = -32603,
                        Message = "Ollama API error",
                        Data = responseContent
                    }
                };
            }

            var ollamaResponse = JsonSerializer.Deserialize<OllamaListResponse>(responseContent, jsonOptions);

            // Project into List<object> so we can safely null-coalesce
            var modelInfos = ollamaResponse?.Models != null
                ? [.. ollamaResponse.Models
                    .Select(m => (object)new
                    {
                        name = m.Name,
                        size = m.Size,
                        modified = m.ModifiedAt
                    })]
                : new List<object>();

            // Return as MCP result
            return new McpResponse
            {
                Id = requestId,
                Result = new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = JsonSerializer.Serialize(modelInfos, jsonOptions)
                        }
                    }
                }
            };
        }

        private static Task<McpResponse> HandleResourcesListAsync(McpRequest request)
        {
            var resources = new object[]
            {
                new
                {
                    uri = "ollama://models",
                    name = "Available Models",
                    description = "List of available Ollama models",
                    mimeType = "application/json"
                }
            };

            return Task.FromResult(new McpResponse
            {
                Id = request.Id,
                Result = new
                {
                    resources
                }
            });
        }

        private async Task<McpResponse> HandleResourceReadAsync(McpRequest request)
        {
            if (request.Params == null)
            {
                return new McpResponse
                {
                    Id = request.Id,
                    Error = new McpError
                    {
                        Code = -32602,
                        Message = "Invalid params"
                    }
                };
            }

            var paramsElement = (JsonElement)request.Params;
            var uri = paramsElement.GetProperty("uri").GetString();

            if (uri == "ollama://models")
            {
                var response = await httpClient.GetAsync($"{ollamaBaseUrl}/api/tags");
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new McpResponse
                    {
                        Id = request.Id,
                        Error = new McpError
                        {
                            Code = -32603,
                            Message = "Failed to fetch models from Ollama"
                        }
                    };
                }

                return new McpResponse
                {
                    Id = request.Id,
                    Result = new
                    {
                        contents = new[]
                        {
                            new
                            {
                                uri,
                                mimeType = "application/json",
                                text = responseContent
                            }
                        }
                    }
                };
            }

            return new McpResponse
            {
                Id = request.Id,
                Error = new McpError
                {
                    Code = -32602,
                    Message = $"Unknown resource URI: {uri}"
                }
            };
        }
    }

    // Program Entry Point
    class Program
    {
        static async Task Main(string[] args)
        {
            var port = PortResolver.GetPort("IFOllama");
            var ollamaUrl = args.Length > 0 ? args[0] : $"http://localhost:{port}";
            var server = new OllamaMcpServer(ollamaUrl);
            await server.RunAsync();
        }
    }
}
