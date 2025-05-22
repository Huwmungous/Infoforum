// Windows service host
using ClipboardSyncService;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Linq;
using Grpc.Core;

namespace ClipboardSyncService
{
    public class ClipboardSyncWindowsService : BackgroundService
    {
        private readonly ILogger<ClipboardSyncWindowsService> logger;
        private readonly ClipboardSyncManager manager;
        private Server grpcServer;

        public ClipboardSyncWindowsService(ILogger<ClipboardSyncWindowsService> logger, ClipboardSyncManager manager)
        {
            this.logger = logger;
            this.manager = manager;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Start gRPC server
            grpcServer = new Server
            {
                Services = { ClipboardSyncService.BindService(new ClipboardSyncServiceImpl(logger as ILogger<ClipboardSyncServiceImpl>, manager)) },
                Ports = { new ServerPort("0.0.0.0", 50051, ServerCredentials.Insecure) }
            };

            grpcServer.Start();
            logger.LogInformation("gRPC server started on port 50051");

            // Start clipboard monitoring
            manager.Start();

            // Register with other nodes (discovery could be improved with mDNS/Bonjour)
            await RegisterWithOtherNodes();

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        private static async Task RegisterWithOtherNodes()
        {
            // Simple IP range scanning - in production, use proper service discovery
            var localIp = GetLocalIPAddress();
            var baseIp = localIp[..(localIp.LastIndexOf('.') + 1)];

            var tasks = new List<Task>();
            for (int i = 1; i <= 254; i++)
            {
                var ip = baseIp + i;
                if (ip != localIp)
                {
                    tasks.Add(TryRegisterWithNode(ip));
                }
            }

            await Task.WhenAll(tasks);
        }

        private static async Task TryRegisterWithNode(string ip)
        {
            try
            {
                using var channel = GrpcChannel.ForAddress($"http://{ip}:50051");
                var client = new ClipboardSyncService.ClipboardSyncServiceClient(channel);

                var nodeInfo = new NodeInfo
                {
                    HostName = Environment.MachineName,
                    IpAddress = GetLocalIPAddress(),
                    Port = 50051
                };

                await client.RegisterNodeAsync(nodeInfo);
            }
            catch
            {
                // Node not available, ignore
            }
        }

        private static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList.FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString() ?? "127.0.0.1";
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            manager.Stop();
            await grpcServer?.ShutdownAsync();
            await base.StopAsync(cancellationToken);
        }
    }

}