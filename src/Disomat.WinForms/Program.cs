using Disomat.Core;
using Disomat.Infrastructure;
namespace Disomat.WinForms;
internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            var settings = AppSettings.Load();
            IWeightRepository repository = string.IsNullOrWhiteSpace(settings.Connection)
                ? new UnconfiguredRepository() : new OdbcWeightRepository(settings.Connection);
            Application.Run(new MainForm(new WeighingService(new TcpWeightTerminal(settings.Terminal), repository), settings));
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "DISOMAT startup error"); }
    }
    private sealed class UnconfiguredRepository : IWeightRepository
    {
        public Task<bool> SaveAsync(int scaleNumber, decimal tonnes, CancellationToken ct) =>
            throw new InvalidOperationException("ODBC is not configured.");
    }
}
