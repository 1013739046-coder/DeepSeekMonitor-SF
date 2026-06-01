using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace DeepSeekMonitor;

public partial class SettingsWindow : Window
{
    private readonly DashboardViewModel _vm;
    private string _realKey = "";
    private bool _isMasked = true;
    private bool _settingText;

    public SettingsWindow(DashboardViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = _vm;
        _realKey = ApiKeyStorage.Load() ?? "";
        if (!string.IsNullOrEmpty(_realKey)) { _settingText = true; ApiKeyBox.Text = MaskKey(_realKey); _settingText = false; ApiPlaceholder.Visibility = Visibility.Collapsed; ApiStatusLabel.Text = "已加载保存的 API Key"; }
        AutoImportCheck.IsChecked = AppSettings.Current.AutoImportOnStart;
        AutoStartCheck.IsChecked = AppSettings.Current.AutoStartWithWindows;
    }

    private static string MaskKey(string key) { if (key.Length <= 8) return new string('•', key.Length); return key[..4] + new string('•', Math.Min(key.Length - 8, 20)) + key[^4..]; }

    private void Eye_Click(object s, RoutedEventArgs e) { _isMasked = !_isMasked; _settingText = true; ApiKeyBox.Text = _isMasked ? MaskKey(_realKey) : _realKey; _settingText = false; }
    private void ApiKeyBox_TextChanged(object s, System.Windows.Controls.TextChangedEventArgs e) { if (_settingText) return; _realKey = ApiKeyBox.Text; ApiPlaceholder.Visibility = string.IsNullOrEmpty(_realKey) ? Visibility.Visible : Visibility.Collapsed; }

    private async void Verify_Click(object s, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_realKey)) { ApiStatusLabel.Text = "请输入 API Key"; ApiStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xf7, 0x76, 0x8e)); return; }
        VerifyButton.IsEnabled = false; ApiStatusLabel.Text = "验证中...";
        try
        {
            var svc = new DeepSeekService(); svc.SetApiKey(_realKey); var b = await svc.FetchBalanceAsync();
            if (b.BalanceInfos.Count > 0) { ApiStatusLabel.Text = $"验证成功！余额: {b.BalanceInfos[0].TotalBalance}"; ApiStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x9d, 0x87)); }
            else { ApiStatusLabel.Text = "API Key 有效但无余额"; ApiStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xe0, 0xaf, 0x68)); }
        }
        catch (Exception ex) { ApiStatusLabel.Text = $"验证失败: {ex.Message}"; ApiStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0xf7, 0x76, 0x8e)); }
        finally { VerifyButton.IsEnabled = true; }
    }

    private async void Save_Click(object s, RoutedEventArgs e) { if (string.IsNullOrEmpty(_realKey)) return; ApiKeyStorage.Save(_realKey); await _vm.SetApiKeyAsync(_realKey); Close(); }
    private void Clear_Click(object s, RoutedEventArgs e) { _realKey = ""; _isMasked = true; _settingText = true; ApiKeyBox.Text = ""; _settingText = false; ApiKeyStorage.Clear(); _vm.ClearApiKey(); ApiStatusLabel.Text = "API Key 已清除"; }
    private void AutoExport_Click(object s, RoutedEventArgs e) { var win = new AutoExportWindow(_vm, this) { Owner = this }; win.ShowDialog(); }
    private void AutoImportCheck_Changed(object s, RoutedEventArgs e) { if (AutoImportCheck.IsChecked.HasValue) { AppSettings.Current.AutoImportOnStart = AutoImportCheck.IsChecked.Value; AppSettings.Save(); } }
    private void AutoStartCheck_Changed(object s, RoutedEventArgs e) { if (AutoStartCheck.IsChecked.HasValue) { AppSettings.Current.AutoStartWithWindows = AutoStartCheck.IsChecked.Value; AppSettings.Save(); ToggleAutoStart(AutoStartCheck.IsChecked.Value); } }
    private static void ToggleAutoStart(bool enable) { var exePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "deepseek", "deepseek.exe"); var rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true); if (enable) rk?.SetValue("DeepSeekMonitor", $"\"{exePath}\""); else rk?.DeleteValue("DeepSeekMonitor", false); }
    private void Window_MouseDown(object s, MouseButtonEventArgs e) { if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    private void Author_Click(object s, MouseButtonEventArgs e) { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/1013739046-coder/DeepSeekMonitor-SF") { UseShellExecute = true }); }
    private void Close_Click(object s, RoutedEventArgs e) => Close();
}
