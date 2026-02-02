using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace ChitterChatterDistribution.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly DistributionOptions _options;
    private readonly ILogger<IndexModel> _logger;

    public string Version { get; private set; } = "1.0.0";
    public string FileName { get; private set; } = "";
    public string FileSize { get; private set; } = "";
    public bool IsAvailable { get; private set; }

    public IndexModel(IOptions<DistributionOptions> options, ILogger<IndexModel> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public void OnGet()
    {
        var distPath = _options.DistributionPath;
        
        // Look for the installer zip
        var zipPath = Path.Combine(distPath, "ChitterChatter-Setup.zip");
        
        if (File.Exists(zipPath))
        {
            IsAvailable = true;
            FileName = "ChitterChatter-Setup.zip";
            
            var fileInfo = new FileInfo(zipPath);
            FileSize = FormatFileSize(fileInfo.Length);
            
            // Try to read version from a version.txt file if present
            var versionPath = Path.Combine(distPath, "version.txt");
            if (File.Exists(versionPath))
            {
                Version = File.ReadAllText(versionPath).Trim();
            }
        }
        else
        {
            IsAvailable = false;
            _logger.LogWarning("Distribution file not found: {Path}", zipPath);
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        int order = 0;
        double size = bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        
        return $"{size:0.##} {sizes[order]}";
    }
}
