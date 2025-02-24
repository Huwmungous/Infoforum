namespace IFOllama.Controllers
{
    public class OllamaResponse
    {
        public required string Model { get; set; }
        public required DateTime CreatedAt { get; set; }
        public required string Response { get; set; }
        public required bool Done { get; set; }
        public required string DoneReason { get; set; }
        public required List<int> Context { get; set; }
        public required long TotalDuration { get; set; }
        public required long LoadDuration { get; set; }
        public required int PromptEvalCount { get; set; }
        public required long PromptEvalDuration { get; set; }
        public required int EvalCount { get; set; }
        public required long EvalDuration { get; set; }
    }
}
