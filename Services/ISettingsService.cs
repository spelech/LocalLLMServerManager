namespace LocalLLMServerManager.Services;

public interface ISettingsService
{
    string SettingsFilePath();
    AppSettings LoadSettings();
    void SaveSettings(AppSettings settings);
}
