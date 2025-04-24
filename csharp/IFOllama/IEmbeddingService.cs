// IEmbeddingService.cs
namespace IFOllama
{
    public interface IEmbeddingService
    {
        Task<float[]> EmbedAsync(string text);
    }
}