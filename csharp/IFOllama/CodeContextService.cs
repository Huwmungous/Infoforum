using FaissNet;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FaissIndex = FaissNet.Index;

namespace IFOllama
{
    public class CodeContextService : IDisposable
    {
        private const int EmbeddingDim = 1536; // matches your embedder output size
        private readonly HashSet<string> _extensions;
        private readonly FileSystemWatcher _watcher;
        private readonly IEmbeddingService _embedder;
        private readonly FaissIndex _faissIndex;
        private readonly Dictionary<string, List<long>> _fileToIds = new();
        private readonly ILogger<CodeContextService> _logger;
        private long _nextId = 1;

        public CodeContextService(IEmbeddingService embedder, IConfiguration configuration, ILogger<CodeContextService> logger)
        {
            _embedder = embedder;
            _logger = logger;
            _faissIndex = FaissIndex.CreateDefault(EmbeddingDim, MetricType.METRIC_INNER_PRODUCT);

            var codesetPath = configuration.GetValue<string>("CodeSet");
            if (string.IsNullOrEmpty(codesetPath))
            {
                _logger.LogError("RootPath must be specified in the configuration.");
                throw new ArgumentException("RootPath must be specified in the configuration.");
            }

            if (!Directory.Exists(codesetPath))
            {
                _logger.LogError("RootPath does not exist: {CodesetPath}", codesetPath);
                throw new DirectoryNotFoundException($"RootPath does not exist: {codesetPath}");
            }

            _logger.LogInformation("CodesetPath is {CodesetPath}", codesetPath);

            // Load extensions from appsettings.json
            var extensionsFromConfig = configuration.GetSection("Extensions").Get<List<string>>();
            _extensions = extensionsFromConfig != null
                ? new HashSet<string>(extensionsFromConfig, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            _logger.LogInformation("Extensions are {Extensions}", extensionsFromConfig);

            // Set up a single watcher for all files, then filter by extension
            _watcher = new FileSystemWatcher(codesetPath, "*.*")
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

            // Initial bulk index for extensions
            foreach (var ext in _extensions)
            {
                foreach (var file in Directory.EnumerateFiles(codesetPath, $"*{ext}", SearchOption.AllDirectories))
                {
                    _logger.LogInformation("Indexing file: {File}", file);
                    IndexFile(file).GetAwaiter().GetResult();
                }
            }
        }

        private async void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (!_extensions.Contains(Path.GetExtension(e.FullPath))) return;

            _logger.LogInformation("File changed: {FilePath}", e.FullPath);

            // Remove old entries (if any)
            if (_fileToIds.TryGetValue(e.FullPath, out var oldIds))
            {
                _faissIndex.RemoveIds(oldIds.ToArray());
                _fileToIds.Remove(e.FullPath);
            }

            // Re-index the changed file
            await IndexFile(e.FullPath);
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            if (!_extensions.Contains(Path.GetExtension(e.FullPath))) return;

            _logger.LogInformation("File deleted: {FilePath}", e.FullPath);

            if (_fileToIds.TryGetValue(e.FullPath, out var oldIds))
            {
                _faissIndex.RemoveIds(oldIds.ToArray());
                _fileToIds.Remove(e.FullPath);
            }
        }

        private async void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            _logger.LogInformation("File renamed from {OldPath} to {NewPath}", e.OldFullPath, e.FullPath);

            await Task.Run(() =>
            {
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

            _logger.LogInformation("Indexing file: {FilePath}", path);

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

            _faissIndex.AddWithIds(embeds.ToArray(), ids.ToArray());
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
            var result = _faissIndex.Search(new[] { queryVec }, k);
            return (result.Item1, result.Item2);
        }

        public void Dispose()
        {
            _logger.LogInformation("Disposing CodeContextService.");
            _watcher.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
