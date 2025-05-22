using IFOllama.RAG;

public interface IRagService
{
    Task<List<string>> GetTopChunksAsync(string query, int k = 3);
    IEmbeddingService GetEmbeddingService();
}