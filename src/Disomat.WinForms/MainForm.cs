using Disomat.Core;
using Disomat.Infrastructure;
namespace Disomat.WinForms;

internal sealed class MainForm : Form
{
    private readonly WeighingService _service;
    private readonly AppSettings _settings;
    private readonly Button _measure = new() { Text = "Измери / Measure", AutoSize = true };
    private readonly Button _auto = new() { Text = "Start automatic monitoring", AutoSize = true };
    private readonly Button _cancel = new() { Text = "Cancel / Stop", AutoSize = true };
    private readonly ComboBox _mode = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly NumericUpDown _count = new() { Minimum = 1, Maximum = 1000, Width = 80 };
    private readonly Label _status = new() { Text = "Стиснете Измери", Dock = DockStyle.Fill, AutoSize = true };
    private readonly ListView _samples = new() { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
    private CancellationTokenSource? _operation;
    private bool _closing;
    public MainForm(WeighingService service, AppSettings settings)
    {
        _service = service; _settings = settings;
        Text = "DISOMAT Tersus — Weighing"; Width = 900; Height = 550;
        MinimumSize = new Size(650, 350);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        toolbar.Controls.AddRange(new Control[] { _mode, _count, _measure, _auto, _cancel });
        layout.Controls.Add(toolbar,0,0); layout.Controls.Add(_samples,0,1); layout.Controls.Add(_status,0,2);
        Controls.Add(layout);
        foreach (var title in new[] { "Sample", "Raw kg", "Whole kg", "Rounded kg" }) _samples.Columns.Add(title, 150);
        _mode.Items.AddRange(new object[] { "Rounded", "Raw" });
        _mode.SelectedIndex = 0; _count.Value = settings.Samples;
        _mode.SelectedIndexChanged += (_,_) => _count.Value = _mode.SelectedIndex == 1 ? settings.RawSamples : settings.Samples;
        _measure.Click += async (_,_) => await RunAsync(false);
        _auto.Click += async (_,_) => await RunAsync(true);
        _cancel.Click += (_,_) => _operation?.Cancel();
        FormClosing += (_,e) => { if (_operation != null) { e.Cancel = true; _closing = true; _operation.Cancel(); } };
        UpdateControls();
    }
    private void UpdateControls()
    {
        bool idle = _operation == null;
        _measure.Enabled = _mode.Enabled = _count.Enabled = idle;
        _auto.Enabled = idle && !string.IsNullOrWhiteSpace(_settings.Connection);
        _cancel.Enabled = !idle;
    }
    private async Task RunAsync(bool automatic)
    {
        if (_operation != null) return;
        using var cts = new CancellationTokenSource();
        _operation = cts; UpdateControls();
        try
        {
            var pause = TimeSpan.FromMilliseconds(_settings.DelayMs);
            if (automatic)
            {
                _status.Text = "Monitoring trigger directory…";
                var processor = new TriggerFileProcessor(_service, Path.Combine(_settings.Directory,"meri.dat"),
                    Path.Combine(_settings.Directory,"izmereno.dat"), _settings.Samples, pause, _settings.Scale);
                await Task.Run(() => processor.RunAsync(TimeSpan.FromMilliseconds(500),cts.Token));
            }
            else
            {
                _samples.Items.Clear(); _status.Text = "Measuring…";
                var result = await _service.MeasureAsync((int)_count.Value,pause,cts.Token,
                    _mode.SelectedIndex == 1 ? MeasurementMode.Raw : MeasurementMode.Rounded);
                int i = 0;
                foreach (var sample in result.Samples)
                    _samples.Items.Add(new ListViewItem(new[] { (++i).ToString(),sample.RawKg.ToString(),sample.WholeKg.ToString(),sample.RoundedKg.ToString() }));
                _status.Text = $"Raw average: {result.RawAverageKg:0.##} kg | Rounded average: {result.RoundedAverageKg} kg | {result.Tonnes:0.00} t";
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) { _status.Text = "Stopped."; }
        catch (Exception ex) { _status.Text = $"Operation failed: {ex.Message}"; }
        finally { _operation = null; UpdateControls(); if (_closing) Close(); }
    }
}
