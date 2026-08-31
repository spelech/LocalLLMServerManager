using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;

namespace LocalLLMServerManager.Shared.ViewModels;

public partial class CivitaiSearchViewModel : ObservableObject
{
    private readonly ICivitaiSearchService _civitaiSearchService;
    private readonly ICanIRunItService _canIRunItService;
    private readonly ITelemetryService? _telemetryService;

    [ObservableProperty] private string _civitaiSearchQuery = "";
    [ObservableProperty] private string _selectedCivitaiType = "Checkpoint";
    public ObservableCollection<CivitaiModelItem> CivitaiResults { get; } = new();
    public ObservableCollection<CivitaiModelItem> FilteredCivitaiResults { get; } = new();

    // Hardware Compatibility Filter Flags
    [ObservableProperty] private bool _isFullVramActive = true;
    [ObservableProperty] private bool _isPartialOffloadActive = true;
    [ObservableProperty] private bool _isCpuOnlyActive = true;
    [ObservableProperty] private bool _isOomActive = true;

    [ObservableProperty] private string _apiBase = OperatingSystem.IsBrowser() ? "" : "http://127.0.0.1:5246";

    [ObservableProperty] private double _totalVramMb = 16384.0;
    [ObservableProperty] private double _totalRamMb = 32768.0;

    public Action<string, string>? OnInspectModelRequested { get; set; }

    public CivitaiSearchViewModel(ICivitaiSearchService civitaiSearchService)
        : this(civitaiSearchService, new CanIRunItService(), null)
    {
    }

    public CivitaiSearchViewModel(
        ICivitaiSearchService civitaiSearchService,
        ICanIRunItService? canIRunItService,
        ITelemetryService? telemetryService = null)
    {
        _civitaiSearchService = civitaiSearchService;
        _canIRunItService = canIRunItService ?? new CanIRunItService();
        _telemetryService = telemetryService;
        CivitaiResults.CollectionChanged += (s, e) => ApplyFilter();
    }

    public void UpdateHardwareTelemetry(double totalVramMb, double totalRamMb)
    {
        if (totalVramMb > 0) TotalVramMb = totalVramMb;
        if (totalRamMb > 0) TotalRamMb = totalRamMb;

        RecomputeBadges();
    }

    public void RecomputeBadges()
    {
        for (int i = 0; i < CivitaiResults.Count; i++)
        {
            var r = CivitaiResults[i];
            var badge = _canIRunItService.EvaluateQuickFit(r.Name, r.SizeBytes > 0 ? r.SizeBytes : null, "Image", (long)TotalVramMb, (long)TotalRamMb);
            CivitaiResults[i] = r with { FitBadge = badge };
        }
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        FilteredCivitaiResults.Clear();
        foreach (var r in CivitaiResults)
        {
            if (r.FitBadge == null)
            {
                FilteredCivitaiResults.Add(r);
                continue;
            }

            bool matches = r.FitBadge.FitVerdict switch
            {
                FitVerdict.FullVram => IsFullVramActive,
                FitVerdict.PartialOffload => IsPartialOffloadActive,
                FitVerdict.CpuOnly => IsCpuOnlyActive,
                FitVerdict.OutOfMemory => IsOomActive,
                _ => true
            };

            if (matches)
            {
                FilteredCivitaiResults.Add(r);
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
            OnInspectModelRequested?.Invoke(modelName, "Image");
        }
    }

    [RelayCommand]
    public void InspectModel(CivitaiModelItem? item)
    {
        if (item != null)
        {
            OnInspectModelRequested?.Invoke(item.Name, "Image");
        }
    }

    [RelayCommand]
    public async Task SearchCivitaiAsync()
    {
        await SearchCivitaiAsync(ApiBase, HttpHelper.CreateClient(ApiBase));
    }

    public async Task SearchCivitaiAsync(string apiBase, HttpClient http)
    {
        try
        {
            var results = await _civitaiSearchService.SearchModelsAsync(apiBase, CivitaiSearchQuery, SelectedCivitaiType, "Most Downloaded", http);
            CivitaiResults.Clear();
            foreach (var r in results)
            {
                var badge = _canIRunItService.EvaluateQuickFit(r.Name, r.SizeBytes > 0 ? r.SizeBytes : null, "Image", (long)TotalVramMb, (long)TotalRamMb);
                CivitaiResults.Add(r with { FitBadge = badge });
            }
            ApplyFilter();
        }
        catch
        {
            ToastService.Instance.Show("Failed to search CivitAI models.", ToastType.Error);
        }
    }

    [RelayCommand]
    public async Task DownloadCivitaiModelAsync(CivitaiModelItem item)
    {
        await DownloadCivitaiModelAsync(item, ApiBase, HttpHelper.CreateClient(ApiBase));
    }

    public async Task DownloadCivitaiModelAsync(CivitaiModelItem item, string apiBase, HttpClient http)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.DownloadUrl)) return;

        ToastService.Instance.Show($"Queued download for '{item.Name}'", ToastType.Info);

        try
        {
            var url = $"{apiBase}/api/civitai/download?fileUrl={Uri.EscapeDataString(item.DownloadUrl)}&modelType={Uri.EscapeDataString(item.Type)}&fileName={Uri.EscapeDataString(item.FileName)}";
            var resp = await http.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                ToastService.Instance.Show($"Download started for '{item.Name}'", ToastType.Success);
            }
        }
        catch
        {
            ToastService.Instance.Show($"Failed to queue download for '{item.Name}'", ToastType.Error);
        }
    }
}
