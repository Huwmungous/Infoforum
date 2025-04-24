// RagService.cs
 
using System.Text;
using System.Text.Json; 
using FaissNet;
using FaissIndex = FaissNet.Index;

public class RagService : IRagService
{
    private const string ChunksMetaFile = "chunks.json";
    private const int EmbeddingDimension = 1536; // text-embedding-ada-002

    private readonly IHttpClientFactory _httpFactory;
    private readonly FaissIndex _faissIndex;
    private readonly List<string> _chunks;

    public RagService(IHttpClientFactory httpFactory)
    {
        _httpFactory = httpFactory ?? throw new ArgumentNullException(nameof(httpFactory));

        if (!File.Exists(ChunksMetaFile))
            throw new InvalidOperationException($"Chunks metadata file not found: {ChunksMetaFile}");

        _chunks = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(ChunksMetaFile))
            ?? throw new InvalidOperationException("Failed to deserialize chunks.json");

        // Create a FAISS index for inner-product similarity
        _faissIndex = FaissIndex.CreateDefault(EmbeddingDimension, MetricType.METRIC_INNER_PRODUCT);

        // Synchronously embed and add all chunks at startup
        var embeddings = new List<float[]>();
        foreach (var chunk in _chunks)
        {
            var embed = GetEmbeddingAsync(chunk).GetAwaiter().GetResult();
            embeddings.Add(embed);
        }
        var ids = Enumerable.Range(0, embeddings.Count).Select(i => (long)i).ToArray();
        _faissIndex.AddWithIds(embeddings.ToArray(), ids);
    }

    public async Task<List<string>> GetTopChunksAsync(string query, int k = 3)
    {
        var queryEmbedding = await GetEmbeddingAsync(query);
        var (distances, indices) = _faissIndex.Search(new[] { queryEmbedding }, k);

        // indices is a 2D array; take the first row
        var top = indices.Length > 0 ? indices[0] : Array.Empty<long>();
        return top
            .Where(idx => idx >= 0 && idx < _chunks.Count)
            .Select(idx => _chunks[(int)idx])
            .ToList();
    }

    private async Task<float[]> GetEmbeddingAsync(string text)
    {
        var client = _httpFactory.CreateClient("OpenAI");
        var payload = JsonSerializer.Serialize(new { input = new[] { text }, model = "text-embedding-ada-002" });
        using var resp = await client.PostAsync("/v1/embeddings", new StringContent(payload, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
        var data = doc.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(e => e.GetSingle())
            .ToArray();

        return data;
    }
}