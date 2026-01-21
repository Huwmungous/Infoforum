namespace CodeZip.Core;

/// <summary>
/// Handles pruning of old zip files from the output directory.
/// </summary>
public static class ZipPruner
{
    private const string ZipPattern = "*_source_*.zip";

    public static int PruneOldZips(string outputDirectory, int retentionDays)
    {
        if (!Directory.Exists(outputDirectory)) return 0;

        var cutoffDate = DateTime.Now.AddDays(-retentionDays);
        var prunedCount = 0;

        try
        {
            foreach (var file in Directory.GetFiles(outputDirectory, ZipPattern))
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        fileInfo.Delete();
                        prunedCount++;
                    }
                }
                catch { }
            }
        }
        catch { }

        return prunedCount;
    }

    public static IReadOnlyList<(string FilePath, DateTime CreatedAt)> GetFilesToPrune(
        string outputDirectory, int retentionDays)
    {
        if (!Directory.Exists(outputDirectory)) return [];

        var cutoffDate = DateTime.Now.AddDays(-retentionDays);
        var filesToPrune = new List<(string, DateTime)>();

        try
        {
            foreach (var file in Directory.GetFiles(outputDirectory, ZipPattern))
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < cutoffDate)
                    {
                        filesToPrune.Add((file, fileInfo.CreationTime));
                    }
                }
                catch { }
            }
        }
        catch { }

        return filesToPrune.OrderBy(f => f.Item2).ToList();
    }
}
