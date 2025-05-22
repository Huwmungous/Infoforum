using ClipboardSyncService;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        // Add Windows Service support
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "ClipboardSyncService";
        });

        // Add logging
        builder.Services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddEventLog(eventLogSettings =>
            {
                eventLogSettings.SourceName = "ClipboardSyncService";
            });
        });

        // Add our services
        builder.Services.AddSingleton<ClipboardSyncManager>();
        builder.Services.AddHostedService<ClipboardSyncWindowsService>();

        var host = builder.Build();
        await host.RunAsync();
    }
}