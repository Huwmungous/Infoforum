using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using BraveSearchMcpServer.Protocol;
using BraveSearchMcpServer.Services;

var apiKey = Environment.GetEnvironmentVariable("BRAVE_API_KEY");
if (string.IsNullOrEmpty(apiKey))
{
    Console.Error.WriteLine("ERROR: BRAVE_API_KEY not set");
    return 1;
}

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddHttpClient<BraveSearchService>();
        services.AddSingleton<BraveSearchService>(sp =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient();
            var logger = sp.GetRequiredService<ILogger<BraveSearchService>>();
            return new BraveSearchService(httpClient, logger, apiKey);
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
return 0;