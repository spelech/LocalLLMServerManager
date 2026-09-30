using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LocalLLMServerManager.Services;

public class SettingsService : ISettingsService
{
    private static readonly SemaphoreSlim SettingsSemaphore = new(1, 1);
    private static AppSettings? _cachedSettings;

    public string SettingsFilePath()
    {
        var appDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LocalLLMServerManager");
        var appDataPath = Path.Combine(appDataDir, "settings.json");
        var baseDirPath = Path.Combine(AppContext.BaseDirectory, "settings.json");

        if (File.Exists(appDataPath))
        {
            return appDataPath;
        }

        bool baseWritable = false;
        try
        {
            if (File.Exists(baseDirPath))
            {
                using var fs = File.Open(baseDirPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                baseWritable = true;
            }
            else
            {
                var testFile = Path.Combine(AppContext.BaseDirectory, $".write_test_{Guid.NewGuid():N}");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
                baseWritable = true;
            }
        }
        catch
        {
            baseWritable = false;
        }

        if (baseWritable)
        {
            return baseDirPath;
        }

        try
        {
            if (!Directory.Exists(appDataDir))
            {
                Directory.CreateDirectory(appDataDir);
            }
            if (File.Exists(baseDirPath) && !File.Exists(appDataPath))
            {
                File.Copy(baseDirPath, appDataPath);
            }
        }
        catch { }

        return appDataPath;
    }

    public AppSettings LoadSettings()
    {
        SettingsSemaphore.Wait();
        try
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
                    if (i < 4)
                    {
                        Thread.Sleep(50);
                    }
                }
            }

            _cachedSettings = new AppSettings();
            return _cachedSettings;
        }
        finally
        {
            SettingsSemaphore.Release();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        SettingsSemaphore.Wait();
        try
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
                    if (i < 4)
                    {
                        Thread.Sleep(50);
                    }
                }
            }
        }
        finally
        {
            SettingsSemaphore.Release();
        }
    }

    public async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        await SettingsSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
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
                        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                        _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                        return _cachedSettings;
                    }
                    break;
                }
                catch when (i < 4)
                {
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }
            }

            _cachedSettings = new AppSettings();
            return _cachedSettings;
        }
        finally
        {
            SettingsSemaphore.Release();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await SettingsSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    var path = SettingsFilePath();
                    var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);

                    // Update cache after successful save
                    _cachedSettings = settings;
                    break;
                }
                catch when (i < 4)
                {
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            SettingsSemaphore.Release();
        }
    }
}
