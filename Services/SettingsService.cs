using System.IO;
using System.Text.Json;

namespace LocalLLMServerManager.Services;

public class SettingsService : ISettingsService
{
    private static readonly object SettingsLock = new();
    private static AppSettings? _cachedSettings;

    public string SettingsFilePath()
    {
        return Path.Combine(AppContext.BaseDirectory, "settings.json");
    }

    public AppSettings LoadSettings()
    {
        lock (SettingsLock)
        {
            if (_cachedSettings != null)
            {
                return _cachedSettings;
            }

            for (int i = 0; i < 5; i++)
            {
                try
                {
                    var path = SettingsFilePath();
                    if (File.Exists(path))
                    {
                        var json = File.ReadAllText(path);
                        _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                        return _cachedSettings;
                    }
                    break;
                }
                catch
                {
                    Thread.Sleep(50);
                }
            }

            _cachedSettings = new AppSettings();
            return _cachedSettings;
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

                    // Update cache after successful save
                    _cachedSettings = settings;
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
