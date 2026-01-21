using System.IO.Compression;

namespace CodeZip.Core;

/// <summary>
/// Creates zip archives containing only source code files.
/// </summary>
public sealed class SourceZipper
{
    private readonly CodeZipConfig _config;

    public SourceZipper(CodeZipConfig? config = null)
    {
        _config = config ?? CodeZipConfig.Load();
    }

    public ZipResult CreateZip(string sourcePath, bool skipPrune = false, Action<string>? progress = null)
    {
        if (!Directory.Exists(sourcePath))
            return ZipResult.Failed($"Source directory does not exist: {sourcePath}");

        if (!Directory.Exists(_config.OutputDirectory))
        {
            try { Directory.CreateDirectory(_config.OutputDirectory); }
            catch (Exception ex) { return ZipResult.Failed($"Failed to create output directory: {ex.Message}"); }
        }

        var prunedCount = 0;
        if (_config.PruneOnRun && !skipPrune)
        {
            progress?.Invoke("Pruning old zip files...");
            prunedCount = ZipPruner.PruneOldZips(_config.OutputDirectory, _config.RetentionDays);
            if (prunedCount > 0) progress?.Invoke($"Pruned {prunedCount} expired zip file(s)");
        }

        progress?.Invoke("Detecting project types...");
        var projectTypes = ProjectDetector.DetectProjectTypes(sourcePath);
        progress?.Invoke($"Detected: {ProjectDetector.GetDescription(projectTypes)}");

        var exclusionRules = new ExclusionRules(projectTypes, _config);

        var folderName = new DirectoryInfo(sourcePath).Name;
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HHmmss");
        var zipFileName = $"{folderName}_source_{timestamp}.zip";
        var zipFilePath = Path.Combine(_config.OutputDirectory, zipFileName);

        progress?.Invoke("Scanning files...");
        var (filesToInclude, excludedFileCount, excludedDirCount) = CollectFiles(sourcePath, exclusionRules);

        if (filesToInclude.Count == 0)
            return ZipResult.Failed("No source files found to zip after applying exclusions.");

        progress?.Invoke($"Found {filesToInclude.Count} source files ({excludedFileCount} files, {excludedDirCount} directories excluded)");

        try
        {
            progress?.Invoke("Creating zip archive...");
            CreateZipArchive(sourcePath, zipFilePath, filesToInclude, progress);
            var zipInfo = new FileInfo(zipFilePath);

            return ZipResult.Succeeded(zipFilePath, filesToInclude.Count, zipInfo.Length,
                projectTypes, excludedFileCount, excludedDirCount, prunedCount);
        }
        catch (Exception ex)
        {
            if (File.Exists(zipFilePath)) try { File.Delete(zipFilePath); } catch { }
            return ZipResult.Failed($"Failed to create zip: {ex.Message}");
        }
    }

    public (List<string> Included, int ExcludedFiles, int ExcludedDirs, ProjectType Types) DryRun(string sourcePath)
    {
        if (!Directory.Exists(sourcePath)) return ([], 0, 0, ProjectType.None);

        var projectTypes = ProjectDetector.DetectProjectTypes(sourcePath);
        var exclusionRules = new ExclusionRules(projectTypes, _config);
        var (files, excludedFiles, excludedDirs) = CollectFiles(sourcePath, exclusionRules);

        return (files, excludedFiles, excludedDirs, projectTypes);
    }

    private static (List<string> Files, int ExcludedFileCount, int ExcludedDirCount) CollectFiles(
        string rootPath, ExclusionRules rules)
    {
        var files = new List<string>();
        var excludedFileCount = 0;
        var excludedDirCount = 0;

        void ScanDirectory(string directory)
        {
            try
            {
                foreach (var file in Directory.GetFiles(directory))
                {
                    if (rules.ShouldExcludeFile(Path.GetFileName(file))) excludedFileCount++;
                    else files.Add(file);
                }

                foreach (var subDir in Directory.GetDirectories(directory))
                {
                    var dirName = new DirectoryInfo(subDir).Name;
                    if (rules.ShouldExcludeDirectory(dirName))
                    {
                        excludedDirCount++;
                        excludedFileCount += CountFilesRecursive(subDir);
                    }
                    else ScanDirectory(subDir);
                }
            }
            catch (UnauthorizedAccessException) { }
        }

        ScanDirectory(rootPath);
        return (files, excludedFileCount, excludedDirCount);
    }

    private static int CountFilesRecursive(string directory)
    {
        try { return Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length; }
        catch { return 0; }
    }

    private static void CreateZipArchive(string rootPath, string zipFilePath, List<string> files, Action<string>? progress)
    {
        using var zipStream = new FileStream(zipFilePath, FileMode.Create);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

        var rootPathNormalized = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var processedCount = 0;
        var lastProgressUpdate = 0;

        foreach (var file in files)
        {
            var fullPath = Path.GetFullPath(file);
            var relativePath = fullPath[rootPathNormalized.Length..];
            var entryName = relativePath.Replace('\\', '/');

            archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
            processedCount++;

            if (progress != null && (processedCount - lastProgressUpdate >= 100 || processedCount == files.Count))
            {
                progress($"Compressed {processedCount}/{files.Count} files...");
                lastProgressUpdate = processedCount;
            }
        }
    }
}
