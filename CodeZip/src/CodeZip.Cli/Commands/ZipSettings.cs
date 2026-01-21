using System.ComponentModel;
using Spectre.Console.Cli;

namespace CodeZip.Cli.Commands;

public sealed class ZipSettings : CommandSettings
{
    [Description("Path to the project directory to zip")]
    [CommandArgument(0, "<path>")]
    public string Path { get; init; } = string.Empty;

    [Description("Skip automatic pruning of old zip files")]
    [CommandOption("--no-prune")]
    public bool NoPrune { get; init; }

    [Description("Preview what would be included without creating the zip")]
    [CommandOption("--dry-run")]
    public bool DryRun { get; init; }

    [Description("Skip copying the zip path to clipboard")]
    [CommandOption("--no-clipboard")]
    public bool NoClipboard { get; init; }

    [Description("Show verbose output including file list")]
    [CommandOption("-v|--verbose")]
    public bool Verbose { get; init; }
}
