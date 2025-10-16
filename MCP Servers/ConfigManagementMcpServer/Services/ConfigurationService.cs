using ConfigManagementMcpServer.Models;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ConfigManagementMcpServer.Services;

public class ConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigurationService(ILogger<ConfigurationService> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<ConfigResult> ReadConfigAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return new ConfigResult
                {
                    Success = false,
                    Message = $"File not found: {filePath}"
                };
            }

            var content = await File.ReadAllTextAsync(filePath);
            var config = JsonSerializer.Deserialize<Dictionary<string, object>>(content, _jsonOptions);

            return new ConfigResult
            {
                Success = true,
                Message = $"Configuration read from {filePath}",
                Data = config
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading config");
            return new ConfigResult { Success = false, Message = ex.Message };
        }
    }

    public async Task<ConfigResult> WriteConfigAsync(string filePath, Dictionary<string, object> config)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            return new ConfigResult
            {
                Success = true,
                Message = $"Configuration written to {filePath}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing config");
            return new ConfigResult { Success = false, Message = ex.Message };
        }
    }

    public async Task<ConfigResult> MergeConfigsAsync(string baseConfigPath, string overrideConfigPath, string outputPath)
    {
        try
        {
            var baseResult = await ReadConfigAsync(baseConfigPath);
            if (!baseResult.Success) return baseResult;

            var overrideResult = await ReadConfigAsync(overrideConfigPath);
            if (!overrideResult.Success) return overrideResult;

            var baseConfig = baseResult.Data as Dictionary<string, object> ?? new Dictionary<string, object>();
            var overrideConfig = overrideResult.Data as Dictionary<string, object> ?? new Dictionary<string, object>();

            var merged = MergeRecursive(baseConfig, overrideConfig);
            
            return await WriteConfigAsync(outputPath, merged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error merging configs");
            return new ConfigResult { Success = false, Message = ex.Message };
        }
    }

    private Dictionary<string, object> MergeRecursive(Dictionary<string, object> baseDict, Dictionary<string, object> overrideDict)
    {
        var result = new Dictionary<string, object>(baseDict);

        foreach (var kvp in overrideDict)
        {
            if (result.ContainsKey(kvp.Key))
            {
                if (kvp.Value is JsonElement overrideElement && result[kvp.Key] is JsonElement baseElement)
                {
                    if (overrideElement.ValueKind == JsonValueKind.Object && baseElement.ValueKind == JsonValueKind.Object)
                    {
                        var baseObj = JsonSerializer.Deserialize<Dictionary<string, object>>(baseElement.GetRawText());
                        var overrideObj = JsonSerializer.Deserialize<Dictionary<string, object>>(overrideElement.GetRawText());
                        result[kvp.Key] = MergeRecursive(baseObj ?? new(), overrideObj ?? new());
                        continue;
                    }
                }
            }
            result[kvp.Key] = kvp.Value;
        }

        return result;
    }

    public ConfigResult EncryptConnectionString(string connectionString, string? key = null)
    {
        try
        {
            var encryptionKey = key ?? "DefaultKey12345"; // In production, use secure key management
            var encrypted = EncryptStringAES(connectionString, encryptionKey);

            return new ConfigResult
            {
                Success = true,
                Message = "Connection string encrypted (AES-256)",
                Data = new 
                { 
                    EncryptedValue = encrypted,
                    Note = "Cross-platform AES encryption. Store key securely!"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error encrypting");
            return new ConfigResult { Success = false, Message = ex.Message };
        }
    }

    public ConfigResult DecryptConnectionString(string encryptedConnectionString, string? key = null)
    {
        try
        {
            var encryptionKey = key ?? "DefaultKey12345";
            var decrypted = DecryptStringAES(encryptedConnectionString, encryptionKey);

            return new ConfigResult
            {
                Success = true,
                Message = "Connection string decrypted",
                Data = new { DecryptedValue = decrypted }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error decrypting");
            return new ConfigResult { Success = false, Message = ex.Message };
        }
    }

    private string EncryptStringAES(string plainText, string key)
    {
        using var aes = Aes.Create();
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        aes.Key = keyBytes;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        using var msEncrypt = new MemoryStream();
        msEncrypt.Write(aes.IV, 0, aes.IV.Length);
        
        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
        using (var swEncrypt = new StreamWriter(csEncrypt))
        {
            swEncrypt.Write(plainText);
        }

        return Convert.ToBase64String(msEncrypt.ToArray());
    }

    private string DecryptStringAES(string cipherText, string key)
    {
        var fullCipher = Convert.FromBase64String(cipherText);
        
        using var aes = Aes.Create();
        var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        aes.Key = keyBytes;

        var iv = new byte[aes.IV.Length];
        var cipher = new byte[fullCipher.Length - iv.Length];

        Array.Copy(fullCipher, iv, iv.Length);
        Array.Copy(fullCipher, iv.Length, cipher, 0, cipher.Length);

        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
        using var msDecrypt = new MemoryStream(cipher);
        using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
        using var srDecrypt = new StreamReader(csDecrypt);
        
        return srDecrypt.ReadToEnd();
    }

    public async Task<ValidationResult> ValidateConfigAsync(string filePath)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            if (!File.Exists(filePath))
            {
                errors.Add($"File not found: {filePath}");
                return new ValidationResult { IsValid = false, Errors = errors.ToArray() };
            }

            var content = await File.ReadAllTextAsync(filePath);
            
            try
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, object>>(content, _jsonOptions);
                
                if (config == null)
                {
                    errors.Add("Configuration is null or empty");
                    return new ValidationResult { IsValid = false, Errors = errors.ToArray() };
                }

                if (config.ContainsKey("ConnectionStrings"))
                {
                    var connStrings = config["ConnectionStrings"];
                    if (connStrings is JsonElement element && element.ValueKind == JsonValueKind.Object)
                    {
                        var connDict = JsonSerializer.Deserialize<Dictionary<string, string>>(element.GetRawText());
                        if (connDict == null || connDict.Count == 0)
                        {
                            warnings.Add("ConnectionStrings section is empty");
                        }
                    }
                }
                else
                {
                    warnings.Add("No ConnectionStrings section found");
                }

                var commonSections = new[] { "Logging", "AllowedHosts" };
                foreach (var section in commonSections)
                {
                    if (!config.ContainsKey(section))
                    {
                        warnings.Add($"Common section '{section}' not found");
                    }
                }
            }
            catch (JsonException ex)
            {
                errors.Add($"Invalid JSON: {ex.Message}");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors.ToArray(),
                Warnings = warnings.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation error");
            errors.Add(ex.Message);
            return new ValidationResult { IsValid = false, Errors = errors.ToArray() };
        }
    }

    public async Task<ConfigResult> TransformConfigAsync(string sourceConfigPath, string environment, string outputPath)
    {
        try
        {
            var baseResult = await ReadConfigAsync(sourceConfigPath);
            if (!baseResult.Success) return baseResult;

            var config = baseResult.Data as Dictionary<string, object> ?? new Dictionary<string, object>();
            var transformed = ApplyEnvironmentTransformations(config, environment);

            return await WriteConfigAsync(outputPath, transformed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transforming config");
            return new ConfigResult { Success = false, Message = ex.Message };
        }
    }

    private Dictionary<string, object> ApplyEnvironmentTransformations(Dictionary<string, object> config, string environment)
    {
        var transformed = new Dictionary<string, object>(config);
        transformed["Environment"] = environment;

        if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
        {
            if (!transformed.ContainsKey("Logging"))
            {
                transformed["Logging"] = new Dictionary<string, object>
                {
                    ["LogLevel"] = new Dictionary<string, string>
                    {
                        ["Default"] = "Warning",
                        ["Microsoft"] = "Warning"
                    }
                };
            }
        }
        else if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
        {
            if (!transformed.ContainsKey("Logging"))
            {
                transformed["Logging"] = new Dictionary<string, object>
                {
                    ["LogLevel"] = new Dictionary<string, string>
                    {
                        ["Default"] = "Information",
                        ["Microsoft"] = "Information"
                    }
                };
            }
        }

        return transformed;
    }
}