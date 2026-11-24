using Disomat.Core;
using Disomat.Infrastructure;

namespace Disomat.WinForms;

public sealed class MainForm : Form
{
    private readonly WeighingService _service;
    private readonly Button _measure = new() { Text = "Измери / Measure", Dock = DockStyle.Top, Height = 42 };
    private readonly Button _auto = new() { Text = "Start automatic trigger monitoring", Dock = DockStyle.Top, Height = 38 };
    private readonly ComboBox _mode = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList, Height = 30 };
    private readonly NumericUpDown _count = new() { Dock = DockStyle.Top, Minimum = 1, Maximum = 1000, Value = 10 };
    private readonly Label _status = new() { Text = "Стиснете Измери", Dock = DockStyle.Top, AutoSize = true };
    private readonly ListView _samples = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
    private CancellationTokenSource? _monitor;
    private bool _busy;

    public MainForm(WeighingService service)
    {
        _service = service;
        Text = "DISOMAT Tersus — Weighing";
        Width = 760; Height = 540;
        _samples.Columns.Add("Sample", 85);
        _samples.Columns.Add("Raw kg", 155);
        _samples.Columns.Add("Whole kg", 155);
        _samples.Columns.Add("Rounded kg", 155);
        _mode.Items.AddRange(new object[] { "Rounded (10 samples)", "Raw (original 7 samples)" });
        _mode.SelectedIndex = 0;
        _mode.SelectedIndexChanged += (_, _) => _count.Value = _mode.SelectedIndex == 1 ? 7 : 10;
        Controls.Add(_samples); Controls.Add(_status); Controls.Add(_auto);
        Controls.Add(_measure); Controls.Add(_count); Controls.Add(_mode);
        _measure.Click += async (_, _) => await ManualMeasurementAsync();
        _auto.Click += (_, _) => ToggleMonitoring();
        FormClosing += (_, _) => { _monitor?.Cancel(); };
    }

    private async Task ManualMeasurementAsync()
    {
        if (_busy) return;
        _busy = true;
        _measure.Enabled = false;
        try
        {
            var mode = _mode.SelectedIndex == 1 ? MeasurementMode.Raw : MeasurementMode.Rounded;
            var result = await _service.MeasureAsync((int)_count.Value, TimeSpan.Zero, CancellationToken.None, mode);
            _samples.Items.Clear();
            foreach (var (sample, index) in result.Samples.Select((sample, index) => (sample, index)))
                _samples.Items.Add(new ListViewItem(new[]
                    { (index + 1).ToString(), sample.RawKg.ToString(), sample.WholeKg.ToString(), sample.RoundedKg.ToString() }));
            _status.Text = $"Mode: {result.Mode} | Raw average: {result.RawAverageKg:0.##} kg | Rounded average: {result.RoundedAverageKg} kg | {result.Tonnes:0.00} t";
        }
        catch (Exception ex) { _status.Text = ex.Message; }
        finally { _busy = false; _measure.Enabled = true; }
    }

    private void ToggleMonitoring()
    {
        if (_monitor != null)
        {
            _monitor.Cancel();
            _monitor = null;
            _auto.Text = "Start automatic trigger monitoring";
            return;
        }
        _monitor = new CancellationTokenSource();
        _auto.Text = "Stop automatic trigger monitoring";
        var directory = Environment.GetEnvironmentVariable("DISOMAT_TRIGGER_DIR") ?? @"C:\vaga";
        var processor = new TriggerFileProcessor(_service, Path.Combine(directory, "meri.dat"),
            Path.Combine(directory, "izmereno.dat"), 10, TimeSpan.Zero, 1);
        _ = MonitorAsync(processor, _monitor.Token);
    }

    private async Task MonitorAsync(TriggerFileProcessor processor, CancellationToken token)
    {
        try { await processor.RunAsync(TimeSpan.FromMilliseconds(500), token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { if (!IsDisposed) _status.Text = $"Automatic mode stopped: {ex.Message}"; }
        finally { if (!IsDisposed) _auto.Text = "Start automatic trigger monitoring"; }
    }
}
