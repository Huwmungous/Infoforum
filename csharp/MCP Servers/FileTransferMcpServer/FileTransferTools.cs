using System.IO.Compression;
using System.Text.Json;

namespace FileTransferMcpServer;

public static class FileTransferTools
{
    public static Task<object> UploadFile(JsonElement args)
    {
        var path = args.GetProperty("path").GetString()!;
        var base64Content = args.GetProperty("content").GetString()!;
        var overwrite = args.TryGetProperty("overwrite", out var ow) && ow.GetBoolean();

        if (!overwrite && File.Exists(path))
            throw new IOException($"File already exists: {path}");

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var bytes = Convert.FromBase64String(base64Content);
        File.WriteAllBytes(path, bytes);

        return Task.FromResult<object>(new
        {
            success = true,
            path,
            size = bytes.Length,
            uploaded = DateTime.UtcNow.ToString("o")
        });
    }

    public static Task<object> DownloadFile(JsonElement args)
    {
        var path = args.GetProperty("path").GetString()!;

        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        var bytes = File.ReadAllBytes(path);
        var base64Content = Convert.ToBase64String(bytes);
        var fileInfo = new FileInfo(path);

        return Task.FromResult<object>(new
        {
            success = true,
            path,
            content = base64Content,
            size = bytes.Length,
            fileName = Path.GetFileName(path),
            mimeType = GetMimeType(path),
            lastModified = fileInfo.LastWriteTimeUtc.ToString("o")
        });
    }

    public static Task<object> CreateZip(JsonElement args)
    {
        var sourcePath = args.GetProperty("sourcePath").GetString()!;
        var zipPath = args.GetProperty("zipPath").GetString()!;
        var includeBaseDirectory = args.TryGetProperty("includeBaseDirectory", out var ibd) && ibd.GetBoolean();

        if (!Directory.Exists(sourcePath) && !File.Exists(sourcePath))
            throw new FileNotFoundException($"Source not found: {sourcePath}");

        var zipDirectory = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(zipDirectory) && !Directory.Exists(zipDirectory))
            Directory.CreateDirectory(zipDirectory);

        if (File.Exists(zipPath))
            File.Delete(zipPath);

        if (Directory.Exists(sourcePath))
        {
            ZipFile.CreateFromDirectory(sourcePath, zipPath, CompressionLevel.Optimal, includeBaseDirectory);
        }
        else
        {
            using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
            archive.CreateEntryFromFile(sourcePath, Path.GetFileName(sourcePath));
        }

