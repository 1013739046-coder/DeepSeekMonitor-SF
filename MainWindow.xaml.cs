using System.Windows;
using System.Windows.Input;

namespace DeepSeekMonitor;

public partial class MainWindow : Window
{
    private readonly DashboardViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new DashboardViewModel();
        DataContext = _vm;
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Top + 20;

        var savedKey = ApiKeyStorage.Load();
        if (!string.IsNullOrEmpty(savedKey)) { _vm.Service.SetApiKey(savedKey); _vm.HasApiKey = true; }

        if (!_vm.HasApiKey) { var s = new SettingsWindow(_vm) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }; s.ShowDialog(); }

        if (_vm.HasApiKey) { LocalCache.Clear(); _vm.ResetAllData(); await _vm.RefreshAsync(); if (AppSettings.Current.AutoImportOnStart) { var exp = new AutoExportWindow(_vm) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }; exp.ShowDialog(); } }
        else _vm.StatusText = "请点击设置按钮配置 API Key";

        _vm.StartAutoRefresh();
    }

    private void OnClosing(object? s, System.ComponentModel.CancelEventArgs e) => _vm.StopAutoRefresh();
    private void Window_MouseDown(object s, MouseButtonEventArgs e) { if (e.ClickCount == 1) DragMove(); }
    private async void Refresh_Click(object s, RoutedEventArgs e) => await _vm.RefreshAsync();
    private void Settings_Click(object s, RoutedEventArgs e) { var w = new SettingsWindow(_vm) { Owner = this, WindowStartupLocation = WindowStartupLocation.CenterOwner }; w.ShowDialog(); }
    private void Close_Click(object s, RoutedEventArgs e) => Application.Current.Shutdown();
    private void Topmost_Click(object s, MouseButtonEventArgs e) { Topmost = !Topmost; _vm.StatusText = Topmost ? "已置顶" : "已取消置顶"; _vm.LastUpdated = DateTime.Now.ToString("HH:mm:ss"); }
}
