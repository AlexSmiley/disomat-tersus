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
    private readonly TextBox _log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill };
    private readonly Label _raw = new() { AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold) };
    private readonly Label _rounded = new() { AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold) };
    private readonly ToolStripStatusLabel _connection = new("TCP: Idle");
    private readonly ToolStripStatusLabel _automatic = new("Auto: Stopped");
    private readonly Button _stopAuto = new() { Text = "Stop monitoring", AutoSize = true };
    private void Log(string message)
    {
        _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
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
        toolbar.Controls.AddRange(new Control[] { _mode, _count, _measure, _cancel });
        layout.Controls.Add(toolbar,0,0); layout.Controls.Add(_samples,0,1); layout.Controls.Add(_status,0,2);
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var manual = new TabPage("Manual Measurement");
        var auto = new TabPage("Automatic Processing");
        var config = new TabPage("Configuration");
        var manualSplit = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal };
        manualSplit.Panel1.Controls.Add(layout);
        var averages = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        _raw.Text = "Raw average: —"; _rounded.Text = "Rounded average: —";
        averages.Controls.AddRange(new Control[] { _raw, _rounded });
        manualSplit.Panel2.Controls.Add(averages);
        manual.Controls.Add(manualSplit);
        var autoPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(12) };
        autoPanel.Controls.AddRange(new Control[] { new Label { AutoSize = true, Text = $"Trigger directory: {settings.Directory}" },
            new Label { AutoSize = true, Text = $"Scale: {settings.Scale} | ODBC: {(string.IsNullOrWhiteSpace(settings.Connection) ? "Not configured" : "Configured")}" }, _auto, _stopAuto });
        auto.Controls.Add(autoPanel);
        var details = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            Text = string.Join(Environment.NewLine, new[] { $"Host: {settings.Terminal.Host}", $"Port: {settings.Terminal.Port}",
                $"Timeout: {settings.Terminal.TimeoutMs} ms", $"Rounded samples: {settings.Samples}", $"Raw samples: {settings.RawSamples}",
                $"Sampling delay: {settings.DelayMs} ms", $"Response terminator (hex): {settings.Terminal.ResponseTerminatorHex}",
                "Configuration is loaded from environment variables at startup." }) };
        config.Controls.Add(details);
        tabs.TabPages.AddRange(new[] { manual, auto, config });
        var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 72));
        shell.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
        shell.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var logGroup = new GroupBox { Text = "Activity log", Dock = DockStyle.Fill };
        logGroup.Controls.Add(_log);
        var statusBar = new StatusStrip();
        statusBar.Items.AddRange(new ToolStripItem[] { _connection, _automatic });
        shell.Controls.Add(tabs,0,0); shell.Controls.Add(logGroup,0,1); shell.Controls.Add(statusBar,0,2);
        Controls.Add(shell);
        _stopAuto.Click += (_,_) => _operation?.Cancel();
        Log("Ready.");
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
        _stopAuto.Enabled = !idle && _automatic.Text == "Auto: Monitoring";
    }
    private async Task RunAsync(bool automatic)
    {
        if (_operation != null) return;
        using var cts = new CancellationTokenSource();
        _operation = cts;
        _automatic.Text = automatic ? "Auto: Monitoring" : "Auto: Stopped";
        _connection.Text = automatic ? "TCP: On demand" : "TCP: Measuring";
        Log(automatic ? "Monitoring started." : "Measurement started."); UpdateControls();
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
                _raw.Text = $"Raw average: {result.RawAverageKg:0.##} kg";
                _rounded.Text = $"Rounded average: {result.RoundedAverageKg} kg / {result.Tonnes:0.00} t";
                Log($"Received {result.Samples.Count()} samples successfully.");
                foreach (var sample in result.Samples)
                    _samples.Items.Add(new ListViewItem(new[] { (++i).ToString(),sample.RawKg.ToString(),sample.WholeKg.ToString(),sample.RoundedKg.ToString() }));
                _status.Text = $"Raw average: {result.RawAverageKg:0.##} kg | Rounded average: {result.RoundedAverageKg} kg | {result.Tonnes:0.00} t";
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested) { _status.Text = "Stopped."; Log("Operation cancelled."); }
        catch (Exception ex) { _status.Text = $"Operation failed: {ex.Message}"; Log(_status.Text); }
        finally { _operation = null; _connection.Text = "TCP: Idle"; _automatic.Text = "Auto: Stopped"; UpdateControls(); if (_closing) Close(); }
    }
}
