// RagIndexer.cs (Console App)
using System.Text.Json;
using System.Text.RegularExpressions;

class RagIndexer
{
    static readonly List<Uri> Uris = new()
    {
        new("https://context7.com/angular/angular"),
        new("https://context7.com/angular/components"),
        new("https://context7.com/dotnet/csharplang"),
        new("https://context7.com/postgres/postgres"),
        new("https://context7.com/microsoft/typescript")
    };

    const int ChunkSize = 2000; // ≈500 tokens

    static async Task Main()
    {
        var http = new HttpClient();
        var chunks = new List<string>();

        foreach (var uri in Uris)
        {
            var html = await http.GetStringAsync(uri);
            // crude HTML→text
            var text = Regex.Replace(html, "<[^>]+>", " ");
            text = Regex.Replace(text, @"\s+", " ").Trim();

            for (int i = 0; i < text.Length; i += ChunkSize)
            {
                var len = Math.Min(ChunkSize, text.Length - i);
                chunks.Add(text.Substring(i, len));
            }
        }

        // Only persist the raw chunks; we'll embed & index at runtime
        File.WriteAllText("chunks.json", JsonSerializer.Serialize(chunks));
        Console.WriteLine($"Wrote {chunks.Count} chunks to chunks.json");
    }
}
