
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO.Compression;


namespace OllamaMcpServer.Services;

public class ZipCodeExtractor
{
    private readonly ILogger<ZipCodeExtractor> _logger;
    private readonly string[] _allowedExtensions;
    private readonly long _maxFileSize;
    private readonly int _maxFiles;

    public ZipCodeExtractor(IConfiguration config, ILogger<ZipCodeExtractor> logger)
    {
        _logger = logger;
        _allowedExtensions = config.GetSection("ContextIngestion:AcceptedExtensions").Get<string[]>() ?? new[] { ".cs", ".ts", ".html", ".json" };
        _maxFileSize = config.GetValue("ContextIngestion:MaxFileSizeBytes", 512_000);
        _maxFiles = config.GetValue("ContextIngestion:MaxFiles", 100);
    }

    public async Task<List<(string FileName, string Content)>> ExtractAsync(Stream zipStream)
    {
        var results = new List<(string, string)>();
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);

        foreach (var entry in archive.Entries
                     .Where(e => _allowedExtensions.Contains(Path.GetExtension(e.FullName), StringComparer.OrdinalIgnoreCase))
                     .OrderBy(e => e.FullName)
                     .Take(_maxFiles))
        {
            if (entry.Length > _maxFileSize) continue;

            using var reader = new StreamReader(entry.Open());
            var content = await reader.ReadToEndAsync();
            results.Add((entry.FullName, content));
        }

        _logger.LogInformation($"Extracted {results.Count} code files from zip.");
        return results;
    }
}
