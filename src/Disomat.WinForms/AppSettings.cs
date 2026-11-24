using Disomat.Core;
using Disomat.Infrastructure;
namespace Disomat.WinForms;
internal sealed record AppSettings(TerminalSettings Terminal, string? Connection, string Directory, int Samples, int RawSamples, int DelayMs, int Scale)
{
    public static AppSettings Load()
    {
        string Get(string key, string fallback) => Environment.GetEnvironmentVariable(key) ?? fallback;
        int Number(string key, int fallback, int min, int max)
        {
            var value = Get(key, fallback.ToString());
            if (!int.TryParse(value, out int n) || n < min || n > max) throw new FormatException($"Invalid {key}");
            return n;
        }
        return new(new TerminalSettings(Get("DISOMAT_HOST","127.0.0.1"), Number("DISOMAT_PORT",1502,1,65535),
            Number("DISOMAT_TIMEOUT_MS",5000,1,120000), Get("DISOMAT_REQUEST_HEX",DisomatProtocol.LegacyRequestHex),
            Convert.ToByte(Get("DISOMAT_DELIMITER_HEX","23"),16), Number("DISOMAT_WEIGHT_FIELD",4,1,100),
            4096, Get("DISOMAT_RESPONSE_TERMINATOR_HEX","454E44")),
            Environment.GetEnvironmentVariable("DISOMAT_ODBC_CONNECTION"), Get("DISOMAT_TRIGGER_DIR",@"C:\vaga"),
            Number("DISOMAT_SAMPLES",10,1,1000), Number("DISOMAT_RAW_SAMPLES",7,1,1000),
            Number("DISOMAT_DELAY_MS",0,0,60000), Number("DISOMAT_SCALE",1,1,int.MaxValue));
    }
}
