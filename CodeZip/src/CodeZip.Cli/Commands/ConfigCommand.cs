using System.ComponentModel;
using CodeZip.Core;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CodeZip.Cli.Commands;

public sealed class ConfigSettings : CommandSettings
{
    [Description("Action: show, set, reset")]
    [CommandArgument(0, "[action]")]
    public string? Action { get; init; }

    [Description("Setting name (for set action)")]
    [CommandArgument(1, "[setting]")]
    public string? Setting { get; init; }

    [Description("Setting value (for set action)")]
    [CommandArgument(2, "[value]")]
    public string? Value { get; init; }
}

public sealed class ConfigCommand : Command<ConfigSettings>
{
    public override int Execute(CommandContext context, ConfigSettings settings)
    {
        var action = settings.Action?.ToLowerInvariant() ?? "show";

        return action switch
        {
            "show" => ShowConfig(),
            "set" => SetConfig(settings.Setting, settings.Value),
            "reset" => ResetConfig(),
            _ => ShowHelp()
        };
    }

    private static int ShowConfig()
    {
        var config = CodeZipConfig.Load();

        AnsiConsole.MarkupLine($"[blue]Configuration file:[/] {CodeZipConfig.ConfigFilePath}");
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Setting");
        table.AddColumn("Value");
        table.AddRow("outputDirectory", config.OutputDirectory);
        table.AddRow("retentionDays", config.RetentionDays.ToString());
        table.AddRow("pruneOnRun", config.PruneOnRun.ToString().ToLowerInvariant());

        AnsiConsole.Write(table);
        return 0;
    }

    private static int SetConfig(string? setting, string? value)
    {
        if (string.IsNullOrEmpty(setting) || string.IsNullOrEmpty(value))
        {
            AnsiConsole.MarkupLine("[red]Error:[/] Usage: codeship config set <setting> <value>");
            return 1;
        }

        var config = CodeZipConfig.Load();

        switch (setting.ToLowerInvariant())
        {
            case "outputdirectory":
                config.OutputDirectory = value;
                break;
            case "retentiondays":
                if (!int.TryParse(value, out var days) || days < 0)
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] Invalid retention days value");
                    return 1;
                }
                config.RetentionDays = days;
                break;
            case "pruneonrun":
                if (!bool.TryParse(value, out var prune))
                {
                    AnsiConsole.MarkupLine("[red]Error:[/] Invalid boolean value");
                    return 1;
                }
                config.PruneOnRun = prune;
                break;
            default:
                AnsiConsole.MarkupLine($"[red]Error:[/] Unknown setting: {setting}");
                return 1;
        }

        config.Save();
        AnsiConsole.MarkupLine($"[green]âœ“[/] Set {setting} = {value}");
        return 0;
    }

    private static int ResetConfig()
    {
        if (!AnsiConsole.Confirm("Reset configuration to defaults?", false))
            return 0;

        var config = new CodeZipConfig();
        config.Save();
        AnsiConsole.MarkupLine("[green]âœ“[/] Configuration reset to defaults");
        return 0;
    }

    private static int ShowHelp()
    {
        AnsiConsole.MarkupLine("[blue]Config commands:[/]");
        AnsiConsole.MarkupLine("  codeship config show          Show current configuration");
        AnsiConsole.MarkupLine("  codeship config set <k> <v>   Set a configuration value");
        AnsiConsole.MarkupLine("  codeship config reset         Reset to defaults");
        return 0;
    }
}
