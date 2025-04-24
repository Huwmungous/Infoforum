namespace IFOllama.Controllers
{

    public class ImageGenerationRequest
    {
        public required string Model { get; set; }
        public required string Prompt { get; set; }
        public required string ConversationId { get; set; }
    }
}
