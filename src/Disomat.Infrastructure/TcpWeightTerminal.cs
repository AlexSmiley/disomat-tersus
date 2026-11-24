using System.Net.Sockets;
using Disomat.Core;

namespace Disomat.Infrastructure;

public sealed record TerminalSettings(string Host, int Port = 502, int TimeoutMs = 5000,
    string RequestHex = DisomatProtocol.LegacyRequestHex, byte Delimiter = 0x23,
    int WeightField = 4, int MaxResponseBytes = 4096);

public sealed class TcpWeightTerminal(TerminalSettings settings) : IWeightTerminal
{
    public async Task<IWeightSession> OpenSessionAsync(CancellationToken cancellationToken)
    {
        if (settings.TimeoutMs <= 0 || settings.MaxResponseBytes < 32 ||
            settings.WeightField < 1 || settings.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(settings));
        var client = new TcpClient();
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(settings.TimeoutMs);
            await client.ConnectAsync(settings.Host, settings.Port, timeout.Token);
            return new TcpSession(client, settings);
        }
        catch { client.Dispose(); throw; }
    }

    private sealed class TcpSession : IWeightSession
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly TerminalSettings _settings;
        private readonly byte[] _request;
        private readonly List<byte> _pending = new();

        public TcpSession(TcpClient client, TerminalSettings settings)
        {
            _client = client;
            _stream = client.GetStream();
            _settings = settings;
            _request = DisomatProtocol.DecodeHex(settings.RequestHex);
        }

        public async Task<decimal> ReadWeightAsync(CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_settings.TimeoutMs);
            await _stream.WriteAsync(_request, timeout.Token);

            while (true)
            {
                int terminator = FindFieldEnd();
                if (terminator >= 0)
                {
                    var fieldFrame = _pending.Take(terminator + 1).ToArray();
                    _pending.RemoveRange(0, terminator + 1);
                    // Extracted fields are understood, but real terminal trailer is not yet decoded.
                    return DisomatProtocol.ParseLegacyWeight(fieldFrame, _settings.Delimiter, _settings.WeightField);
                }
                if (_pending.Count >= _settings.MaxResponseBytes)
                    throw new InvalidDataException("Response exceeded maximum size before weight field.");
                var buffer = new byte[Math.Min(1024, _settings.MaxResponseBytes - _pending.Count)];
                int read = await _stream.ReadAsync(buffer, timeout.Token);
                if (read == 0) throw new EndOfStreamException("Terminal closed connection during response.");
                for (int i = 0; i < read; i++) _pending.Add(buffer[i]);
            }
        }

        private int FindFieldEnd()
        {
            int seen = 0;
            for (int i = 0; i < _pending.Count; i++)
                if (_pending[i] == _settings.Delimiter && ++seen == _settings.WeightField + 1)
                    return i;
            return -1;
        }

        public ValueTask DisposeAsync()
        {
            _stream.Dispose();
            _client.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
