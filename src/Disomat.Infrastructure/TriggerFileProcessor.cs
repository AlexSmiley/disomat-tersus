using Disomat.Core;

namespace Disomat.Infrastructure;

/// <summary>
/// Bounded polling detects existing and newly created trigger files.
/// Unacknowledged triggers are left intact when measurement/database update fails.
/// </summary>
public sealed class TriggerFileProcessor(WeighingService service, string pendingPath, string completePath,
    int samples, TimeSpan pause, int scaleNumber)
{
    public async Task RunAsync(TimeSpan pollInterval, CancellationToken cancellationToken)
    {
        if (pollInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pollInterval));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pendingPath))!);
        while (!cancellationToken.IsCancellationRequested)
        {
            if (File.Exists(pendingPath))
            {
                await service.MeasureAndSaveAsync(samples, pause, scaleNumber, cancellationToken);
                // If rename fails after update, repeating the trigger can duplicate the write.
                // Production use requires an idempotency scheme coordinated with the DB owner.
                File.Move(pendingPath, completePath, overwrite: true);
            }
            await Task.Delay(pollInterval, cancellationToken);
        }
    }
}
