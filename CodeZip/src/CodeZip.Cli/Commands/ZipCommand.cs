using CodeZip.Core;
using Spectre.Console;
using Spectre.Console.Cli;
using TextCopy;

namespace CodeZip.Cli.Commands;

public sealed class ZipCommand : Command<ZipSettings>
{
    public override int Execute(CommandContext context, ZipSettings settings)
    {
        var path = Path.GetFullPath(settings.Path);

        if (!Directory.Exists(path))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Directory not found: {path}");
            return 1;
        }

        var config = CodeZipConfig.Load();
        var zipper = new SourceZipper(config);

        if (settings.DryRun)
            return ExecuteDryRun(path, zipper, settings.Verbose);

        return ExecuteZip(path, zipper, config, settings);
    }

    private static int ExecuteDryRun(string path, SourceZipper zipper, bool verbose)
    {
        AnsiConsole.MarkupLine($"[blue]Dry run:[/] Analyzing {path}");
        AnsiConsole.WriteLine();

        var (included, excludedFiles, excludedDirs, types) = zipper.DryRun(path);

        var table = new Table();
        table.AddColumn("Metric");
        table.AddColumn("Value");
        table.AddRow("Project Types", ProjectDetector.GetDescription(types));
        table.AddRow("Files to Include", included.Count.ToString());
        table.AddRow("Files Excluded", excludedFiles.ToString());
        table.AddRow("Directories Excluded", excludedDirs.ToString());

        AnsiConsole.Write(table);

        if (verbose && included.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[blue]Files to include:[/]");
            foreach (var file in included.Take(100))
                AnsiConsole.MarkupLine($"  [grey]{file}[/]");
            if (included.Count > 100)
                AnsiConsole.MarkupLine($"  [grey]... and {included.Count - 100} more[/]");
        }

        return 0;
    }

    private static int ExecuteZip(string path, SourceZipper zipper, CodeZipConfig config, ZipSettings settings)
    {
        AnsiConsole.MarkupLine($"[blue]Creating source zip:[/] {path}");
        AnsiConsole.MarkupLine($"[grey]Output directory:[/] {config.OutputDirectory}");
        AnsiConsole.WriteLine();

        var result = AnsiConsole.Status().Start("Processing...", ctx =>
            zipper.CreateZip(path, settings.NoPrune, msg => ctx.Status(msg)));

        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {result.ErrorMessage}");
            return 1;
        }

        var table = new Table();
        table.AddColumn("Result");
        table.AddColumn("Value");

        if (result.PrunedFileCount > 0)
            table.AddRow("[yellow]Pruned[/]", $"{result.PrunedFileCount} expired zip(s)");

        table.AddRow("Project Types", ProjectDetector.GetDescription(result.DetectedTypes));
        table.AddRow("Files Included", result.FileCount.ToString());
        table.AddRow("Files Excluded", result.ExcludedFileCount.ToString());
        table.AddRow("Directories Excluded", result.ExcludedDirectoryCount.ToString());
        table.AddRow("Zip Size", FormatBytes(result.ZipSizeBytes));
        table.AddRow("[green]Created[/]", result.ZipFilePath ?? "");

        AnsiConsole.Write(table);

        if (!settings.NoClipboard && result.ZipFilePath != null)
        {
            try
            {
                ClipboardService.SetText(result.ZipFilePath);
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[green]âœ“[/] Path copied to clipboard");
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not copy to clipboard: {ex.Message}");
            }
        }

        return 0;
    }

    private static string FormatBytes(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB"];
        var order = 0;
        var size = (double)bytes;
        while (size >= 1024 && order < suffixes.Length - 1) { order++; size /= 1024; }
        return $"{size:0.##} {suffixes[order]}";
    }
}
