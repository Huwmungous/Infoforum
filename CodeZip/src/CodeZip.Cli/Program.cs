using CodeZip.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();

app.Configure(config =>
{
    config.SetApplicationName("codeship");
    config.SetApplicationVersion("1.0.0");

    config.AddCommand<ZipCommand>("zip")
        .WithDescription("Create a source-only zip of a project directory")
        .WithExample("zip", "C:\\Projects\\MyApp")
        .WithExample("zip", "/home/user/projects/myapp", "--dry-run");

    config.AddCommand<PruneCommand>("prune")
        .WithDescription("Prune old zip files from the output directory");

    config.AddCommand<ConfigCommand>("config")
        .WithDescription("View or modify configuration");
});

return app.Run(args);
