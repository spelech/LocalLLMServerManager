using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Services;

namespace LocalLLMServerManager.Shared.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty] private string _forgeModelsPath = "";
    [ObservableProperty] private string _comfyUiUrl = "http://127.0.0.1:8188";
    [ObservableProperty] private string _threeDModelsPath = "";
    [ObservableProperty] private string _workflowsPath = "";
    [ObservableProperty] private string _preferredImageEngine = "comfy";
    [ObservableProperty] private string _comfyUiExecutablePath = "";
    [ObservableProperty] private string _forgeExecutablePath = "";
    [ObservableProperty] private string _lanAccessUrl = "http://127.0.0.1:5246";
    [ObservableProperty] private string _selectedThemeStyle = "semi";

    [RelayCommand]
    public void SwitchThemeStyle(string style)
    {
        if (string.IsNullOrWhiteSpace(style)) return;
        SelectedThemeStyle = style;
        try
        {
            var appType = Type.GetType("LocalLLMServerManager.App, LocalLLMServerManager");
            var method = appType?.GetMethod("SetThemeStyle", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            method?.Invoke(null, new object[] { style });
            ToastService.Instance.Show($"Switched theme to '{style.ToUpperInvariant()}' style.", ToastType.Info);
        }
        catch { }
    }

    public async Task LoadSettingsAsync(string apiBase, HttpClient http)
    {
        try
        {
            var response = await http.GetAsync($"{apiBase}/api/settings");
            if (response.IsSuccessStatusCode)
            {
                var jsonStr = await response.Content.ReadAsStringAsync();
                var settings = JsonSerializer.Deserialize<AppSettings>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (settings != null)
                {
                    ForgeModelsPath = settings.ForgeModelsPath;
                    ComfyUiUrl = settings.ComfyUiUrl;
                    ThreeDModelsPath = settings.ThreeDModelsPath;
                    WorkflowsPath = settings.WorkflowsPath;
                    PreferredImageEngine = settings.PreferredImageEngine;
                    ComfyUiExecutablePath = settings.ComfyUiExecutablePath;
                    ForgeExecutablePath = settings.ForgeExecutablePath;
                    LanAccessUrl = settings.LanAccessUrl;
                    SelectedThemeStyle = settings.SelectedThemeStyle;
                }
            }
        }
        catch { }
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        await SaveSettingsAsync("http://127.0.0.1:5246", new HttpClient());
    }

    public async Task SaveSettingsAsync(string apiBase, HttpClient http)
    {
        try
        {
            var settings = new AppSettings(
                ForgeModelsPath: this.ForgeModelsPath,
                ComfyUiUrl: this.ComfyUiUrl,
                ThreeDModelsPath: this.ThreeDModelsPath,
                WorkflowsPath: this.WorkflowsPath,
                PreferredImageEngine: this.PreferredImageEngine,
                ComfyUiExecutablePath: this.ComfyUiExecutablePath,
                ForgeExecutablePath: this.ForgeExecutablePath,
                LanAccessUrl: this.LanAccessUrl,
                SelectedThemeStyle: this.SelectedThemeStyle
            );

            var content = new StringContent(
                JsonSerializer.Serialize(settings),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            var response = await http.PostAsync($"{apiBase}/api/settings", content);
            if (response.IsSuccessStatusCode)
            {
                ToastService.Instance.Show("Settings saved successfully.", ToastType.Success);
            }
        }
        catch
        {
            ToastService.Instance.Show("Failed to save settings.", ToastType.Error);
        }
    }
}
