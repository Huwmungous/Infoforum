
using Microsoft.Extensions.Logging;

namespace IFOllama.RAG
{
    public class RagService : IRagService
    {
        private readonly ILogger<RagService> _logger;
        private readonly IEmbeddingService _embedder;

        public RagService(IEmbeddingService embedder, ILogger<RagService> logger)
        {
            _embedder = embedder;
            _logger = logger;
        }

        public System.Threading.Tasks.Task<System.Collections.Generic.List<string>> GetTopChunksAsync(string input, int topK)
        {
            return System.Threading.Tasks.Task.FromResult(new System.Collections.Generic.List<string>());
        }

        public IEmbeddingService GetEmbeddingService()
        {
            return _embedder;
        }
    }
}
