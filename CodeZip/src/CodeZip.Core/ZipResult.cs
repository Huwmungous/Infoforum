namespace CodeZip.Core;

/// <summary>
/// Represents the result of a zip operation.
/// </summary>
public sealed class ZipResult
{
    public bool Success { get; init; }
    public string? ZipFilePath { get; init; }
    public int FileCount { get; init; }
    public long ZipSizeBytes { get; init; }
    public ProjectType DetectedTypes { get; init; }
    public int ExcludedFileCount { get; init; }
    public int ExcludedDirectoryCount { get; init; }
    public string? ErrorMessage { get; init; }
    public int PrunedFileCount { get; init; }

    public static ZipResult Succeeded(
        string zipFilePath, int fileCount, long zipSizeBytes, ProjectType detectedTypes,
        int excludedFileCount, int excludedDirectoryCount, int prunedFileCount) => new()
    {
        Success = true,
        ZipFilePath = zipFilePath,
        FileCount = fileCount,
        ZipSizeBytes = zipSizeBytes,
        DetectedTypes = detectedTypes,
        ExcludedFileCount = excludedFileCount,
        ExcludedDirectoryCount = excludedDirectoryCount,
        PrunedFileCount = prunedFileCount
    };

    public static ZipResult Failed(string errorMessage) => new()
    {
        Success = false,
        ErrorMessage = errorMessage
    };
}
