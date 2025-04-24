// IEmbeddingService.cs
public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text);
}