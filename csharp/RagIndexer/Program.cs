// RagIndexer.cs (Console App) 
using System.Text.Json;
using System.Text.RegularExpressions;

partial class RagIndexer
{
    static readonly List<Uri> Uris =
    [
       new("https://context7.com/angular/angular"),
       new("https://context7.com/angular/components"),
       new("https://context7.com/postgres/postgres"),
       new("https://context7.com/dotnet/csharplang"),
       new("https://context7.com/microsoft/typescript")
    ];
    const int ChunkSize = 2000;

    const string ChunksFile = "Context7Chunks.json";

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();

    static async Task Main()
    {
        var http = new HttpClient();
        var chunks = new List<string>();
        foreach (var uri in Uris)
        {
            var html = await http.GetStringAsync(uri);
            var text = HtmlTagRegex().Replace(html, " ");
            text = WhitespaceRegex().Replace(text, " ").Trim();
            for (int i = 0; i < text.Length; i += ChunkSize)
                chunks.Add(text.Substring(i, Math.Min(ChunkSize, text.Length - i)));
        }
        File.WriteAllText(ChunksFile, JsonSerializer.Serialize(chunks));
        Console.WriteLine($"Saved {chunks.Count} chunks.");
    }
}
