
using Microsoft.Extensions.Logging;

namespace IFOllama.RAG
{
    public class CodeContextService
    {
        private readonly ILogger<CodeContextService> _logger;

        public CodeContextService(
            ILogger<CodeContextService> logger,
            IEmbeddingService embedder,
            IConversationContextManager contextManager)
        {
            _logger = logger;
        }
    }
}
