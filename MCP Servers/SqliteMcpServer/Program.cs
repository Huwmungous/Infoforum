using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SqliteMcpServer.Protocol;
using SqliteMcpServer.Services;

var dbPath = Environment.GetEnvironmentVariable("SQLITE_DB_PATH") ?? "migration_metadata.db";

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<SqliteService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SqliteService>>();
            return new SqliteService(logger, dbPath);
        });
        services.AddSingleton<McpServer>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .Build();

var server = host.Services.GetRequiredService<McpServer>();
await server.RunAsync(CancellationToken.None);