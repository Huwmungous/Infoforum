// RagService.cs 
using System.Text.Json; 
using FaissNet;
using FaissIndex = FaissNet.Index; 

public class RagService : IRagService
{
    private readonly string _chunksFile;
    private readonly FaissIndex _faiss;
    private readonly List<string> _chunks;
    private readonly IEmbeddingService _embedder;

    public RagService(IEmbeddingService embedder, IConfiguration configuration)
    {
        _embedder = embedder;

        // Read ChunksFile path from appsettings.json
        _chunksFile = configuration["ChunksFile"] ?? throw new InvalidOperationException("ChunksFile path is not configured.");

        if (!File.Exists(_chunksFile))
            throw new InvalidOperationException($"Missing chunks metadata: {_chunksFile}");

        _chunks = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(_chunksFile))
            ?? throw new InvalidOperationException("Failed to read chunks.json");

        // Build FAISS index locally
        _faiss = FaissIndex.CreateDefault(768, MetricType.METRIC_INNER_PRODUCT);
        var embeddings = new List<float[]>();
        foreach (var text in _chunks)
            embeddings.Add(_embedder.EmbedAsync(text).GetAwaiter().GetResult());

        var ids = Enumerable.Range(0, embeddings.Count).Select(i => (long)i).ToArray();
        _faiss.AddWithIds(embeddings.ToArray(), ids);
    }

    public async Task<List<string>> GetTopChunksAsync(string query, int k = 3)
    {
        var vec = await _embedder.EmbedAsync(query);
        var (dists, inds) = _faiss.Search(new[] { vec }, k);
        return inds[0]
            .Where(i => i >= 0 && i < _chunks.Count)
            .Select(i => _chunks[(int)i])
            .ToList();
    }
}

