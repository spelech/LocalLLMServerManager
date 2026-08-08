using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Services;

namespace LocalLLMServerManager.Shared.ViewModels;

public partial class HuggingFaceSearchViewModel : ObservableObject
{
    private readonly IHuggingFaceSearchService _hfSearchService;

    [ObservableProperty] private string _hfSearchQuery = "";
    public ObservableCollection<HuggingFaceRepoItem> HuggingFaceResults { get; } = new();

    [ObservableProperty] private bool _isHfModalOpen = false;
    [ObservableProperty] private string _modalRepoId = "";
    [ObservableProperty] private string _modalAuthor = "";
    public ObservableCollection<HfFileQuantItem> ModalHfFiles { get; } = new();

    public HuggingFaceSearchViewModel(IHuggingFaceSearchService hfSearchService)
    {
        _hfSearchService = hfSearchService;
    }

    public async Task SearchHuggingFaceAsync(string apiBase, HttpClient http)
    {
        try
        {
            var results = await _hfSearchService.SearchRepositoriesAsync(apiBase, HfSearchQuery, http);
            HuggingFaceResults.Clear();
            foreach (var r in results)
            {
                HuggingFaceResults.Add(r);
            }
        }
        catch
        {
            ToastService.Instance.Show("Failed to query Hugging Face Hub.", ToastType.Error);
        }
    }

    public async Task OpenHfModalAsync(string repoId, string apiBase, HttpClient http)
    {
        if (string.IsNullOrWhiteSpace(repoId)) return;

        ModalRepoId = repoId;
        ModalAuthor = repoId.Contains("/") ? repoId.Split('/')[0] : "Community";
        ModalHfFiles.Clear();
        IsHfModalOpen = true;

        var quants = await _hfSearchService.FetchQuantizationsAsync(apiBase, repoId, http);
        foreach (var q in quants)
        {
            ModalHfFiles.Add(q);
        }
    }

    [RelayCommand]
    public void CloseHfModal()
    {
        IsHfModalOpen = false;
    }
}
