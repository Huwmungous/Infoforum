using Ingestor.Core;
using System;
using System.IO;

namespace Ingestor.CLI;

internal class Program
{
    static void Main()
    {
        Console.WriteLine("CodeIngestor CLI - Ingesting Codebase");
        Console.Write("Enter path to source folder: ");
        string? path = Console.ReadLine();

        if(string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            Console.WriteLine("Invalid path.");
            return;
        }

        Console.WriteLine("Ingesting...");
        var chunks = CodeIngestor.IngestDirectory(path);
        CodeIngestor.SaveChunksToJson(chunks, "ingested.json");
        Console.WriteLine($"Done. {chunks.Count} files saved to ingested.json");
    }
}
