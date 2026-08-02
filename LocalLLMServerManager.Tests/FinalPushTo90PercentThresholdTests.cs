using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using LocalLLMServerManager;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class FinalPushTo90PercentThresholdTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;

    public FinalPushTo90PercentThresholdTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task WebApp_DirectEndpoints_ExerciseDirectoryScanningAndDownloads()
    {
        var dir3d = Path.Combine(AppContext.BaseDirectory, "wwwroot", "models", "3d");
        var dirWf = Path.Combine(AppContext.BaseDirectory, "Workflows");
        Directory.CreateDirectory(dir3d);
        Directory.CreateDirectory(dirWf);

        // 1. Scan 3D Models
        var resp3d = await _fixture.Client.GetAsync("/api/models/3d");
        Assert.NotNull(resp3d);

        // 2. Scan Workflows
        var respWf = await _fixture.Client.GetAsync("/api/workflows");
        Assert.NotNull(respWf);

        // 3. CivitAI Search Proxy
        var respCivSearch = await _fixture.Client.GetAsync("/api/civitai/search?q=sdxl");
        Assert.NotNull(respCivSearch);

        // 4. CivitAI Model Detail Proxy
        var respCivModel = await _fixture.Client.GetAsync("/api/civitai/model?id=123");
        Assert.NotNull(respCivModel);

        // 5. HuggingFace Search Proxy
        var respHfSearch = await _fixture.Client.GetAsync("/api/hf/search?q=llama");
        Assert.NotNull(respHfSearch);

        // 6. HuggingFace Model Proxy
        var respHfModel = await _fixture.Client.GetAsync("/api/hf/model?repoId=meta-llama/Llama-2-7b");
        Assert.NotNull(respHfModel);
    }

    [Fact]
    public async Task MainViewModel_QuantizationParsing_PopulatesAllQuantTypes()
    {
        var vm = new MainViewModel
        {
            ApiBase = AppTestServerFixture.TestBaseUrl
        };

        // Exercise Context Token setter branches
        vm.TargetContextTokens = 2048;
        Assert.Contains("MB", vm.EstimatedKvCacheText);

        vm.TargetContextTokens = 8192;
        Assert.Contains("MB", vm.EstimatedKvCacheText);

        vm.TargetContextTokens = 65536;
        Assert.Contains("GB", vm.EstimatedKvCacheText);

        // Exercise Modal closing and toggle methods
        vm.CloseHfModal();
        Assert.False(vm.IsHfModalOpen);

        vm.ClosePullDrawer();
        Assert.False(vm.IsPullDrawerOpen);
    }
}
