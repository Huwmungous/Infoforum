using System.Text.Json;
using System.Threading.Channels;

namespace FileTransferMcpServer;

public sealed class SseHub
{
    private readonly Channel<string> _ch = Channel.CreateUnbounded<string>();
    public ChannelReader<string> Reader => _ch.Reader;
    public Task PushAsync(string evt, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var chunk = $"event: {evt}\ndata: {json}\n\n";
        return _ch.Writer.WriteAsync(chunk).AsTask();
    }
}