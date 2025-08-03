using Xunit;
using IFOllama.RAG;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
using System.Linq;

namespace IFOllama.Tests
{
    public class ConversationContextManagerTests
    {
        [Fact]
        public void Initialize_CreatesDirectoryIfNotExists()
        {
            var logger = new NullLogger<ConversationContextManager>();
            var manager = new ConversationContextManager(logger);
            manager.Initialize();

            Assert.True(Directory.Exists(manager.ConversationFolder));
        }

        [Fact]
        public void AppendAndRetrieveMessage_WorksCorrectly()
        {
            var logger = new NullLogger<ConversationContextManager>();
            var manager = new ConversationContextManager(logger);
            manager.Initialize();

            var conversationId = "test-convo";
            manager.AppendMessage(conversationId, "user", "Hello");
            var messages = manager.GetConversation(conversationId);

            Assert.NotNull(messages);
            Assert.Contains(messages, m => m.Contains("user: Hello"));
        }
    }
}
