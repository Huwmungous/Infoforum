namespace IFOllama.Controllers
{

    public class ImageGenerationRequest
    {
        public string Model { get; set; }
        public string Prompt { get; set; }
        public string ConversationId { get; set; }
    }
}
