using System.IO;
using System.Text.Json;

namespace DeepSeekMonitor;

public static class LocalCache
{
    private static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeepSeekMonitor");
    private static readonly string FilePath = Path.Combine(Dir, "cache.json");

    public static void Save(DashboardCache cache)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(cache));
    }

    public static DashboardCache? Load()
    {
        if (!File.Exists(FilePath)) return null;
        try { return JsonSerializer.Deserialize<DashboardCache>(File.ReadAllText(FilePath)); }
        catch { return null; }
    }

    public static void Clear() { if (File.Exists(FilePath)) File.Delete(FilePath); }
}
