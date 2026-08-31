using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;

namespace LocalLLMServerManager.Shared.ViewModels;

public partial class OllamaLibraryViewModel : ObservableObject
{
    private readonly IOllamaModelService _ollamaModelService;
    private readonly ICanIRunItService _canIRunItService;
    private readonly ITelemetryService? _telemetryService;

    public ObservableCollection<OllamaModelItem> InstalledModels { get; } = new();
    public ObservableCollection<OllamaModelItem> FilteredInstalledModels { get; } = new();

    [ObservableProperty] private bool _isFullVramActive = true;
    [ObservableProperty] private bool _isPartialOffloadActive = true;
    [ObservableProperty] private bool _isCpuOnlyActive = true;
    [ObservableProperty] private bool _isOomActive = true;

    [ObservableProperty] private double _targetContextTokens = 8192;
    [ObservableProperty] private string _estimatedKvCacheText = "~0.5 GB";

    [ObservableProperty] private string _pullModelName = "";
    [ObservableProperty] private double _pullProgressPercent = 0;
    [ObservableProperty] private string _pullProgressBytesText = "";
    [ObservableProperty] private string _pullStatusLog = "";
    [ObservableProperty] private bool _isPullDrawerOpen = false;

    [ObservableProperty] private string _apiBase = OperatingSystem.IsBrowser() ? "" : "http://127.0.0.1:5246";

    [ObservableProperty] private double _totalVramMb = 16384.0;
    [ObservableProperty] private double _totalRamMb = 32768.0;

    public Action<string, string>? OnInspectModelRequested { get; set; }

    public OllamaLibraryViewModel(IOllamaModelService ollamaModelService)
        : this(ollamaModelService, new CanIRunItService(), null)
    {
    }

    public OllamaLibraryViewModel(
        IOllamaModelService ollamaModelService,
        ICanIRunItService? canIRunItService,
        ITelemetryService? telemetryService = null)
    {
        _ollamaModelService = ollamaModelService;
        _canIRunItService = canIRunItService ?? new CanIRunItService();
        _telemetryService = telemetryService;
    }

    public void UpdateHardwareTelemetry(double totalVramMb, double totalRamMb)
    {
        if (totalVramMb > 0) TotalVramMb = totalVramMb;
        if (totalRamMb > 0) TotalRamMb = totalRamMb;

        RecomputeBadges();
    }

    public void RecomputeBadges()
    {
        for (int i = 0; i < InstalledModels.Count; i++)
        {
            var m = InstalledModels[i];
            var badge = _canIRunItService.EvaluateQuickFit(m.Name, m.SizeBytes > 0 ? m.SizeBytes : null, "LLM", (long)TotalVramMb, (long)TotalRamMb);
            InstalledModels[i] = m with { FitBadge = badge };
        }
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        FilteredInstalledModels.Clear();
        foreach (var m in InstalledModels)
        {
            if (m.FitBadge == null)
            {
                FilteredInstalledModels.Add(m);
                continue;
            }

            bool matches = m.FitBadge.FitVerdict switch
            {
                FitVerdict.FullVram => IsFullVramActive,
                FitVerdict.PartialOffload => IsPartialOffloadActive,
                FitVerdict.CpuOnly => IsCpuOnlyActive,
                FitVerdict.OutOfMemory => IsOomActive,
                _ => true
            };

            if (matches)
            {
                FilteredInstalledModels.Add(m);
            }
        }
    }

    [RelayCommand]
    public void ToggleFitVerdict(string verdict)
    {
        var v = (verdict ?? "").Trim().ToLowerInvariant();
        if (v.Contains("full") || v.Contains("vram"))
        {
            IsFullVramActive = !IsFullVramActive;
        }
        else if (v.Contains("partial") || v.Contains("offload"))
        {
            IsPartialOffloadActive = !IsPartialOffloadActive;
        }
        else if (v.Contains("cpu"))
        {
            IsCpuOnlyActive = !IsCpuOnlyActive;
        }
        else if (v.Contains("oom") || v.Contains("won") || v.Contains("memory"))
        {
            IsOomActive = !IsOomActive;
        }
        ApplyFilter();
    }

    partial void OnIsFullVramActiveChanged(bool value) => ApplyFilter();
    partial void OnIsPartialOffloadActiveChanged(bool value) => ApplyFilter();
    partial void OnIsCpuOnlyActiveChanged(bool value) => ApplyFilter();
    partial void OnIsOomActiveChanged(bool value) => ApplyFilter();

    [RelayCommand]
    public void NavigateToCanIRunIt(string? modelName)
    {
        if (!string.IsNullOrWhiteSpace(modelName))
        {
            OnInspectModelRequested?.Invoke(modelName, "LLM");
        }
    }

