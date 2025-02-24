namespace IFOllama.Controllers
{
    public class OllamaRequest
    {
        public required string Model { get; set; }
        public required string Prompt { get; set; }
    }
}
