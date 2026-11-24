using System.Net;
using System.Net.Sockets;
using System.Text;
using Disomat.Core;
using Disomat.Infrastructure;
using Xunit;

namespace Disomat.UnitTests;

public class TcpIntegrationTests
{
    [Fact]
    public async Task ReadsConsecutiveFragmentedFramesIncludingTrailer()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var ct = timeout.Token;
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var expected = DisomatProtocol.DecodeHex(DisomatProtocol.LegacyRequestHex);
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync(ct);
            using var stream = peer.GetStream();
            foreach (var weight in new[] { 12340, 12360 })
            {
                var request = new byte[expected.Length];
                await stream.ReadExactlyAsync(request, ct);
                Assert.Equal(expected, request);
                await stream.WriteAsync(Encoding.ASCII.GetBytes($"SIM#B#46#1#{weight}#TRAIL#E"), ct);
                await Task.Delay(20, ct);
                await stream.WriteAsync(Encoding.ASCII.GetBytes("ND"), ct);
            }
        }, ct);
        var terminal = new TcpWeightTerminal(new TerminalSettings("127.0.0.1", port));
        await using var session = await terminal.OpenSessionAsync(ct);
        Assert.Equal(12340m, await session.ReadWeightAsync(ct));
        Assert.Equal(12360m, await session.ReadWeightAsync(ct));
        await server;
    }
}
