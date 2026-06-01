using System.Windows;

namespace DeepSeekMonitor;

public partial class AutoExportWindow : Window
{
    private readonly ExportAutomationService _service = new();
    private readonly DashboardViewModel _vm;
    private readonly Window? _settingsWindow;

    public AutoExportWindow(DashboardViewModel vm, Window? settingsWindow = null)
    {
        InitializeComponent();
        _vm = vm;
        _settingsWindow = settingsWindow;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object s, RoutedEventArgs e) { _service.SetWebView(Browser); await _service.StartAsync(OnLog, OnComplete); }
    private void OnLog(string msg) => Dispatcher.Invoke(() => { StatusText.Text = msg; DetailText.Text = (DetailText.Text + "\n" + msg).Trim(); if (DetailText.Text.Length > 600) DetailText.Text = DetailText.Text[^500..]; });
    private void OnComplete(List<UsageRecord> records) => Dispatcher.Invoke(() => { if (records.Count > 0) { _vm.ImportAutoExport(records); StatusText.Text = $"导入成功！{records.Count} 条，即将关闭..."; Task.Delay(1500).ContinueWith(_ => Dispatcher.Invoke(() => { _settingsWindow?.Close(); Close(); })); } else StatusText.Text = "未获取到数据"; });
    private async void Retry_Click(object s, RoutedEventArgs e) { DetailText.Text = ""; await _service.StartAsync(OnLog, OnComplete); }
    private void Close_Click(object s, RoutedEventArgs e) => Close();
    private void OnClosing(object? s, System.ComponentModel.CancelEventArgs e) { _service.Cleanup(); Browser.Dispose(); }
}
