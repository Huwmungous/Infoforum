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
        _maxFileSizeBytes = config.GetValue<long>("FileStorage:MaxFileSizeBytes", 10 * 1024 * 1024); // Default 10MB

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
            FileType = DetermineFileType(file.ContentType)
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

    private static FileContentType DetermineFileType(string contentType)
    {
        var ct = contentType.ToLowerInvariant();

        return ct switch
        {
            _ when ct.StartsWith("image/") => FileContentType.Image,
            _ when ct.StartsWith("text/") => FileContentType.Text,
            "application/pdf" => FileContentType.Pdf,
            _ when ct.Contains("document") || ct.Contains("word") => FileContentType.Document,
            _ when ct.Contains("zip") || ct == "application/x-zip-compressed" => FileContentType.Zip,
            _ => FileContentType.Unknown
        };
    }
}
