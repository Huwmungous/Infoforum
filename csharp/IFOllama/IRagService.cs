public interface IRagService
{
    /// <summary>
    /// Returns the top k most relevant context chunks for the given query.
    /// </summary>
    Task<List<string>> GetTopChunksAsync(string query, int k = 3);
}