        var zipInfo = new FileInfo(zipPath);
        return Task.FromResult<object>(new
        {
            success = true,
            sourcePath,
            zipPath,
            size = zipInfo.Length,
            created = DateTime.UtcNow.ToString("o")
        });
    }

    public static Task<object> ExtractZip(JsonElement args)
    {
        var zipPath = args.GetProperty("zipPath").GetString()!;
        var extractPath = args.GetProperty("extractPath").GetString()!;
        var overwrite = args.TryGetProperty("overwrite", out var ow) && ow.GetBoolean();

        if (!File.Exists(zipPath))
            throw new FileNotFoundException($"Zip file not found: {zipPath}");

        if (!Directory.Exists(extractPath))
            Directory.CreateDirectory(extractPath);

        ZipFile.ExtractToDirectory(zipPath, extractPath, overwrite);

        var extractedFiles = Directory.GetFiles(extractPath, "*", SearchOption.AllDirectories);
        return Task.FromResult<object>(new
        {
            success = true,
            zipPath,
            extractPath,
            filesExtracted = extractedFiles.Length,
            files = extractedFiles.Take(50).ToList()
        });
    }

    public static Task<object> DownloadZip(JsonElement args)
    {
        var zipPath = args.GetProperty("zipPath").GetString()!;

        if (!File.Exists(zipPath))
            throw new FileNotFoundException($"Zip file not found: {zipPath}");

        var bytes = File.ReadAllBytes(zipPath);
        var base64Content = Convert.ToBase64String(bytes);

        return Task.FromResult<object>(new
        {
            success = true,
            zipPath,
            content = base64Content,
            size = bytes.Length,
            fileName = Path.GetFileName(zipPath)
        });
    }

    public static Task<object> UploadZip(JsonElement args)
    {
        var zipPath = args.GetProperty("zipPath").GetString()!;
        var base64Content = args.GetProperty("content").GetString()!;
        var overwrite = args.TryGetProperty("overwrite", out var ow) && ow.GetBoolean();

        if (!overwrite && File.Exists(zipPath))
            throw new IOException($"Zip file already exists: {zipPath}");

        var directory = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var bytes = Convert.FromBase64String(base64Content);
        File.WriteAllBytes(zipPath, bytes);

        return Task.FromResult<object>(new
        {
            success = true,
            zipPath,
            size = bytes.Length,
            uploaded = DateTime.UtcNow.ToString("o")
        });
    }

    public static Task<object> ListZipContents(JsonElement args)
    {
        var zipPath = args.GetProperty("zipPath").GetString()!;

        if (!File.Exists(zipPath))
            throw new FileNotFoundException($"Zip file not found: {zipPath}");

        using var archive = ZipFile.OpenRead(zipPath);
        var entries = archive.Entries.Select(e => new
        {
            name = e.FullName,
            compressedSize = e.CompressedLength,
            uncompressedSize = e.Length,
            lastModified = e.LastWriteTime.ToString("o")
        }).ToList();

        return Task.FromResult<object>(new
        {
            success = true,
            zipPath,
            entryCount = entries.Count,
            entries
        });
    }

    public static Task<object> GetFileBase64(JsonElement args)
    {
        var path = args.GetProperty("path").GetString()!;
        var maxSize = args.TryGetProperty("maxSize", out var ms) ? ms.GetInt64() : 10485760;

        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}");

        var fileInfo = new FileInfo(path);
        if (fileInfo.Length > maxSize)
            throw new IOException($"File too large: {fileInfo.Length} bytes (max: {maxSize})");

        var bytes = File.ReadAllBytes(path);
        var base64 = Convert.ToBase64String(bytes);

        return Task.FromResult<object>(new
        {
            success = true,
            path,
            base64,
            size = bytes.Length,
            fileName = Path.GetFileName(path)
        });
    }

    public static Task<object> WriteBase64ToFile(JsonElement args)
    {
        var path = args.GetProperty("path").GetString()!;
        var base64 = args.GetProperty("base64").GetString()!;
        var overwrite = args.TryGetProperty("overwrite", out var ow) && ow.GetBoolean();

        if (!overwrite && File.Exists(path))
            throw new IOException($"File already exists: {path}");

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var bytes = Convert.FromBase64String(base64);
        File.WriteAllBytes(path, bytes);

        return Task.FromResult<object>(new
        {
            success = true,
            path,
            size = bytes.Length,
            written = DateTime.UtcNow.ToString("o")
        });
    }

    public static Task<object> CompressFiles(JsonElement args)
    {
        var files = args.GetProperty("files").EnumerateArray().Select(f => f.GetString()!).ToArray();
        var zipPath = args.GetProperty("zipPath").GetString()!;

        var zipDirectory = Path.GetDirectoryName(zipPath);
        if (!string.IsNullOrEmpty(zipDirectory) && !Directory.Exists(zipDirectory))
            Directory.CreateDirectory(zipDirectory);

        if (File.Exists(zipPath))
            File.Delete(zipPath);

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var file in files)
        {
            if (!File.Exists(file))
                continue;
            archive.CreateEntryFromFile(file, Path.GetFileName(file));
        }

        var zipInfo = new FileInfo(zipPath);
        return Task.FromResult<object>(new
        {
            success = true,
            zipPath,
            filesCompressed = files.Length,
            size = zipInfo.Length
        });
    }

    private static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".csv" => "text/csv",
            ".html" => "text/html",
            ".css" => "text/css",
            ".js" => "application/javascript",
            _ => "application/octet-stream"
        };
    }
}
