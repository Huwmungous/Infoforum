using Generator.WebAPI; // Needed for PromptBuilder and LlmClient
using Ingestor.Core;     // Needed for CodeIngestor
using Shared.Models;     // Needed if passing CodeChunk types explicitly
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Generator.WebAPI;

internal class Program
{
    static async Task Main()
    {
        Console.WriteLine("Web API Generator");
        var chunks = CodeIngestor.LoadChunksFromJson("ingested.json");

        Console.Write("Enter request (e.g. Convert OrderManager to API): ");
        var request = Console.ReadLine() ?? "";

        var filtered = chunks.Where(c => c.UnitName.Contains("OrderManager", StringComparison.OrdinalIgnoreCase)).ToList();
        var prompt = PromptBuilder.BuildPrompt(filtered, request);

        var client = new LlmClient();
        var result = await client.CallLlmAsync(prompt);

        File.WriteAllText("generated.cs", result);
        Console.WriteLine("Done. Output written to generated.cs");
    }
}
