using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Services;

namespace LocalLLMServerManager.Shared.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty] private string _forgeModelsPath = "";
    [ObservableProperty] private string _comfyModelsPath = "";
    [ObservableProperty] private string _comfyUiUrl = "http://127.0.0.1:8188";
    [ObservableProperty] private string _threeDModelsPath = "";
    [ObservableProperty] private string _workflowsPath = "";
    [ObservableProperty] private string _preferredImageEngine = "comfy";
    [ObservableProperty] private string _comfyUiExecutablePath = "";
    [ObservableProperty] private string _forgeExecutablePath = "";
    [ObservableProperty] private string _ollamaExecutablePath = "ollama";
    [ObservableProperty] private string _lanAccessUrl = "http://127.0.0.1:5246";
    [ObservableProperty] private string _selectedThemeStyle = "semi";
    [ObservableProperty] private string _serviceName = "LocalLLMServerManager";
    [ObservableProperty] private string _publishOutputPath = "C:\\LocalLLMServerManager";
    [ObservableProperty] private string _audioEngineExecutablePath = "";
    [ObservableProperty] private string _audioEngineUrl = "http://127.0.0.1:8880";
    [ObservableProperty] private string _preferredAudioVoice = "af_heart";

    [ObservableProperty] private IStorageProvider? _storageProvider;
    [ObservableProperty] private bool _isAutoDetecting;

    // Real-time status indicators
    [ObservableProperty] private string _comfyUiExecutableStatus = "⚠️ Missing";
    [ObservableProperty] private string _forgeExecutableStatus = "⚠️ Missing";
    [ObservableProperty] private string _ollamaStatus = "⚠️ Missing";
    [ObservableProperty] private string _forgeModelsStatus = "⚠️ Missing";
    [ObservableProperty] private string _comfyModelsStatus = "⚠️ Missing";
    [ObservableProperty] private string _threeDModelsStatus = "⚠️ Missing";
    [ObservableProperty] private string _workflowsStatus = "⚠️ Missing";
    [ObservableProperty] private string _audioEngineExecutableStatus = "⚠️ Missing";

    public string OllamaExecutableStatus => OllamaStatus;

    private readonly IThemeService _themeService;

    public IReadOnlyList<string> AvailableThemes { get; } = new[]
    {
        "Matte Carbon (Default)",
        "OLED Pure Black",
        "Clean Light"
    };

    [ObservableProperty] private string _selectedTheme = "Matte Carbon (Default)";

    public SettingsViewModel() : this(ThemeService.Instance)
    {
    }

    public SettingsViewModel(IThemeService themeService)
    {
        _themeService = themeService ?? ThemeService.Instance;
        _selectedTheme = MapThemeToString(_themeService.CurrentTheme);
        RefreshAllStatuses();
    }

    partial void OnForgeModelsPathChanged(string value) => ForgeModelsStatus = EvaluateDirectoryStatus(value);
    partial void OnComfyModelsPathChanged(string value) => ComfyModelsStatus = EvaluateDirectoryStatus(value);
    partial void OnThreeDModelsPathChanged(string value) => ThreeDModelsStatus = EvaluateDirectoryStatus(value);
    partial void OnWorkflowsPathChanged(string value) => WorkflowsStatus = EvaluateDirectoryStatus(value);
    partial void OnComfyUiExecutablePathChanged(string value) => ComfyUiExecutableStatus = EvaluateExecutableStatus(value);
    partial void OnForgeExecutablePathChanged(string value) => ForgeExecutableStatus = EvaluateExecutableStatus(value);
    partial void OnAudioEngineExecutablePathChanged(string value) => AudioEngineExecutableStatus = EvaluateExecutableStatus(value);
    partial void OnOllamaExecutablePathChanged(string value)
    {
        OllamaStatus = EvaluateExecutableStatus(value);
        OnPropertyChanged(nameof(OllamaExecutableStatus));
    }

    partial void OnSelectedThemeChanged(string value)
    {
        var theme = MapStringToTheme(value);
        _themeService?.SetTheme(theme);
    }

    public static string MapThemeToString(AppTheme theme) => theme switch
    {
        AppTheme.OledBlack => "OLED Pure Black",
        AppTheme.Light => "Clean Light",
        _ => "Matte Carbon (Default)"
    };

    public static AppTheme MapStringToTheme(string? themeName) => themeName switch
    {
        "OLED Pure Black" => AppTheme.OledBlack,
        "Clean Light" => AppTheme.Light,
        _ => AppTheme.MatteCarbon
    };

    public void RefreshAllStatuses()
    {
        ComfyUiExecutableStatus = EvaluateExecutableStatus(ComfyUiExecutablePath);
        ForgeExecutableStatus = EvaluateExecutableStatus(ForgeExecutablePath);
        OllamaStatus = EvaluateExecutableStatus(OllamaExecutablePath);
        ForgeModelsStatus = EvaluateDirectoryStatus(ForgeModelsPath);
        ComfyModelsStatus = EvaluateDirectoryStatus(ComfyModelsPath);
        ThreeDModelsStatus = EvaluateDirectoryStatus(ThreeDModelsPath);
        WorkflowsStatus = EvaluateDirectoryStatus(WorkflowsPath);
        AudioEngineExecutableStatus = EvaluateExecutableStatus(AudioEngineExecutablePath);
        OnPropertyChanged(nameof(OllamaExecutableStatus));
    }

    public static string EvaluateExecutableStatus(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "⚠️ Missing";

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
            if (File.Exists(expanded))
                return "🟢 Verified";

            if (string.Equals(expanded, "ollama", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(expanded, "ollama.exe", StringComparison.OrdinalIgnoreCase))
            {
                var pathEnv = Environment.GetEnvironmentVariable("PATH");
                if (!string.IsNullOrEmpty(pathEnv))
                {
                    var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var dir in paths)
                    {
                        if (File.Exists(Path.Combine(dir, "ollama.exe")) || File.Exists(Path.Combine(dir, "ollama")))
                            return "🟢 Verified";
                    }
                }
            }
        }
        catch { }

        return "⚠️ Missing";
    }

    public static string EvaluateDirectoryStatus(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "⚠️ Missing";

        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
            if (Directory.Exists(expanded))
                return "🟢 Verified";
        }
        catch { }

        return "⚠️ Missing";
    }

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

    [RelayCommand]
    public async Task AutoDetectToolsAsync()
    {
        await AutoDetectToolsAsync("http://127.0.0.1:5246", new HttpClient());
    }

    public async Task AutoDetectToolsAsync(string apiBase, HttpClient http)
    {
        try
        {
            IsAutoDetecting = true;
            var response = await http.GetAsync($"{apiBase}/api/system/tools/detect");
            if (response.IsSuccessStatusCode)
            {
                var jsonStr = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonStr);
                var root = doc.RootElement;

                if (root.TryGetProperty("comfyUi", out var comfy))
                {
                    if (comfy.TryGetProperty("executablePath", out var cExe) && cExe.ValueKind == JsonValueKind.String)
                    {
                        var val = cExe.GetString();
                        if (!string.IsNullOrWhiteSpace(val) && string.IsNullOrWhiteSpace(ComfyUiExecutablePath))
                        {
                            ComfyUiExecutablePath = val;
                            ComfyUiExecutableStatus = "🔍 Auto-Discovered";
                        }
                    }
                    if (comfy.TryGetProperty("modelsDirectory", out var cModels) && cModels.ValueKind == JsonValueKind.String)
                    {
                        var val = cModels.GetString();
                        if (!string.IsNullOrWhiteSpace(val) && string.IsNullOrWhiteSpace(ComfyModelsPath))
                        {
                            ComfyModelsPath = val;
                            ComfyModelsStatus = "🔍 Auto-Discovered";
                        }
                    }
                }

                if (root.TryGetProperty("forge", out var forge))
                {
                    if (forge.TryGetProperty("executablePath", out var fExe) && fExe.ValueKind == JsonValueKind.String)
                    {
                        var val = fExe.GetString();
                        if (!string.IsNullOrWhiteSpace(val) && string.IsNullOrWhiteSpace(ForgeExecutablePath))
                        {
                            ForgeExecutablePath = val;
                            ForgeExecutableStatus = "🔍 Auto-Discovered";
                        }
                    }
                    if (forge.TryGetProperty("modelsDirectory", out var fModels) && fModels.ValueKind == JsonValueKind.String)
                    {
                        var val = fModels.GetString();
                        if (!string.IsNullOrWhiteSpace(val) && string.IsNullOrWhiteSpace(ForgeModelsPath))
                        {
                            ForgeModelsPath = val;
                            ForgeModelsStatus = "🔍 Auto-Discovered";
                        }
                    }
                }

                if (root.TryGetProperty("ollama", out var ollama))
                {
                    if (ollama.TryGetProperty("executablePath", out var oExe) && oExe.ValueKind == JsonValueKind.String)
                    {
                        var val = oExe.GetString();
                        if (!string.IsNullOrWhiteSpace(val) && (string.IsNullOrWhiteSpace(OllamaExecutablePath) || OllamaExecutablePath == "ollama"))
                        {
                            OllamaExecutablePath = val;
                            OllamaStatus = "🔍 Auto-Discovered";
                            OnPropertyChanged(nameof(OllamaExecutableStatus));
                        }
                    }
                }

                if (root.TryGetProperty("suggestedThreeDPath", out var s3d) && s3d.ValueKind == JsonValueKind.String)
                {
                    var val = s3d.GetString();
                    if (!string.IsNullOrWhiteSpace(val) && string.IsNullOrWhiteSpace(ThreeDModelsPath))
                    {
                        ThreeDModelsPath = val;
                        ThreeDModelsStatus = "🔍 Auto-Discovered";
                    }
                }

                if (root.TryGetProperty("suggestedWorkflowsPath", out var swf) && swf.ValueKind == JsonValueKind.String)
                {
                    var val = swf.GetString();
                    if (!string.IsNullOrWhiteSpace(val) && string.IsNullOrWhiteSpace(WorkflowsPath))
                    {
                        WorkflowsPath = val;
                        WorkflowsStatus = "🔍 Auto-Discovered";
                    }
                }

                if (root.TryGetProperty("audioEngine", out var audio))
                {
                    if (audio.TryGetProperty("executablePath", out var aExe) && aExe.ValueKind == JsonValueKind.String)
                    {
                        var val = aExe.GetString();
                        if (!string.IsNullOrWhiteSpace(val) && string.IsNullOrWhiteSpace(AudioEngineExecutablePath))
                        {
                            AudioEngineExecutablePath = val;
                            AudioEngineExecutableStatus = "🔍 Auto-Discovered";
                        }
                    }
                }

                ToastService.Instance.Show("Auto-detection complete.", ToastType.Success);
            }
            else
            {
                ToastService.Instance.Show("Auto-detection request failed.", ToastType.Warning);
            }
        }
        catch
        {
            ToastService.Instance.Show("Failed to run auto-detection.", ToastType.Error);
        }
        finally
        {
            IsAutoDetecting = false;
        }
    }

    [RelayCommand]
    public async Task BrowseComfyExecutableAsync(IStorageProvider? provider = null)
    {
        var path = await PickFileAsync(provider, "Select ComfyUI Executable", new[] { "*.bat", "*.cmd", "*.exe" });
        if (!string.IsNullOrWhiteSpace(path))
        {
            ComfyUiExecutablePath = path;
        }
    }

    [RelayCommand]
    public async Task BrowseForgeExecutableAsync(IStorageProvider? provider = null)
    {
        var path = await PickFileAsync(provider, "Select SD Forge Executable", new[] { "*.bat", "*.cmd", "*.exe" });
        if (!string.IsNullOrWhiteSpace(path))
        {
            ForgeExecutablePath = path;
        }
    }

    [RelayCommand]
    public async Task BrowseOllamaExecutableAsync(IStorageProvider? provider = null)
    {
        var path = await PickFileAsync(provider, "Select Ollama Executable", new[] { "*.exe", "*.cmd", "*.bat" });
        if (!string.IsNullOrWhiteSpace(path))
        {
            OllamaExecutablePath = path;
        }
    }

    [RelayCommand]
    public async Task BrowseForgeModelsAsync(IStorageProvider? provider = null)
    {
        var path = await PickFolderAsync(provider, "Select SD Forge Models Directory");
        if (!string.IsNullOrWhiteSpace(path))
        {
            ForgeModelsPath = path;
        }
    }

    [RelayCommand]
    public async Task BrowseComfyModelsAsync(IStorageProvider? provider = null)
    {
        var path = await PickFolderAsync(provider, "Select ComfyUI Models Directory");
        if (!string.IsNullOrWhiteSpace(path))
        {
            ComfyModelsPath = path;
        }
    }

    [RelayCommand]
    public async Task BrowseThreeDModelsAsync(IStorageProvider? provider = null)
    {
        var path = await PickFolderAsync(provider, "Select 3D Models Directory");
        if (!string.IsNullOrWhiteSpace(path))
        {
            ThreeDModelsPath = path;
        }
    }

    [RelayCommand]
    public async Task BrowseWorkflowsAsync(IStorageProvider? provider = null)
    {
        var path = await PickFolderAsync(provider, "Select Workflows Directory");
        if (!string.IsNullOrWhiteSpace(path))
        {
            WorkflowsPath = path;
        }
    }

    [RelayCommand]
    public async Task BrowseAudioEngineExecutableAsync(IStorageProvider? provider = null)
    {
        var path = await PickFileAsync(provider, "Select Audio Engine Executable or Script", new[] { "*.py", "*.bat", "*.cmd", "*.exe", "*.sh" });
        if (!string.IsNullOrWhiteSpace(path))
        {
            AudioEngineExecutablePath = path;
        }
    }

    [RelayCommand]
    public async Task TestVoiceSynthesizerAsync()
    {
        await TestVoiceSynthesizerAsync("http://127.0.0.1:5246", new HttpClient());
    }

    public async Task TestVoiceSynthesizerAsync(string apiBase, HttpClient http)
    {
        try
        {
            var payload = new
            {
                model = "kokoro",
                input = "Local LLM Server Manager audio text-to-speech engine test.",
                voice = string.IsNullOrWhiteSpace(PreferredAudioVoice) ? "af_heart" : PreferredAudioVoice,
                response_format = "mp3"
            };
            var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
            var response = await http.PostAsync($"{apiBase}/v1/audio/speech", content);
            if (response.IsSuccessStatusCode)
            {
                ToastService.Instance.Show("TTS Synthesis test succeeded!", ToastType.Success);
            }
            else
            {
                ToastService.Instance.Show($"TTS Synthesis test returned status code {(int)response.StatusCode}", ToastType.Warning);
            }
        }
        catch
        {
            ToastService.Instance.Show("Failed to test TTS Voice Synthesizer.", ToastType.Error);
        }
    }

    private async Task<string?> PickFileAsync(IStorageProvider? explicitProvider, string title, string[] patterns)
    {
        var provider = explicitProvider ?? StorageProvider;
        if (provider == null) return null;

        try
        {
            var fileTypes = new List<FilePickerFileType>
            {
                new("Executable Files") { Patterns = patterns },
                new("All Files (*.*)") { Patterns = new[] { "*.*" } }
            };

            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = fileTypes
            });

            if (files != null && files.Count > 0)
            {
                var item = files[0];
                return item.TryGetLocalPath() ?? (item.Path != null && item.Path.IsFile ? item.Path.LocalPath : item.Path?.ToString());
            }
        }
        catch { }

        return null;
    }

    private async Task<string?> PickFolderAsync(IStorageProvider? explicitProvider, string title)
    {
        var provider = explicitProvider ?? StorageProvider;
        if (provider == null) return null;

        try
        {
            var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

            if (folders != null && folders.Count > 0)
            {
                var item = folders[0];
                return item.TryGetLocalPath() ?? (item.Path != null && item.Path.IsFile ? item.Path.LocalPath : item.Path?.ToString());
            }
        }
        catch { }

        return null;
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
                    ForgeModelsPath = settings.ForgeModelsPath ?? "";
                    ComfyModelsPath = settings.ComfyModelsPath ?? "";
                    ComfyUiUrl = settings.ComfyUiUrl ?? "http://127.0.0.1:8188";
                    ThreeDModelsPath = settings.ThreeDModelsPath ?? "";
                    WorkflowsPath = settings.WorkflowsPath ?? "";
                    PreferredImageEngine = settings.PreferredImageEngine ?? "comfy";
                    ComfyUiExecutablePath = settings.ComfyUiExecutablePath ?? "";
                    ForgeExecutablePath = settings.ForgeExecutablePath ?? "";
                    OllamaExecutablePath = settings.OllamaExecutablePath ?? "ollama";
                    LanAccessUrl = settings.LanAccessUrl ?? "http://127.0.0.1:5246";
                    SelectedThemeStyle = settings.SelectedThemeStyle ?? "semi";
                    ServiceName = settings.ServiceName ?? "LocalLLMServerManager";
                    PublishOutputPath = settings.PublishOutputPath ?? "C:\\LocalLLMServerManager";
                    AudioEngineExecutablePath = settings.AudioEngineExecutablePath ?? "";
                    AudioEngineUrl = settings.AudioEngineUrl ?? "http://127.0.0.1:8880";
                    PreferredAudioVoice = settings.PreferredAudioVoice ?? "af_heart";

                    RefreshAllStatuses();
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
                OllamaExecutablePath: this.OllamaExecutablePath,
                ServiceName: this.ServiceName,
                PublishOutputPath: this.PublishOutputPath,
                ComfyModelsPath: this.ComfyModelsPath,
                LanAccessUrl: this.LanAccessUrl,
                SelectedThemeStyle: this.SelectedThemeStyle,
                AudioEngineExecutablePath: this.AudioEngineExecutablePath,
                AudioEngineUrl: this.AudioEngineUrl,
                PreferredAudioVoice: this.PreferredAudioVoice
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
