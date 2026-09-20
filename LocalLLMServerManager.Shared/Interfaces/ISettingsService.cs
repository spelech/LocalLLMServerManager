using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.Interfaces;

public interface ISettingsService
{
    string SettingsFilePath();
    AppSettings LoadSettings();
    void SaveSettings(AppSettings settings);
    Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(LoadSettings());
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        SaveSettings(settings);
        return Task.CompletedTask;
    }
}
