using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Services;

namespace LocalLLMServerManager.Shared.ViewModels;

public partial class CivitaiSearchViewModel : ObservableObject
{
    private readonly ICivitaiSearchService _civitaiSearchService;

    [ObservableProperty] private string _civitaiSearchQuery = "";
    [ObservableProperty] private string _selectedCivitaiType = "Checkpoint";
    public ObservableCollection<CivitaiModelItem> CivitaiResults { get; } = new();

    public CivitaiSearchViewModel(ICivitaiSearchService civitaiSearchService)
    {
        _civitaiSearchService = civitaiSearchService;
    }

    public async Task SearchCivitaiAsync(string apiBase, HttpClient http)
    {
        try
        {
            var results = await _civitaiSearchService.SearchModelsAsync(apiBase, CivitaiSearchQuery, SelectedCivitaiType, "Most Downloaded", http);
            CivitaiResults.Clear();
            foreach (var r in results)
            {
                CivitaiResults.Add(r);
            }
        }
        catch
        {
            ToastService.Instance.Show("Failed to search CivitAI models.", ToastType.Error);
        }
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
