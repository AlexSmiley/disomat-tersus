# DISOMAT Tersus — industrial TCP/IP weighing client

Refactored C#/.NET 10 application based on three legacy Windows Forms weighing clients. Includes a Windows desktop UI, a reusable core, raw TCP terminal connection, ODBC updates, file-triggered automatic measurement, and a **synthetic** test terminal.

## Important safety and validation notices

**Do not deploy against a production weighing instrument without validating the complete response format and device status with the actual manufacturer protocol documentation and captured responses.** The original client used a single TCP read and extracted the weight field between the fourth and fifth ASCII `#` delimiters. The new client improves partial-read handling but does **not yet validate full response boundaries, status bits or CRC**, because these details cannot be inferred from the supplied source. The synthetic simulator does not establish hardware compatibility. Do not use for certified/legal-for-trade measurements.

## Behavior retained across all three supplied programs

| Feature | Earliest | Manual rounded | Automatic DB | Refactor |
|---|---|---|---|---|
| Fixed vendor-specific binary command | Yes | Configurable | Configurable | Preserved |
| One TCP connection for multiple samples | Yes | Yes | Yes | Yes |
| Raw average (seven samples by default) | Yes | Optional view | No | Selectable |
| Round each sample to nearest 20 kg | No | Yes | Yes | Selectable |
| Manual measure button and sample list | Yes | Yes | No | Yes |
| `meri.dat` file trigger | No | No | Yes | Yes |
| ODBC `VAGI_MERENJA_FTP` update | No | No | Yes | Yes |
| Prevent trigger acknowledgment on DB failure | No | No | No | Yes |

## Running the simulator

Requires Python 3.10+:

```powershell
python simulator/server.py --host 127.0.0.1 --port 1502
```

Simulator accepts the original 42-byte request and returns a *synthetic, not hardware-verified* ASCII response with weight between the fourth and fifth `#` delimiters.

## Building the desktop app

Requires Windows and the .NET 10 SDK. From repository root:

```powershell
dotnet build src/Disomat.WinForms/Disomat.WinForms.csproj
dotnet run --project src/Disomat.WinForms/Disomat.WinForms.csproj
```

Default settings connect to `127.0.0.1:1502` so that the application will not accidentally issue commands to a real industrial scale.

For approved real hardware, configure environment variables:

```powershell
$env:DISOMAT_HOST = "192.168.240.1"
$env:DISOMAT_PORT = "502"
$env:DISOMAT_ODBC_CONNECTION = "<your private ODBC connection string>"
$env:DISOMAT_TRIGGER_DIR = "C:\vaga"
```

Never commit ODBC credentials or production network details. The historic connection string included credentials and must not be copied into source control; rotate them if still active.

## Manual mode

Choose **Rounded (10 samples)** or **Raw (original 7 samples)**, adjust sample count, and press **Измери / Measure**. The UI shows raw, integer and nearest-20-kg sample readings, raw average, rounded average and tonnes.

The later program computes integer rounded-weight average then rounds tonnes to two decimals. The earliest program computes a raw average without rounding to increments of 20 kg.

## Automatic mode

Configure `DISOMAT_ODBC_CONNECTION`, then press **Start automatic trigger monitoring**. The worker looks for `C:\vaga\meri.dat` (or custom directory) and reads ten measurements. It runs:

```sql
UPDATE VAGI_MERENJA_FTP
SET IZMERENO = ?, IND_SOSTOJBA = 2
WHERE BROJ_VAGA = ?
```

After **exactly one row** is updated, it renames `meri.dat` to `izmereno.dat`. If the update fails, the trigger remains pending. This still is **not transactional across the file system and database**: a crash between DB update and file rename can repeat the measurement. Production deployment should add idempotency, a durable event ID and reconciliation.

## Protocol limitations

- Port `502` alone does **not** make the 42-byte command standard Modbus TCP.
- The complete packet envelope, checksum, terminal status, response length and trailer semantics are unverified.
- There is a risk of interpreting buffered trailer bytes as a new response when issuing consecutive requests; this must be fixed using manufacturer-verified complete framing before hardware deployment.
- TCP timeouts, validation of extracted number and bound on read buffer are provided.
- Only non-negative readings within supported integer range are accepted.
- Measurement units are assumed to be kilograms as in the legacy application.
- The Windows Forms UI is intended for locally supervised usage, not remote industrial control.

## Test commands

```powershell
dotnet test tests/Disomat.UnitTests/Disomat.UnitTests.csproj
python -m unittest discover -s tests -p "test_*.py" -v
```

GitHub Actions builds the Windows app, runs xUnit tests and executes the simulator smoke test. Hardware integration tests remain pending.

## Source layout

```
src/Disomat.Core/             protocol decoding, calculations, orchestration
src/Disomat.Infrastructure/   TCP terminal, ODBC persistence, file triggers
src/Disomat.WinForms/         manual and automatic Windows desktop interface
tests/Disomat.UnitTests/      xUnit tests
simulator/                    synthetic Python TCP terminal
.github/workflows/           CI
```

## Next steps

Capture complete request/response hex dumps from a test terminal, validate real framing and CRC/status, make file-to-database processing idempotent, and add hardware-in-the-loop tests. No claims of verified hardware compatibility are made.

## Refactored desktop configuration and framing

The desktop app supports cancellation and mutually exclusive manual/automatic operations. Environment variables: `DISOMAT_SAMPLES` (10), `DISOMAT_RAW_SAMPLES` (7), `DISOMAT_DELAY_MS` (0), `DISOMAT_TIMEOUT_MS` (5000), `DISOMAT_SCALE` (1), `DISOMAT_REQUEST_HEX`, `DISOMAT_DELIMITER_HEX` (23), `DISOMAT_WEIGHT_FIELD` (4), and `DISOMAT_RESPONSE_TERMINATOR_HEX` (454E44, synthetic END marker). The TCP decoder now consumes through the configured terminator, including trailer bytes. The default is only for the simulator; verify actual hardware framing and status before deployment. A loopback integration test covers consecutive fragmented frames with trailer delimiters.
