 
using System.Text.Json; 
using HNSW.Net;       

namespace IFOllama
{
    public class RagService : IRagService
    {
        private readonly SmallWorld<float[], float> _hnsw;
        private readonly List<string> _chunks;
        private readonly IEmbeddingService _embedder;

        // optional if you want to reuse the same RNG
        private readonly IProvideRandomValues _rng = DefaultRandomGenerator.Instance;

        public RagService(
            IEmbeddingService embedder,
            IConfiguration configuration,
            ILogger<RagService> logger)
        {
            _embedder = embedder;

            // 1) load your pre‐split chunks
            var path = configuration["ChunksFile"]
                       ?? throw new InvalidOperationException("ChunksFile must be configured.");
            if (!File.Exists(path))
                throw new FileNotFoundException("Chunks file not found", path);

            _chunks = JsonSerializer
                .Deserialize<List<string>>(File.ReadAllText(path))
                ?? throw new InvalidOperationException("Failed to parse chunks.json");

            // 2) set up HNSW parameters
            var parameters = new SmallWorld<float[], float>.Parameters
            {
                M = 16,
                LevelLambda = 1.0 / Math.Log(16)
            };

            // 3) instantiate the graph (distance, RNG, params)
            _hnsw = new SmallWorld<float[], float>(
                CosineDistance.NonOptimized,
                _rng,
                parameters
            );

            // 4) embed all chunks and add in bulk
            var vectors = _chunks
                .Select(text => _embedder.EmbedAsync(text).GetAwaiter().GetResult())
                .ToArray();
            _hnsw.AddItems(vectors);
        }

        public async Task<List<string>> GetTopChunksAsync(string query, int k = 3)
        {
            var qvec = await _embedder.EmbedAsync(query);
            var neighbors = _hnsw.KNNSearch(qvec, k);

            return neighbors
                .Where(r => r.Id >= 0 && r.Id < _chunks.Count)
                .Select(r => _chunks[r.Id])
                .ToList();
        }

        public IEmbeddingService GetEmbeddingService() => _embedder;
    }
}
