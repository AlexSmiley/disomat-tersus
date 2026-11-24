using Disomat.Core;
using Disomat.Infrastructure;
using Xunit;

namespace Disomat.UnitTests;

public class MeasurementTests
{
    [Fact]
    public void LegacyPacketHas42Bytes()
    {
        var packet = DisomatProtocol.DecodeHex(DisomatProtocol.LegacyRequestHex);
        Assert.Equal(42, packet.Length);
        Assert.Equal((byte)0xaa, packet[^2]);
        Assert.Equal((byte)0x84, packet[^1]);
    }

    [Theory]
    [InlineData("SIM#B#46#1#28456#END", 28456)]
    [InlineData("SIM#B#46#1#28,5#END", 28.5)]
    public void ExtractsWeightBetweenFourthAndFifthDelimiters(string frame, decimal expected) =>
        Assert.Equal(expected, DisomatProtocol.ParseLegacyWeight(
            System.Text.Encoding.ASCII.GetBytes(frame)));

    [Fact]
    public void RejectsMissingField()
    {
        Assert.Throws<FormatException>(() => DisomatProtocol.ParseLegacyWeight(
            System.Text.Encoding.ASCII.GetBytes("SIM#B#46#1#")));
    }

    [Fact]
    public void RoundedModeReproducesLegacyComputation()
    {
        var result = WeightCalculator.Calculate(new decimal[] { 28453, 28457, 28462 });
        Assert.Equal(28460, result.RoundedAverageKg);
        Assert.Equal(28.46m, result.Tonnes);
        Assert.Equal(new[] { 28460, 28460, 28460 }, result.Samples.Select(s => s.RoundedKg));
    }

    [Fact]
    public void RawModeRetainsUnroundedAverage()
    {
        var result = WeightCalculator.Calculate(new decimal[] { 1001, 1002 }, MeasurementMode.Raw);
        Assert.Equal(1001.5m, result.RawAverageKg);
        Assert.Equal(1.00m, result.Tonnes);
    }

    [Fact]
    public async Task SavesOnlyWhenRepositoryConfirms()
    {
        var service = new WeighingService(new DummyTerminal(), new FailedRepository());
        await Assert.ThrowsAsync<IOException>(() => service.MeasureAndSaveAsync(
            1, TimeSpan.Zero, 1, CancellationToken.None));
    }

    private sealed class DummyTerminal : IWeightTerminal
    {
        public Task<IWeightSession> OpenSessionAsync(CancellationToken ct) =>
            Task.FromResult<IWeightSession>(new DummySession());
    }

    private sealed class DummySession : IWeightSession
    {
        public Task<decimal> ReadWeightAsync(CancellationToken ct) => Task.FromResult(1000m);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FailedRepository : IWeightRepository
    {
        public Task<bool> SaveAsync(int scale, decimal tonnes, CancellationToken ct) =>
            Task.FromResult(false);
    }
}
