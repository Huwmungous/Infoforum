using FaissNet;
using FaissIndex = FaissNet.Index;

namespace IFOllama
{
    public class CodeContextService : IDisposable
{
    private const int EmbeddingDim = 1536; // matches your embedder output size
    private static readonly HashSet<string> _extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".cs", ".ts", ".sql", ".scss", ".css" };

    private readonly FileSystemWatcher _watcher;
    private readonly IEmbeddingService _embedder;
    private readonly FaissIndex _faissIndex;
    private readonly Dictionary<string, List<long>> _fileToIds = [];
    private long _nextId = 1;

    public CodeContextService(string rootPath, IEmbeddingService embedder)
    {
        _embedder = embedder;
        _faissIndex = FaissIndex.CreateDefault(EmbeddingDim, MetricType.METRIC_INNER_PRODUCT);

        // Set up a single watcher for all files, then filter by extension
        _watcher = new FileSystemWatcher(rootPath, "*.*")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite
                         | NotifyFilters.FileName
                         | NotifyFilters.DirectoryName,
            EnableRaisingEvents = true
        };

        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Deleted += OnFileDeleted;
        _watcher.Renamed += OnFileRenamed;

        // Initial bulk index for .cs, .ts, .sql
        foreach (var ext in _extensions)
        {
            foreach (var file in Directory.EnumerateFiles(rootPath, $"*{ext}", SearchOption.AllDirectories))
            {
                IndexFile(file).GetAwaiter().GetResult();
            }
        }
    }

    private async void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!_extensions.Contains(Path.GetExtension(e.FullPath))) return;

        // Remove old entries (if any)
        if (_fileToIds.TryGetValue(e.FullPath, out var oldIds))
        {
            _faissIndex.RemoveIds([.. oldIds]);
            _fileToIds.Remove(e.FullPath);
        }

        // Re-index the changed file
        await IndexFile(e.FullPath);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (!_extensions.Contains(Path.GetExtension(e.FullPath))) return;

        if (_fileToIds.TryGetValue(e.FullPath, out var oldIds))
        {
            _faissIndex.RemoveIds([.. oldIds]);
            _fileToIds.Remove(e.FullPath);
        }
    }

    private async void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        await Task.Run(() => {  
           OnFileDeleted(sender, new FileSystemEventArgs(
               WatcherChangeTypes.Deleted, Path.GetDirectoryName(e.OldFullPath)!, Path.GetFileName(e.OldFullPath)));
           OnFileChanged(sender, new FileSystemEventArgs(
               WatcherChangeTypes.Created, Path.GetDirectoryName(e.FullPath)!, Path.GetFileName(e.FullPath)));
        });
    }

    private async Task IndexFile(string path)
    {
        // Double-check extension
        if (!_extensions.Contains(Path.GetExtension(path))) return;

        var text = await File.ReadAllTextAsync(path);
        var chunks = ChunkText(text, maxChars: 2000).ToList();

        var ids = new List<long>();
        var embeds = new List<float[]>();

        foreach (var chunk in chunks)
        {
            var vec = await _embedder.EmbedAsync(chunk);
            embeds.Add(vec);
            ids.Add(_nextId++);
        }

        _faissIndex.AddWithIds([.. embeds], [.. ids]);
        _fileToIds[path] = ids;
    }

    private static IEnumerable<string> ChunkText(string txt, int maxChars)
    {
        for (int i = 0; i < txt.Length; i += maxChars)
            yield return txt.Substring(i, Math.Min(maxChars, txt.Length - i));
    }

    /// <summary>
    /// Runs a FAISS search on the single query vector; returns (distances, ids).
    /// </summary>
    public (float[][] distances, long[][] ids) Search(float[] queryVec, int k)
    {
        var result = _faissIndex.Search([queryVec], k);
        return (result.Item1, result.Item2);
    }

        public void Dispose()
        {
            _watcher.Dispose();
            GC.SuppressFinalize(this);
        }
    }


}