    [RelayCommand]
    public void InspectModel(OllamaModelItem? item)
    {
        if (item != null)
        {
            OnInspectModelRequested?.Invoke(item.Name, "LLM");
        }
    }

    partial void OnTargetContextTokensChanged(double value)
    {
        double estimatedBytes = value * 65536.0;
        double mb = estimatedBytes / (1024.0 * 1024.0);
        EstimatedKvCacheText = mb >= 1024 ? $"~{(mb / 1024.0):F1} GB" : $"~{mb:F0} MB";
    }

    private readonly System.Threading.SemaphoreSlim _loadLock = new(1, 1);

    public async Task LoadInstalledModelsAsync(string apiBase, HttpClient http)
    {
        await _loadLock.WaitAsync();
        try
        {
            var models = await _ollamaModelService.LoadInstalledModelsAsync(apiBase, http);
            InstalledModels.Clear();
            foreach (var m in models)
            {
                var badge = _canIRunItService.EvaluateQuickFit(m.Name, m.SizeBytes > 0 ? m.SizeBytes : null, "LLM", (long)TotalVramMb, (long)TotalRamMb);
                InstalledModels.Add(m with { FitBadge = badge });
            }
            ApplyFilter();
        }
        finally
        {
            _loadLock.Release();
        }
    }

    [RelayCommand]
    public async Task DeleteModelAsync(OllamaModelItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Name)) return;

        ToastService.Instance.Show($"Deleting model '{item.Name}'...", ToastType.Info);
        try
        {
            var success = await _ollamaModelService.DeleteModelAsync(ApiBase, item.Name, HttpHelper.CreateClient(ApiBase));
            if (success)
            {
                InstalledModels.Remove(item);
                ApplyFilter();
                ToastService.Instance.Show($"Model '{item.Name}' deleted successfully.", ToastType.Success);
            }
            else
            {
                ToastService.Instance.Show($"Failed to delete model '{item.Name}'.", ToastType.Error);
            }
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"Error deleting model '{item.Name}': {ex.Message}", ToastType.Error);
        }
    }

    [RelayCommand]
    public async Task UnloadAllVramAsync()
    {
        await UnloadAllVramAsync(ApiBase, HttpHelper.CreateClient(ApiBase));
    }

    public async Task UnloadAllVramAsync(string apiBase, HttpClient http)
    {
        ToastService.Instance.Show("Unloading all models from VRAM...", ToastType.Info);
        await _ollamaModelService.UnloadAllVramAsync(apiBase, http);
        await Task.Delay(1000);
        await LoadInstalledModelsAsync(apiBase, http);
        ToastService.Instance.Show("All models unloaded from VRAM successfully.", ToastType.Success);
    }

    public async Task PullModelAsync(string fullPullString, HttpClient http)
    {
        if (string.IsNullOrWhiteSpace(fullPullString)) return;

        PullModelName = fullPullString;
        PullProgressPercent = 0;
        PullStatusLog = $"Connecting to Ollama to pull '{fullPullString}'...\n";
        IsPullDrawerOpen = true;

        ToastService.Instance.Show($"Started pulling model '{fullPullString}'", ToastType.Info);

        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { name = fullPullString, stream = true }),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            using var req = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:11434/api/pull") { Content = content };
            using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

            if (resp.IsSuccessStatusCode)
            {
                using var stream = await resp.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    string? line = await reader.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        try
                        {
                            var doc = JsonNode.Parse(line);
                            string status = doc?["status"]?.ToString() ?? "";
                            long total = doc?["total"]?.GetValue<long>() ?? 0L;
                            long completed = doc?["completed"]?.GetValue<long>() ?? 0L;

                            if (total > 0)
                            {
                                PullProgressPercent = Math.Round(((double)completed / total) * 100, 1);
                                double compMb = completed / (1024.0 * 1024.0);
                                double totMb = total / (1024.0 * 1024.0);
                                PullProgressBytesText = $"{compMb:F1} MB / {totMb:F1} MB ({PullProgressPercent}%)";
                            }

                            PullStatusLog += $"{status}\n";
                        }
                        catch { }
                    }
                }

                ToastService.Instance.Show($"Model '{fullPullString}' pulled successfully!", ToastType.Success);
            }
        }
        catch (Exception ex)
        {
            PullStatusLog += $"\nError: {ex.Message}\n";
            ToastService.Instance.Show($"Failed to pull model '{fullPullString}'.", ToastType.Error);
        }
    }

    [RelayCommand]
    public void ClosePullDrawer()
    {
        IsPullDrawerOpen = false;
    }
}
