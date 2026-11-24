namespace Disomat.Core;

public interface IWeightSession : IAsyncDisposable
{
    Task<decimal> ReadWeightAsync(CancellationToken cancellationToken);
}
public interface IWeightTerminal
{
    Task<IWeightSession> OpenSessionAsync(CancellationToken cancellationToken);
}
public interface IWeightRepository
{
    Task<bool> SaveAsync(int scaleNumber, decimal tonnes, CancellationToken cancellationToken);
}

public sealed class WeighingService(IWeightTerminal terminal, IWeightRepository repository)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<MeasurementResult> MeasureAsync(int count, TimeSpan pause,
        CancellationToken cancellationToken, MeasurementMode mode = MeasurementMode.Rounded)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await CollectAsync(count, pause, cancellationToken, mode); }
        finally { _gate.Release(); }
    }

    private async Task<MeasurementResult> CollectAsync(int count, TimeSpan pause,
        CancellationToken cancellationToken, MeasurementMode mode)
    {
        if (count is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(count));
        if (pause < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pause));
        var readings = new List<decimal>(count);
        await using var session = await terminal.OpenSessionAsync(cancellationToken);
        for (int i = 0; i < count; i++)
        {
            readings.Add(await session.ReadWeightAsync(cancellationToken));
            if (i + 1 < count && pause > TimeSpan.Zero)
                await Task.Delay(pause, cancellationToken);
        }
        return WeightCalculator.Calculate(readings, mode);
    }

    public async Task<MeasurementResult> MeasureAndSaveAsync(int count, TimeSpan pause,
        int scaleNumber, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var result = await CollectAsync(count, pause, cancellationToken, MeasurementMode.Rounded);
            if (!await repository.SaveAsync(scaleNumber, result.Tonnes, cancellationToken))
                throw new IOException("Database row was not updated; leave the trigger pending.");
            return result;
        }
        finally { _gate.Release(); }
    }
}
