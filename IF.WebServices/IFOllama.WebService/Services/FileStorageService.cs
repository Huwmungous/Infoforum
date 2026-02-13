using IFOllama.Classes.Models;

namespace IFOllama.WebService.Services;

public class FileStorageService
{
    private readonly string _storageBasePath;
    private readonly ILogger<FileStorageService> _logger;
    private readonly long _maxFileSizeBytes;

    public FileStorageService(ILogger<FileStorageService> logger, IConfiguration config)
    {
        _logger = logger;
        _storageBasePath = config["FileStorage:BasePath"] ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        _maxFileSizeBytes = config.GetValue<long>("FileStorage:MaxFileSizeBytes", 20 * 1024 * 1024); // Default 20MB

        if (!Directory.Exists(_storageBasePath))
        {
            Directory.CreateDirectory(_storageBasePath);
            _logger.LogInformation("Created file storage directory: {Path}", _storageBasePath);
        }
    }

    public async Task<FileAttachment> SaveFileAsync(IFormFile file, string conversationId)
    {
        if (file.Length > _maxFileSizeBytes)
        {
            throw new InvalidOperationException($"File size exceeds maximum allowed size of {_maxFileSizeBytes / (1024 * 1024)}MB");
        }

        var fileAttachment = new FileAttachment
        {
            FileName = file.FileName,
            ContentType = file.ContentType,
            SizeBytes = file.Length,
            FileType = DetermineFileType(file.ContentType, file.FileName)
        };

        var conversationPath = Path.Combine(_storageBasePath, conversationId);
        if (!Directory.Exists(conversationPath))
        {
            Directory.CreateDirectory(conversationPath);
        }

        var fileName = $"{fileAttachment.Id}_{file.FileName}";
        var filePath = Path.Combine(conversationPath, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        fileAttachment.StoragePath = filePath;
        _logger.LogInformation("Saved file {FileName} for conversation {ConversationId}", file.FileName, conversationId);

        return fileAttachment;
    }

    public async Task<byte[]> ReadFileAsync(string storagePath)
    {
        if (!File.Exists(storagePath))
        {
            throw new FileNotFoundException($"File not found: {storagePath}");
        }

        return await File.ReadAllBytesAsync(storagePath);
    }

    public async Task<string> ReadTextFileAsync(string storagePath)
    {
        if (!File.Exists(storagePath))
        {
            throw new FileNotFoundException($"File not found: {storagePath}");
        }

        return await File.ReadAllTextAsync(storagePath);
    }

    public string GetBase64Content(string storagePath)
    {
        if (!File.Exists(storagePath))
        {
            throw new FileNotFoundException($"File not found: {storagePath}");
        }

        var bytes = File.ReadAllBytes(storagePath);
        return Convert.ToBase64String(bytes);
    }

    public async Task DeleteFileAsync(string storagePath)
    {
        if (File.Exists(storagePath))
        {
            await Task.Run(() => File.Delete(storagePath));
            _logger.LogInformation("Deleted file: {Path}", storagePath);
        }
    }

    public async Task DeleteConversationFilesAsync(string conversationId)
    {
        var conversationPath = Path.Combine(_storageBasePath, conversationId);
        if (Directory.Exists(conversationPath))
        {
            await Task.Run(() => Directory.Delete(conversationPath, true));
            _logger.LogInformation("Deleted all files for conversation: {ConversationId}", conversationId);
        }
    }

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".cs", ".ts", ".tsx", ".js", ".jsx", ".json", ".xml", ".csv",
        ".log", ".py", ".java", ".cpp", ".c", ".h", ".hpp", ".css", ".scss", ".html",
        ".htm", ".sql", ".sh", ".bash", ".yml", ".yaml", ".toml", ".ini", ".cfg",
        ".config", ".csproj", ".sln", ".props", ".targets", ".razor", ".vue", ".svelte",
        ".rb", ".go", ".rs", ".swift", ".kt", ".gradle", ".ps1", ".psm1", ".psd1",
        ".dockerfile", ".env", ".gitignore", ".editorconfig", ".eslintrc", ".prettierrc",
        ".tf", ".proto", ".graphql", ".r", ".m", ".mm", ".pl", ".lua", ".ex", ".exs",
        ".erl", ".hs", ".fs", ".fsx", ".clj", ".scala", ".pas", ".dfm", ".dpr"
    };

    private static readonly HashSet<string> ZipExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".gz", ".tar", ".7z", ".rar"
    };

    private static FileContentType DetermineFileType(string contentType, string fileName)
    {
        var ext = Path.GetExtension(fileName)?.ToLowerInvariant() ?? "";

        // Extension-based detection first (more reliable than browser content-type)
        if (TextExtensions.Contains(ext))
            return FileContentType.Text;

        if (ZipExtensions.Contains(ext))
            return FileContentType.Zip;

        if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg")
            return FileContentType.Image;

        if (ext is ".pdf")
            return FileContentType.Pdf;

        if (ext is ".doc" or ".docx" or ".odt" or ".rtf")
            return FileContentType.Document;

        // Fall back to content-type
        var ct = contentType.ToLowerInvariant();

        return ct switch
        {
            _ when ct.StartsWith("image/") => FileContentType.Image,
            _ when ct.StartsWith("text/") => FileContentType.Text,
            "application/json" or "application/xml" or "application/javascript"
                or "application/x-yaml" or "application/x-sh" => FileContentType.Text,
            "application/pdf" => FileContentType.Pdf,
            _ when ct.Contains("document") || ct.Contains("word") => FileContentType.Document,
            _ when ct.Contains("zip") || ct == "application/x-zip-compressed"
                || ct == "application/x-gzip" => FileContentType.Zip,
            _ => FileContentType.Unknown
        };
    }
}
