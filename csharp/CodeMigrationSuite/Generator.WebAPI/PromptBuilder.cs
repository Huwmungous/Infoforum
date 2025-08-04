// ------------------- Generator.WebAPI/PromptBuilder.cs -------------------
using Shared.Models;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Generator.WebAPI;

public static class PromptBuilder
{
    public static string BuildPrompt(List<CodeChunk> relevantChunks, string request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("'");
        sb.AppendLine();

        foreach(var chunk in relevantChunks)
        {
            sb.AppendLine("// Source: " + Path.GetFileName(chunk.FilePath));
            sb.AppendLine(chunk.Content);
            sb.AppendLine();
        }

        sb.AppendLine("'");

        sb.AppendLine(request);

        return sb.ToString();
    }
}