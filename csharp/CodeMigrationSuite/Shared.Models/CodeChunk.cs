
using System;
using System.Collections.Generic;

namespace Shared.Models;

public class CodeChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Language { get; set; } = "";
    public string UnitName { get; set; } = "";
    public string ChunkType { get; set; } = "";
    public string Content { get; set; } = "";
    public string FilePath { get; set; } = "";
    public Dictionary<string, string> Metadata { get; set; } = [];
}
