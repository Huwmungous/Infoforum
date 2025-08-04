using Shared.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Ingestor.Core;

public static class CodeIngestor
{
    private static readonly System.Text.Json.JsonSerializerOptions CachedJsonOptions = new() { WriteIndented = true };

    public static List<CodeChunk> IngestDirectory(string path)
    {
        var chunks = new List<CodeChunk>();
        var files = Directory.GetFiles(path, "*.pas", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(path, "*.dfm", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories));

        foreach (var file in files)
        {
            string content = File.ReadAllText(file);
            string language = file.EndsWith(".cs") ? "CSharp" : file.EndsWith(".dfm") ? "Delphi Form" : "Pascal File";
            string unitName = Path.GetFileNameWithoutExtension(file);

            chunks.Add(new CodeChunk
            {
                Language = language,
                UnitName = unitName,
                ChunkType = "File",
                Content = content,
                FilePath = file
            });
        }

        return chunks;
    }

    public static void SaveChunksToJson(List<CodeChunk> chunks, string outputPath)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(chunks, CachedJsonOptions);
        File.WriteAllText(outputPath, json);
    }

    public static List<CodeChunk> LoadChunksFromJson(string path)
    {
        var json = File.ReadAllText(path);
        return System.Text.Json.JsonSerializer.Deserialize<List<CodeChunk>>(json) ?? [];
    }
}