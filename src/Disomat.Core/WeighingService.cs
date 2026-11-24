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
        if (count is < 1 or > 1000) throw new ArgumentOutOfRangeException(nameof(count));
        if (pause < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pause));
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var readings = new List<decimal>(count);
            // All samples use the SAME TCP connection, as in the historical applications.
            await using var session = await terminal.OpenSessionAsync(cancellationToken);
            for (int i = 0; i < count; i++)
            {
                readings.Add(await session.ReadWeightAsync(cancellationToken));
                if (i + 1 < count && pause > TimeSpan.Zero)
                    await Task.Delay(pause, cancellationToken);
            }
            return WeightCalculator.Calculate(readings, mode);
        }
        finally { _gate.Release(); }
    }

    public async Task<MeasurementResult> MeasureAndSaveAsync(int count, TimeSpan pause,
        int scaleNumber, CancellationToken cancellationToken)
    {
        var result = await MeasureAsync(count, pause, cancellationToken);
        if (!await repository.SaveAsync(scaleNumber, result.Tonnes, cancellationToken))
            throw new IOException("Database row was not updated; leave the trigger pending.");
        return result;
    }
}
