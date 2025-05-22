// gRPC service implementation
using ClipboardSyncService;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;

namespace ClipboardSyncService
{

    public class ClipboardSyncServiceImpl : ClipboardSyncServiceBase
    {
        private readonly ILogger<ClipboardSyncServiceImpl> logger;
        private readonly ClipboardSyncManager manager;

        public ClipboardSyncServiceImpl(ILogger<ClipboardSyncServiceImpl> logger, ClipboardSyncManager manager)
        {
            this.logger = logger;
            this.manager = manager;
        }

        public override Task<SyncResponse> SyncClipboard(ClipboardData request, ServerCallContext context)
        {
            try
            {
                manager.ApplyClipboardData(request);
                return Task.FromResult(new SyncResponse { Success = true });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to sync clipboard");
                return Task.FromResult(new SyncResponse { Success = false, Message = ex.Message });
            }
        }

        public override Task<RegistrationResponse> RegisterNode(NodeInfo request, ServerCallContext context)
        {
            manager.RegisterNode(request);
            return Task.FromResult(new RegistrationResponse
            {
                Success = true,
                ActiveNodes = { manager.GetActiveNodes() }
            });
        }

        public override Task<NodesResponse> GetActiveNodes(Empty request, ServerCallContext context)
        {
            return Task.FromResult(new NodesResponse
            {
                Nodes = { manager.GetActiveNodes() }
            });
        }

        public override async Task StreamClipboardUpdates(Empty request, IServerStreamWriter<ClipboardData> responseStream, ServerCallContext context)
        {
            await manager.StreamClipboardUpdates(responseStream, context.CancellationToken);
        }
    }
}