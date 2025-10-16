using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CodeFormatterMcpServer.Protocol;
using CodeFormatterMcpServer.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddSingleton<CodeFormatterService>();
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