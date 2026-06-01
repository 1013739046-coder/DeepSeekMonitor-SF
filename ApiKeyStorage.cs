using System.IO;
using System.Text.Json;

namespace DeepSeekMonitor;

public static class ApiKeyStorage
{
    private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeepSeekMonitor", "apikey.json");

    public static string? Load()
    {
        if (!File.Exists(FilePath)) return null;
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath))?.GetValueOrDefault("api_key"); }
        catch { return null; }
    }

    public static void Save(string key)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new Dictionary<string, string> { ["api_key"] = key }));
    }

    public static void Clear() { if (File.Exists(FilePath)) File.Delete(FilePath); }
}
