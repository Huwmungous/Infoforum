namespace ClipboardSyncService
{
    // Main clipboard sync manager
    using Grpc.Core;
    using Grpc.Net.Client;
    using Microsoft.Extensions.Logging;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Threading;
    using System;
    using System.Linq;

    public class ClipboardSyncManager
    {
        private readonly List<NodeInfo> activeNodes = [];
        private readonly List<IServerStreamWriter<ClipboardData>> streamWriters = [];
        private readonly Lock lockObject = new();
        private readonly ILogger<ClipboardSyncManager> logger;
        private string lastClipboardHash = "";
        private ClipboardMonitor clipboardMonitor;

        public ClipboardSyncManager(ILogger<ClipboardSyncManager> logger)
        {
            this.logger = logger;
        }

        public void Start()
        {
            clipboardMonitor = new ClipboardMonitor(this);
            clipboardMonitor.Start();
        }

        public void Stop()
        {
            clipboardMonitor?.Stop();
        }

        public void RegisterNode(NodeInfo nodeInfo)
        {
            lock (lockObject)
            {
                activeNodes.RemoveAll(n => n.HostName == nodeInfo.HostName);
                activeNodes.Add(nodeInfo);
                logger.LogTrace("Registered node {IpAddress}:{Port} - {HostName}", nodeInfo.IpAddress, nodeInfo.Port, nodeInfo.HostName);
            }
        }

        public List<NodeInfo> GetActiveNodes()
        {
            lock (lockObject)
            {
                return [.. activeNodes];
            }
        }

        public async Task StreamClipboardUpdates(IServerStreamWriter<ClipboardData> responseStream, CancellationToken cancellationToken)
        {
            lock (lockObject)
            {
                streamWriters.Add(responseStream);
            }

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            finally
            {
                lock (lockObject)
                {
                    streamWriters.Remove(responseStream);
                }
            }
        }

        public void ApplyClipboardData(ClipboardData data)
        {
            if (data.SourceHost == Environment.MachineName)
                return;

            var formats = data.Formats.ToList();
            AdvancedClipboardManager.SetAllClipboardFormats(formats);
            lastClipboardHash = CalculateHash(formats);
        }

        public async void OnClipboardChanged()
        {
            var formats = AdvancedClipboardManager.GetAllClipboardFormats();
            var currentHash = CalculateHash(formats);

            if (currentHash != lastClipboardHash)
            {
                lastClipboardHash = currentHash;

                var clipboardData = new ClipboardData
                {
                    SourceHost = Environment.MachineName,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    MessageId = Guid.NewGuid().ToString(),
                    Formats = { formats }
                };

                await BroadcastClipboardData(clipboardData);
            }
        }

        private async Task BroadcastClipboardData(ClipboardData data)
        {
            var tasks = new List<Task>();

            // Send to streaming clients
            lock (lockObject)
            {
                foreach (var writer in streamWriters.ToList())
                {
                    tasks.Add(SafeWriteToStream(writer, data));
                }
            }

            // Send to registered nodes
            foreach (var node in GetActiveNodes())
            {
                if (node.HostName != Environment.MachineName)
                {
                    tasks.Add(SendToNode(node, data));
                }
            }

            await Task.WhenAll(tasks);
        }

        private async Task SafeWriteToStream(IServerStreamWriter<ClipboardData> writer, ClipboardData data)
        {
            try
            {
                await writer.WriteAsync(data);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to write to stream");
                lock (lockObject)
                {
                    streamWriters.Remove(writer);
                }
            }
        }

        private async Task SendToNode(NodeInfo node, ClipboardData data)
        {
            try
            {
                using var channel = GrpcChannel.ForAddress($"http://{node.IpAddress}:{node.Port}");
                var client = new ClipboardSyncService.ClipboardSyncServiceClient(channel);
                await client.SyncClipboardAsync(data);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send to node {NodeHost}", node.HostName);
            }
        }

        private static string CalculateHash(List<ClipboardFormat> formats)
        {
            var combined = string.Join("|", formats.Select(f => f.FormatName + f.Data.GetHashCode()));
            return combined.GetHashCode().ToString();
        }
    }
}
