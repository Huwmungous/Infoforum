using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HNSW.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IFOllama
{
    public class CodeContextService : IDisposable
    {
        private const int MaxChunkSize = 2000;
        private readonly IEmbeddingService _embedder;
        private readonly ILogger<CodeContextService> _logger;
        private readonly HashSet<string> _extensions;
        private readonly FileSystemWatcher _watcher;
        private SmallWorld<float[], float> _hnswIndex;
        private readonly SmallWorld<float[], float>.Parameters _parameters;
        private readonly IProvideRandomValues _rng = DefaultRandomGenerator.Instance;

        // Keep chunks for ID→text if you need it
        private List<string> _allChunks = new();

        public CodeContextService(
            IEmbeddingService embedder,
            IConfiguration configuration,
            ILogger<CodeContextService> logger)
        {
            _embedder = embedder;
            _logger = logger;

            // 1) Load code­set path & extensions
            var root = configuration["CodeSet"]
                       ?? throw new ArgumentException("CodeSet must be specified");
            if (!Directory.Exists(root))
                throw new DirectoryNotFoundException($"CodeSet not found: {root}");

            var exts = configuration.GetSection("Extensions").Get<List<string>>() ?? new();
            _extensions = new HashSet<string>(exts, StringComparer.OrdinalIgnoreCase);

            // 2) HNSW parameters + constructor (four­-arg)
            _parameters = new SmallWorld<float[], float>.Parameters
            {
                M = 32,
                LevelLambda = 1.0 / Math.Log(32)
            };
            _hnswIndex = new SmallWorld<float[], float>(
                CosineDistance.NonOptimized,
                _rng,
                _parameters
            );

            // 3) Initial index
            RebuildIndex(root);

            // 4) Watch & rebuild on any file change
            _watcher = new FileSystemWatcher(root, "*.*")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite
                                      | NotifyFilters.FileName
                                      | NotifyFilters.DirectoryName,
                EnableRaisingEvents = true
            };
            _watcher.Changed += (_, e) => RebuildIndex(root);
            _watcher.Created += (_, e) => RebuildIndex(root);
            _watcher.Deleted += (_, e) => RebuildIndex(root);
            _watcher.Renamed += (_, e) => RebuildIndex(root);
        }

        private readonly object _lock = new();

        private void RebuildIndex(string rootPath)
        {
            _logger.LogInformation("Rebuilding HNSW index...");

            // 1) Gather & chunk all source files
            var chunks = new List<string>();
            foreach (var ext in _extensions)
            {
                foreach (var file in Directory.EnumerateFiles(rootPath, $"*{ext}", SearchOption.AllDirectories))
                    chunks.AddRange(ChunkText(File.ReadAllText(file), MaxChunkSize));
            }

            // 2) Embed every chunk
            var vectors = chunks
                .Select(txt => _embedder.EmbedAsync(txt).GetAwaiter().GetResult())
                .ToArray();

            // 3) Instantiate a fresh graph
            var newGraph = new SmallWorld<float[], float>(
                CosineDistance.NonOptimized,
                _rng,
                _parameters
            );

            // 4) Bulk‐insert all vectors
            newGraph.AddItems(vectors);

            // 5) Swap in under lock, update chunk mapping
            lock (_lock)
            {
                _hnswIndex = newGraph;
                _allChunks = chunks;
            }
        }


        private static IEnumerable<string> ChunkText(string txt, int maxLen)
        {
            for (int i = 0; i < txt.Length; i += maxLen)
                yield return txt.Substring(i, Math.Min(maxLen, txt.Length - i));
        }

        /// <summary>
        /// Thread-safe K-NN search returning (distances, ids).
        /// </summary>
        public (float[][] distances, long[][] ids) Search(float[] queryVec, int k)
        {
            lock (_lock)
            {
                var results = _hnswIndex.KNNSearch(queryVec, k);
                var distances = results.Select(r => r.Distance).ToArray();
                var ids = results.Select(r => (long)r.Id).ToArray();
                return (new[] { distances }, new[] { ids });
            }
        }

        public void Dispose()
        {
            _watcher.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Embed a text query, run K-NN, and return the top code snippets.
        /// </summary>
        public async Task<List<string>> GetTopChunksAsync(string query, int k = 3)
        {
            // 1) Embed
            var qvec = await _embedder.EmbedAsync(query);

            // 2) Search
            (var distsArr, var idsArr) = Search(qvec, k);
            var ids = idsArr[0];

            // 3) Map IDs → chunk text (guarding bounds)
            var results = new List<string>();
            foreach (var id in ids)
            {
                if (id >= 0 && id < _allChunks.Count)
                    results.Add(_allChunks[(int)id]);
            }
            return results;
        }

    }
}
