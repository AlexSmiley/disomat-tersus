using Disomat.Core;
using Disomat.Infrastructure;

namespace Disomat.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var host = Environment.GetEnvironmentVariable("DISOMAT_HOST") ?? "127.0.0.1";
        var port = int.TryParse(Environment.GetEnvironmentVariable("DISOMAT_PORT"), out var p) ? p : 1502;
        var terminal = new TcpWeightTerminal(new TerminalSettings(host, port));
        var connection = Environment.GetEnvironmentVariable("DISOMAT_ODBC_CONNECTION");
        IWeightRepository repository = string.IsNullOrWhiteSpace(connection)
            ? new UnconfiguredRepository() : new OdbcWeightRepository(connection);
        Application.Run(new MainForm(new WeighingService(terminal, repository)));
    }

    private sealed class UnconfiguredRepository : IWeightRepository
    {
        public Task<bool> SaveAsync(int scaleNumber, decimal tonnes, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Configure DISOMAT_ODBC_CONNECTION before automatic database updates.");
    }
}
