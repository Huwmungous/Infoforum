// IEmbeddingService.cs
namespace IFOllama.RAG
{
    public interface IEmbeddingService
    {
        Task<float[]> EmbedAsync(string text);
    }
}