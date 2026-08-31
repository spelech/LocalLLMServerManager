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
    }

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
        }
        finally
        {
            _loadLock.Release();
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
