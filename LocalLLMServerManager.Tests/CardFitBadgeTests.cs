using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class CardFitBadgeTests
{
    private class FakeHfSearchService : IHuggingFaceSearchService
    {
        public List<HuggingFaceRepoItem> ReturnRepos { get; set; } = new();
        public List<HfFileQuantItem> ReturnQuants { get; set; } = new();

        public Task<List<HuggingFaceRepoItem>> SearchRepositoriesAsync(string apiBase, string query, HttpClient http)
            => Task.FromResult(ReturnRepos);

        public Task<List<HuggingFaceRepoItem>> SearchRepositoriesAsync(string apiBase, string query, string? pipelineTag, HttpClient http)
            => Task.FromResult(ReturnRepos);

        public Task<List<HuggingFaceRepoItem>> SearchModelsAsync(string query, string? pipelineTag = null, CancellationToken ct = default)
            => Task.FromResult(ReturnRepos);

        public Task<List<HfFileQuantItem>> FetchQuantizationsAsync(string apiBase, string repoId, HttpClient http)
            => Task.FromResult(ReturnQuants);
    }

    private class FakeCivitaiSearchService : ICivitaiSearchService
    {
        public List<CivitaiModelItem> ReturnModels { get; set; } = new();

        public Task<List<CivitaiModelItem>> SearchModelsAsync(string apiBase, string query, string types, string sort, HttpClient http)
            => Task.FromResult(ReturnModels);
    }

    private class FakeOllamaModelService : IOllamaModelService
    {
        public List<OllamaModelItem> ReturnModels { get; set; } = new();

        public Task<List<OllamaModelItem>> LoadInstalledModelsAsync(string apiBase, HttpClient http)
            => Task.FromResult(ReturnModels);

        public Task<List<OllamaModelItem>> GetInstalledModelsAsync()
            => Task.FromResult(ReturnModels);

        public Task<bool> UnloadAllVramAsync(string apiBase, HttpClient http) => Task.FromResult(true);
        public Task<bool> PreloadModelAsync(string apiBase, string modelName, HttpClient http) => Task.FromResult(true);
        public Task<bool> PullModelAsync(string modelName) => Task.FromResult(true);
    }

    [Fact]
    public async Task HuggingFaceSearchViewModel_Search_ComputesFitBadgesForRepositories()
    {
        var hfService = new FakeHfSearchService
        {
            ReturnRepos = new List<HuggingFaceRepoItem>
            {
                new("meta-llama/Llama-3.1-8B-Instruct", "meta-llama", 1500, "500K", "text-generation"),
                new("deepseek-ai/DeepSeek-R1-671B", "deepseek-ai", 5000, "1M", "text-generation"),
                new("Wan-AI/Wan2.1-T2V-14B", "Wan-AI", 800, "100K", "text-to-video")
            }
        };

        var canIRunIt = new CanIRunItService();
        var vm = new HuggingFaceSearchViewModel(hfService, canIRunIt)
        {
            TotalVramMb = 16384,
            TotalRamMb = 32768
        };

        await vm.SearchHuggingFaceAsync("http://127.0.0.1:5246", new HttpClient());

        Assert.Equal(3, vm.HuggingFaceResults.Count);

        var llamaItem = vm.HuggingFaceResults[0];
        Assert.NotNull(llamaItem.FitBadge);
        Assert.Equal(FitVerdict.FullVram, llamaItem.FitBadge.FitVerdict);
        Assert.Contains("Full VRAM", llamaItem.FitBadge.BadgeText);

        var deepseekItem = vm.HuggingFaceResults[1];
        Assert.NotNull(deepseekItem.FitBadge);
        Assert.Equal(FitVerdict.OutOfMemory, deepseekItem.FitBadge.FitVerdict);
        Assert.Contains("Won't Fit", deepseekItem.FitBadge.BadgeText);

        var videoItem = vm.HuggingFaceResults[2];
        Assert.NotNull(videoItem.FitBadge);
        Assert.Equal(FitVerdict.PartialOffload, videoItem.FitBadge.FitVerdict);
        Assert.Contains("Partial Offload", videoItem.FitBadge.BadgeText);
    }

    [Fact]
    public async Task HuggingFaceSearchViewModel_FetchQuantizations_ComputesFitBadgesPerFile()
    {
        var hfService = new FakeHfSearchService
        {
            ReturnQuants = new List<HfFileQuantItem>
            {
                new("Meta-Llama-3.1-8B-Q4_K_M.gguf", "Q4_K_M", "4.9 GB", 4900L * 1024 * 1024),
                new("DeepSeek-R1-671B-Q4_K_M.gguf", "Q4_K_M", "400 GB", 400000L * 1024 * 1024)
            }
        };

        var canIRunIt = new CanIRunItService();
        var vm = new HuggingFaceSearchViewModel(hfService, canIRunIt)
        {
            TotalVramMb = 16384,
            TotalRamMb = 32768
        };

        await vm.OpenHfModalAsync("meta-llama/Llama-3.1-8B-Instruct", "http://127.0.0.1:5246", new HttpClient());

        Assert.Equal(2, vm.ModalHfFiles.Count);
        Assert.NotNull(vm.ModalHfFiles[0].FitBadge);
        Assert.Equal(FitVerdict.FullVram, vm.ModalHfFiles[0].FitBadge.FitVerdict);

        Assert.NotNull(vm.ModalHfFiles[1].FitBadge);
        Assert.Equal(FitVerdict.OutOfMemory, vm.ModalHfFiles[1].FitBadge.FitVerdict);
    }

    [Fact]
    public void HuggingFaceSearchViewModel_InspectModelCommand_InvokesCallbackWithModality()
    {
        var vm = new HuggingFaceSearchViewModel(new FakeHfSearchService());
        string? inspectedModel = null;
        string? inspectedModality = null;

        vm.OnInspectModelRequested = (m, mod) =>
        {
            inspectedModel = m;
            inspectedModality = mod;
        };

        var item = new HuggingFaceRepoItem("Wan-AI/Wan2.1-T2V-14B", "Wan-AI", 100, "10K", "text-to-video");
        vm.InspectModelCommand.Execute(item);

        Assert.Equal("Wan-AI/Wan2.1-T2V-14B", inspectedModel);
        Assert.Equal("Video", inspectedModality);

        vm.NavigateToCanIRunItCommand.Execute("meta-llama/Llama-3.1-8B");
        Assert.Equal("meta-llama/Llama-3.1-8B", inspectedModel);
        Assert.Equal("LLM", inspectedModality);
    }

    [Fact]
    public async Task CivitaiSearchViewModel_Search_ComputesFitBadgesForCheckpoints()
    {
        var civitaiService = new FakeCivitaiSearchService
        {
            ReturnModels = new List<CivitaiModelItem>
            {
                new(1, "Flux.1 Dev Checkpoint", "Checkpoint", "", "https://civitai.com/api/download/1", "flux1-dev.safetensors", 4.9, 1500, null, 12000L * 1024 * 1024),
                new(2, "SD 1.5 Realistic Vision", "Checkpoint", "", "https://civitai.com/api/download/2", "v1-5-pruned.safetensors", 4.8, 8000, null, 2000L * 1024 * 1024)
            }
        };

        var canIRunIt = new CanIRunItService();
        var vm = new CivitaiSearchViewModel(civitaiService, canIRunIt)
        {
            TotalVramMb = 16384,
            TotalRamMb = 32768
        };

        await vm.SearchCivitaiAsync("http://127.0.0.1:5246", new HttpClient());

        Assert.Equal(2, vm.CivitaiResults.Count);

        var fluxItem = vm.CivitaiResults[0];
        Assert.NotNull(fluxItem.FitBadge);
        Assert.Equal(FitVerdict.FullVram, fluxItem.FitBadge.FitVerdict);

        var sd15Item = vm.CivitaiResults[1];
        Assert.NotNull(sd15Item.FitBadge);
        Assert.Equal(FitVerdict.FullVram, sd15Item.FitBadge.FitVerdict);
    }

    [Fact]
    public void CivitaiSearchViewModel_InspectModelCommand_InvokesCallbackWithImageModality()
    {
        var vm = new CivitaiSearchViewModel(new FakeCivitaiSearchService());
        string? inspectedModel = null;
        string? inspectedModality = null;

        vm.OnInspectModelRequested = (m, mod) =>
        {
            inspectedModel = m;
            inspectedModality = mod;
        };

        var item = new CivitaiModelItem(10, "Pony Diffusion V6 XL", "Checkpoint", "", "", "pony.safetensors", 4.9, 500);
        vm.InspectModelCommand.Execute(item);

        Assert.Equal("Pony Diffusion V6 XL", inspectedModel);
        Assert.Equal("Image", inspectedModality);
    }

    [Fact]
    public async Task OllamaLibraryViewModel_LoadInstalledModels_ComputesFitBadges()
    {
        var ollamaService = new FakeOllamaModelService
        {
            ReturnModels = new List<OllamaModelItem>
            {
                new("llama3.1:8b", "4.7 GB", "ð» Coding & General", "#38BDF8", false, null, 4700L * 1024 * 1024),
                new("deepseek-r1:671b", "404 GB", "ð§  Reasoning profile", "#A855F7", false, null, 404000L * 1024 * 1024)
            }
        };

        var canIRunIt = new CanIRunItService();
        var vm = new OllamaLibraryViewModel(ollamaService, canIRunIt)
        {
            TotalVramMb = 16384,
            TotalRamMb = 32768
        };

        await vm.LoadInstalledModelsAsync("http://127.0.0.1:5246", new HttpClient());

        Assert.Equal(2, vm.InstalledModels.Count);

        var llama = vm.InstalledModels[0];
        Assert.NotNull(llama.FitBadge);
        Assert.Equal(FitVerdict.FullVram, llama.FitBadge.FitVerdict);

        var deepseek = vm.InstalledModels[1];
        Assert.NotNull(deepseek.FitBadge);
        Assert.Equal(FitVerdict.OutOfMemory, deepseek.FitBadge.FitVerdict);
    }

    [Fact]
    public void OllamaLibraryViewModel_InspectModelCommand_InvokesCallbackWithLlmModality()
    {
        var vm = new OllamaLibraryViewModel(new FakeOllamaModelService());
        string? inspectedModel = null;
        string? inspectedModality = null;

        vm.OnInspectModelRequested = (m, mod) =>
        {
            inspectedModel = m;
            inspectedModality = mod;
        };

        var item = new OllamaModelItem("qwen2.5:32b", "19 GB", "ð» Coding & General", "#38BDF8", false);
        vm.InspectModelCommand.Execute(item);

        Assert.Equal("qwen2.5:32b", inspectedModel);
        Assert.Equal("LLM", inspectedModality);
    }

    [Fact]
    public void MainViewModel_CrossViewModelWiring_HooksUpNavigationToCanIRunIt()
    {
        var mainVm = new MainViewModel();

        // 1. Check HF sub-viewmodel navigation
        var hfItem = new HuggingFaceRepoItem("Wan-AI/Wan2.1-T2V-14B", "Wan-AI", 100, "10K", "text-to-video");
        mainVm.HuggingFace.InspectModelCommand.Execute(hfItem);

        Assert.Equal(4, mainVm.SelectedTabIndex);
        Assert.Equal("Video", mainVm.HardwareFit.SelectedModality);
        Assert.Equal("Wan 2.2 14B", mainVm.HardwareFit.SelectedVideoPreset);

        // 2. Check CivitAI sub-viewmodel navigation
        var civItem = new CivitaiModelItem(1, "Flux.1 Dev", "Checkpoint", "", "", "flux.safetensors", 4.9, 100);
        mainVm.Civitai.InspectModelCommand.Execute(civItem);

        Assert.Equal(4, mainVm.SelectedTabIndex);
        Assert.Equal("Image", mainVm.HardwareFit.SelectedModality);
        Assert.Equal("Flux.1 Dev", mainVm.HardwareFit.SelectedImagePreset);

        // 3. Check Ollama sub-viewmodel navigation
        var ollamaItem = new OllamaModelItem("llama3.3:70b", "42 GB", "ð» Coding & General", "#38BDF8", false);
        mainVm.Ollama.InspectModelCommand.Execute(ollamaItem);

        Assert.Equal(4, mainVm.SelectedTabIndex);
        Assert.Equal("LLM", mainVm.HardwareFit.SelectedModality);
        Assert.Equal("Llama 3.3 70B", mainVm.HardwareFit.SelectedPreset);
    }
}
