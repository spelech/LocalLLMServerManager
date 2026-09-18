using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.Interfaces;

public interface ISettingsService
{
    string SettingsFilePath();
    AppSettings LoadSettings();
    void SaveSettings(AppSettings settings);
}
