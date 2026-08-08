using System.IO;
using System.Text.Json;

namespace LocalLLMServerManager.Services;

public class SettingsService : ISettingsService
{
    private static readonly object SettingsLock = new();

    public string SettingsFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "settings.json");
    }

    public AppSettings LoadSettings()
    {
        lock (SettingsLock)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    var path = SettingsFilePath();
                    if (File.Exists(path))
                    {
                        var json = File.ReadAllText(path);
                        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                    }
                    break;
                }
                catch
                {
                    Thread.Sleep(50);
                }
            }
            return new AppSettings();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        lock (SettingsLock)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    var path = SettingsFilePath();
                    var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(path, json);
                    break;
                }
                catch
                {
                    Thread.Sleep(50);
                }
            }
        }
    }
}
