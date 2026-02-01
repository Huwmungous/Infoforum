using ChitterChatterWebService.Hubs;
using ChitterChatterWebService.Services;
using IFGlobal.WebServices; 

// ═══════════════════════════════════════════════════════════════════════════════
// ChitterChatter Web Service
// SignalR-based VOIP communication hub
// Port: 5003
// ═══════════════════════════════════════════════════════════════════════════════

var app = await ServiceFactory.CreateSimpleAsync(
    serviceName: "ChitterChatter",
    useIFLogger: true,
    configureServices: (services, context) =>
    {
        // Register state management service as singleton
        services.AddSingleton<ChatterStateService>();

        // Configure SignalR with MessagePack for efficient binary serialisation
        services.AddSignalR(options =>
        {
#if DEBUG
            options.EnableDetailedErrors = true;
#endif
            options.MaximumReceiveMessageSize = 64 * 1024; // 64KB for audio packets
            options.StreamBufferCapacity = 20;
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        })
        .AddMessagePackProtocol();

        // Add CORS for desktop client
        services.AddCors(options =>
        {
            options.AddPolicy("ChitterChatterPolicy", builder =>
            {
                builder
                    .AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });
    },
    configurePipeline: (app, context) =>
    {
        app.UseCors("ChitterChatterPolicy");

        // Map the SignalR hub
        app.MapHub<ChatterHub>("/chatter", options =>
        {
            options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
        });

        // Health check endpoint
        app.MapGet("/health", () => Results.Ok(new
        {
            Status = "Healthy",
            Service = "ChitterChatter",
            Timestamp = DateTimeOffset.UtcNow
        }));

        // Status endpoint showing connected users count
        app.MapGet("/status", (ChatterStateService stateService) =>
        {
            var users = stateService.GetAllUsers();
            var rooms = stateService.GetAllRooms();

            return Results.Ok(new
            {
                Service = "ChitterChatter",
                ConnectedUsers = users.Count,
                ActiveRooms = rooms.Count(r => r.ParticipantUserIds.Count > 0),
                TotalRooms = rooms.Count,
                Timestamp = DateTimeOffset.UtcNow
            });
        });
    }
);

await app.RunAsync();
