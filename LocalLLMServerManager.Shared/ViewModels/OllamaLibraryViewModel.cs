using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Input.Platform;
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
    [ObservableProperty] private OllamaModelItem? _selectedInstalledModel;

    [ObservableProperty] private string _calcModelName = "llama3.2:latest (3B, Q4_K_M)";
    [ObservableProperty] private string _calcModelWeightText = "~2.2 GB";
    [ObservableProperty] private string _calcTotalVramNeededText = "~3.3 GB / 16.0 GB";
    [ObservableProperty] private double _calcVramUsageRatio = 0.20;
    [ObservableProperty] private string _calcFitVerdictText = "🟢 Full VRAM (28/28 layers on GPU)";
    [ObservableProperty] private string _calcFitVerdictColor = "#10B981";
    [ObservableProperty] private string _calcRecommendationMessage = "";
    [ObservableProperty] private string _selectedKvPrecision = "FP16";
    [ObservableProperty] private FitVerdict _calcFitVerdict = FitVerdict.FullVram;
    [ObservableProperty] private LlmFitResult? _currentFitResult;

    [ObservableProperty] private bool _isFullVramActive = true;
    [ObservableProperty] private bool _isPartialOffloadActive = true;
    [ObservableProperty] private bool _isCpuOnlyActive = true;
    [ObservableProperty] private bool _isOomActive = true;

    [ObservableProperty] private bool _isDeleteModalOpen = false;
    [ObservableProperty] private OllamaModelItem? _modelToDelete = null;
    [ObservableProperty] private string _deleteModalTitle = "Delete Model";
    [ObservableProperty] private string _deleteModalMessage = "";
    [ObservableProperty] private bool _isDeleting = false;

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
    public Action<string, int>? OnApplyModelContextRequested { get; set; }

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
        InstalledModels.CollectionChanged += (s, e) => ApplyFilter();
        RecalculateKvCache();
    }

    public void UpdateHardwareTelemetry(double totalVramMb, double totalRamMb)
    {
        if (totalVramMb > 0) TotalVramMb = totalVramMb;
        if (totalRamMb > 0) TotalRamMb = totalRamMb;

        RecomputeBadges();
        RecalculateKvCache();
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

    public bool IsFp16Selected => SelectedKvPrecision == "FP16";
    public bool IsQ8Selected => SelectedKvPrecision == "Q8_0";
    public bool IsQ4Selected => SelectedKvPrecision == "Q4_0";

    partial void OnTargetContextTokensChanged(double value) => RecalculateKvCache();
    partial void OnSelectedInstalledModelChanged(OllamaModelItem? value) => RecalculateKvCache();
    partial void OnSelectedKvPrecisionChanged(string value)
    {
        OnPropertyChanged(nameof(IsFp16Selected));
        OnPropertyChanged(nameof(IsQ8Selected));
        OnPropertyChanged(nameof(IsQ4Selected));
        RecalculateKvCache();
    }

    public void RecalculateKvCache()
    {
        var model = SelectedInstalledModel ?? InstalledModels.FirstOrDefault();
        string modelName = model?.Name ?? "llama3.2:latest";
        double paramsB = CanIRunItService.ExtractParamBillions(modelName);
        string quant = CanIRunItService.ExtractQuantization(modelName);

        var req = new LlmFitRequest(
            ParametersBillions: paramsB,
            Quantization: quant,
            ContextLength: (int)TargetContextTokens,
            KvPrecision: SelectedKvPrecision,
            AvailableVramMb: (long)TotalVramMb,
            AvailableRamMb: (long)TotalRamMb
        );

        var fit = _canIRunItService.EvaluateLlmFit(req);
        CurrentFitResult = fit;
        CalcFitVerdict = fit.FitVerdict;
        CalcModelName = $"{modelName} ({paramsB:G2}B, {quant})";

        double weightGb = fit.ModelWeightMb / 1024.0;
        CalcModelWeightText = weightGb >= 1.0 ? $"{weightGb:F1} GB" : $"{fit.ModelWeightMb:N0} MB";

        double kvGb = fit.KvCacheMb / 1024.0;
        EstimatedKvCacheText = kvGb >= 1.0 ? $"{kvGb:F1} GB" : $"{fit.KvCacheMb:N0} MB";

        long totalNeededMb = fit.ModelWeightMb + fit.KvCacheMb + fit.OverheadMb;
        double totalGb = totalNeededMb / 1024.0;
        double vramGb = TotalVramMb / 1024.0;
        CalcTotalVramNeededText = $"{totalGb:F1} GB / {vramGb:F1} GB";
        CalcVramUsageRatio = TotalVramMb > 0 ? Math.Min(1.0, (double)totalNeededMb / TotalVramMb) : 0.0;

        CalcFitVerdictText = fit.FitVerdict switch
        {
            FitVerdict.FullVram => $"🟢 Full VRAM ({fit.GpuLayers}/{fit.TotalLayers} layers on GPU • ~{fit.EstimatedTokPerSec:F0} tok/s)",
            FitVerdict.PartialOffload => $"🟡 Partial Offload ({fit.GpuLayers} GPU / {fit.CpuLayers} CPU layers • ~{fit.EstimatedTokPerSec:F1} tok/s)",
            FitVerdict.CpuOnly => "🟠 CPU Only (VRAM Insufficient)",
            FitVerdict.OutOfMemory => "🔴 System Out of Memory",
            _ => "Unknown"
        };

        CalcFitVerdictColor = fit.FitVerdict switch
        {
            FitVerdict.FullVram => "#10B981",
            FitVerdict.PartialOffload => "#F59E0B",
            FitVerdict.CpuOnly => "#F97316",
            FitVerdict.OutOfMemory => "#EF4444",
            _ => "#94A3B8"
        };

        CalcRecommendationMessage = fit.RecommendationMessage;
    }

    [RelayCommand]
    public void SelectModel(OllamaModelItem? model)
    {
        if (model == null) return;
        SelectedInstalledModel = model;
        for (int i = 0; i < InstalledModels.Count; i++)
        {
            var m = InstalledModels[i];
            InstalledModels[i] = m with { IsSelected = (m.Name == model.Name) };
        }
        ApplyFilter();
        RecalculateKvCache();
    }

    [RelayCommand]
    public void SetContextPreset(string? preset)
    {
        if (string.IsNullOrWhiteSpace(preset)) return;
        var p = preset.Trim().ToUpperInvariant();
        if (p.EndsWith("K") && double.TryParse(p[..^1], out double k))
        {
            TargetContextTokens = k * 1024;
        }
        else if (double.TryParse(p, out double num))
        {
            TargetContextTokens = num;
        }
    }

    [RelayCommand]
    public void SetKvPrecision(string? precision)
    {
        if (string.IsNullOrWhiteSpace(precision)) return;
        SelectedKvPrecision = precision.Trim().ToUpperInvariant();
        RecalculateKvCache();
    }

    [RelayCommand]
    public void UseModelWithContextInStudio()
    {
        var modelName = SelectedInstalledModel?.Name ?? InstalledModels.FirstOrDefault()?.Name ?? "llama3.2:latest";
        int tokens = (int)TargetContextTokens;
        OnApplyModelContextRequested?.Invoke(modelName, tokens);
        ToastService.Instance.Show($"Loaded '{modelName}' with {tokens:N0} tokens into Studio.", ToastType.Success);
    }

    [RelayCommand]
    public async Task CopyModelfileParameterAsync()
    {
        string text = $"PARAMETER num_ctx {(int)TargetContextTokens}";
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow?.Clipboard != null)
            {
                await desktop.MainWindow.Clipboard.SetTextAsync(text);
            }
        }
        catch { }
        ToastService.Instance.Show($"Copied to clipboard: '{text}'", ToastType.Success);
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
            if (SelectedInstalledModel == null && InstalledModels.Count > 0)
            {
                SelectModel(InstalledModels[0]);
            }
            else
            {
                ApplyFilter();
                RecalculateKvCache();
            }
        }
        finally
        {
            _loadLock.Release();
        }
    }

    [RelayCommand]
    public void RequestDeleteModel(OllamaModelItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Name)) return;
        ModelToDelete = item;
        DeleteModalTitle = $"Delete {item.Name}?";
        DeleteModalMessage = $"Are you sure you want to permanently delete model '{item.Name}' ({item.FormatSize}) from local storage? This action cannot be undone.";
        IsDeleteModalOpen = true;
    }

    [RelayCommand]
    public void CancelDeleteModel()
    {
        IsDeleteModalOpen = false;
        ModelToDelete = null;
    }

    [RelayCommand]
    public async Task ConfirmDeleteModelAsync()
    {
        if (ModelToDelete == null)
        {
            IsDeleteModalOpen = false;
            return;
        }

        IsDeleting = true;
        var targetItem = ModelToDelete;
        try
        {
            await DeleteModelAsync(targetItem);
        }
        finally
        {
            IsDeleting = false;
            IsDeleteModalOpen = false;
            ModelToDelete = null;
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
