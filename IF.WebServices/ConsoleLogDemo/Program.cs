using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SfD.Global.Logging;

Console.WriteLine("Simple Log Generator\n");

// Setup dependency injection
var services = new ServiceCollection();

// Add logging
services.AddLogging(builder =>
{
    builder.ClearProviders();
    builder.AddConsole();
    builder.AddSfdLogger("http://localhost:5310", LogLevel.Information);
});

var serviceProvider = services.BuildServiceProvider();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

// Generate log entries
logger.LogInformation("Starting application...");
await Task.Delay(500);

logger.LogInformation("User {Username} logged in from {IpAddress}", "john.doe", "192.168.1.100");
await Task.Delay(500);

logger.LogWarning("High CPU usage detected: {CpuPercent}%", 85);
await Task.Delay(500);

logger.LogInformation("Processing order {OrderId} for amount ${Amount}", "ORD-12345", 99.99);
await Task.Delay(500);

try
{
    throw new FileNotFoundException("Configuration file not found", "config.json");
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to load configuration");
}
await Task.Delay(500);

logger.LogCritical("Payment gateway is DOWN!");
await Task.Delay(500);

logger.LogInformation("Batch job completed. Processed {Count} records in {Duration}ms", 1523, 4567);
await Task.Delay(500);

logger.LogInformation("Application shutdown initiated");
await Task.Delay(2000); // Wait for background queue to process

Console.WriteLine("\n✓ All logs generated!");
Console.WriteLine("Check the LoggerWebService at: http://localhost:5310/api/logs");
Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();