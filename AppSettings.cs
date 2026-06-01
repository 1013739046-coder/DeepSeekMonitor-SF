using System.IO;
using System.Text.Json;

namespace DeepSeekMonitor;

public class AppSettingsData
{
    public double NumberFontSize { get; set; } = 20;
    public double LabelFontSize { get; set; } = 12;
    public bool AutoImportOnStart { get; set; }
    public bool AutoStartWithWindows { get; set; }
}

public static class AppSettings
{
    private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeepSeekMonitor", "settings.json");
    private static AppSettingsData? _current;
    public static AppSettingsData Current => _current ??= Load();

    private static AppSettingsData Load()
    {
        if (!File.Exists(FilePath)) return new AppSettingsData();
        try { return JsonSerializer.Deserialize<AppSettingsData>(File.ReadAllText(FilePath)) ?? new(); }
        catch { return new AppSettingsData(); }
    }

    public static void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(Current));
    }
}
