using System.ComponentModel;
using CodeZip.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CodeZip.Cli.Commands;

public sealed class PruneSettings : CommandSettings
{
    [Description("Number of days to retain zip files")]
    [CommandOption("-d|--days")]
    public int? Days { get; init; }

    [Description("Show what would be pruned without deleting")]
    [CommandOption("--dry-run")]
    public bool DryRun { get; init; }
}

public sealed class PruneCommand : Command<PruneSettings>
{
    public override int Execute(CommandContext context, PruneSettings settings)
    {
        var config = CodeZipConfig.Load();
        var retentionDays = settings.Days ?? config.RetentionDays;

        AnsiConsole.MarkupLine($"[blue]Output directory:[/] {config.OutputDirectory}");
        AnsiConsole.MarkupLine($"[blue]Retention period:[/] {retentionDays} days");
        AnsiConsole.WriteLine();

        if (settings.DryRun)
        {
            var filesToPrune = ZipPruner.GetFilesToPrune(config.OutputDirectory, retentionDays);
            if (filesToPrune.Count == 0)
            {
                AnsiConsole.MarkupLine("[green]No files would be pruned.[/]");
                return 0;
            }

            AnsiConsole.MarkupLine($"[yellow]Files that would be pruned ({filesToPrune.Count}):[/]");
            foreach (var (filePath, createdAt) in filesToPrune)
            {
                var age = DateTime.Now - createdAt;
                AnsiConsole.MarkupLine($"  {Path.GetFileName(filePath)} [grey](created {createdAt:yyyy-MM-dd}, {age.Days}d old)[/]");
            }
            return 0;
        }

        var prunedCount = ZipPruner.PruneOldZips(config.OutputDirectory, retentionDays);

        if (prunedCount == 0)
            AnsiConsole.MarkupLine("[green]No expired zip files found.[/]");
        else
            AnsiConsole.MarkupLine($"[green]âœ“[/] Pruned {prunedCount} expired zip file(s)");

        return 0;
    }
}
