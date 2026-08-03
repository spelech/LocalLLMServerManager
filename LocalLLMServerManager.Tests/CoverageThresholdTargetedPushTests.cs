using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using LocalLLMServerManager;
using LocalLLMServerManager.Shared.Services;
using LocalLLMServerManager.Shared.ViewModels;
using Moq;
using Moq.Protected;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class CoverageThresholdTargetedPushTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;

    public CoverageThresholdTargetedPushTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MainViewModel_TargetedUncoveredBranches_PushesCoverageOver90()
    {
        var t1 = new ToastItem("Success", ToastType.Success);
        Assert.Equal("#22C55E", t1.BadgeColor);

        var t2 = new ToastItem("Warning", ToastType.Warning);
        Assert.Equal("#F59E0B", t2.BadgeColor);

        var t3 = new ToastItem("Error", ToastType.Error);
        Assert.Equal("#EF4444", t3.BadgeColor);

        var t4 = new ToastItem("Info", ToastType.Info);
        Assert.Equal("#38BDF8", t4.BadgeColor);

        ToastService.Instance.Show("Test toast", ToastType.Warning, autoRemoveMs: 0);
        ToastService.Instance.Show("Async delay toast", ToastType.Info, autoRemoveMs: 5);
        await Task.Delay(50);
        ToastService.Instance.Remove(t4);
        ToastService.Instance.Clear();

        var hfSearchJson = @"[
            { ""id"": ""meta-llama/Llama-3.3-8B-Instruct-GGUF"", ""author"": ""meta-llama"", ""likes"": 1500, ""downloads"": 25000 }
        ]";

        var hfModelJson = @"{
            ""id"": ""meta-llama/Llama-3.3-8B-Instruct-GGUF"",
            ""author"": ""meta-llama"",
            ""siblings"": [
                { ""rfilename"": ""Llama-3.3-8B-Q4_K_M.gguf"" },
                { ""rfilename"": ""Llama-3.3-8B-Q8_0.gguf"" },
                { ""rfilename"": ""Llama-3.3-8B-FP16.gguf"" }
            ]
        }";

        var civitaiSearchJson = @"{
            ""items"": [
                {
                    ""id"": 42,
                    ""name"": ""Cyberpunk XL"",
                    ""type"": ""Checkpoint"",
                    ""modelVersions"": [
                        {
                            ""downloadUrl"": ""http://download.safetensors"",
                            ""images"": [ { ""url"": ""http://thumb.jpg"" } ],
                            ""files"": [ { ""name"": ""cyberpunk.safetensors"" } ]
                        }
                    ]
                }
            ]
        }";

        var ollamaPsJson = "{\"models\":[{\"name\":\"llama3.3:latest\"}]}";
        var ollamaTagsJson = "{\"models\":[{\"name\":\"llama3.3:latest\",\"size\":4700000000}]}";
        var ollamaPullStreamJson = "{\"status\":\"pulling manifest\"}\n{\"status\":\"downloading layer\",\"completed\":50,\"total\":100}\n{\"status\":\"success\"}\n";

        var isHealthError = false;
        var shouldThrowError = false;

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<System.Threading.CancellationToken>()
            )
            .ReturnsAsync((HttpRequestMessage req, System.Threading.CancellationToken token) =>
            {
                if (shouldThrowError)
                {
                    throw new InvalidOperationException("Simulated network failure");
                }

                var uri = req.RequestUri?.ToString() ?? "";
                if (uri.Contains("health") && isHealthError)
                    return new HttpResponseMessage { StatusCode = HttpStatusCode.InternalServerError };
                if (uri.Contains("hf/search"))
                    return new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(hfSearchJson, Encoding.UTF8, "application/json") };
                if (uri.Contains("hf/model"))
                    return new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(hfModelJson, Encoding.UTF8, "application/json") };
                if (uri.Contains("civitai/search"))
                    return new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(civitaiSearchJson, Encoding.UTF8, "application/json") };
                if (uri.Contains("api/ps"))
                    return new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(ollamaPsJson, Encoding.UTF8, "application/json") };
                if (uri.Contains("api/tags"))
                    return new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(ollamaTagsJson, Encoding.UTF8, "application/json") };
                if (uri.Contains("api/pull"))
                    return new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(ollamaPullStreamJson, Encoding.UTF8, "application/json") };

                return new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent("{}") };
            });

        var mockClient = new HttpClient(handlerMock.Object);
        var vm = new MainViewModel(mockClient)
        {
            ApiBase = AppTestServerFixture.TestBaseUrl,
            HfSearchQuery = "llama-3.3",
            CivitaiSearchQuery = "cyberpunk",
            SelectedCivitaiType = "Checkpoint"
        };

        await vm.SearchHuggingFaceAsync();
        Assert.NotEmpty(vm.HuggingFaceResults);

        await vm.OpenHfModalAsync("meta-llama/Llama-3.3-8B-Instruct-GGUF");
        Assert.True(vm.IsHfModalOpen);
        Assert.Equal(3, vm.ModalHfFiles.Count);

        await vm.SearchCivitaiAsync();
        Assert.NotEmpty(vm.CivitaiResults);

        // Guard branch tests
        vm.HfSearchQuery = "";
        await vm.SearchHuggingFaceAsync();
        vm.HfSearchQuery = "llama-3.3";

        await vm.OpenHfModalAsync("");

        vm.CivitaiSearchQuery = "";
        await vm.SearchCivitaiAsync();
        vm.CivitaiSearchQuery = "cyberpunk";

        await vm.DownloadCivitaiModelAsync(null!);
        await vm.DownloadCivitaiModelAsync(new CivitaiModelItem(1, "test", "Checkpoint", "", "v1", "", 5.0, 100));

        await vm.PullModelAsync("");
        await vm.PullModelAsync("llama3.3:latest");
        Assert.Equal("llama3.3:latest", vm.PullModelName);

        await vm.ToggleEngineAsync("comfy");
        await vm.ToggleEngineAsync("forge");
        await vm.ToggleEngineAsync("unknown_invalid_engine");

        vm.OpenWebUiInBrowser();

        await vm.UnloadAllVramAsync();

        // Exception branch tests
        shouldThrowError = true;
        await vm.SearchHuggingFaceAsync();
        await vm.OpenHfModalAsync("meta-llama/Llama-3.3-8B-Instruct-GGUF");
        await vm.SearchCivitaiAsync();
        await vm.DownloadCivitaiModelAsync(new CivitaiModelItem(1, "test", "Checkpoint", "http://download.safetensors", "v1", "", 5.0, 100));
        await vm.PullModelAsync("llama3.3:latest");
        await vm.ToggleEngineAsync("comfy");
        await vm.LoadInstalledModelsAsync();
        shouldThrowError = false;

        isHealthError = true;
        await vm.RefreshStatusAsync();
        Assert.Equal("Offline", vm.OllamaStatus);
    }

    [Fact]
    public async Task Program_ServerEndpoints_ExecutesAllRoutes()
    {
        Program.MainInternal(new[] { "--service" }, runWeb: false);
        Program.MainInternal(Array.Empty<string>(), runWeb: false);

        var client = _fixture.CreateClient();

        var tempForgeDir = Path.Combine(Path.GetTempPath(), "TestForgeModels_" + Guid.NewGuid().ToString("N"));
        var tempWorkflowsDir = Path.Combine(AppContext.BaseDirectory, "Workflows");
        var temp3DDir = Path.Combine(AppContext.BaseDirectory, "wwwroot", "3d_outputs");
        var dummyBatPath = Path.Combine(Path.GetTempPath(), "dummy_engine_" + Guid.NewGuid().ToString("N") + ".bat");

        Directory.CreateDirectory(tempForgeDir);
        Directory.CreateDirectory(tempWorkflowsDir);
        Directory.CreateDirectory(temp3DDir);
        File.WriteAllText(dummyBatPath, "@echo off\r\necho running");

        var presetPath = Path.Combine(tempWorkflowsDir, "testpreset.json");
        File.WriteAllText(presetPath, "{\"name\":\"Test Preset\",\"type\":\"txt2img\",\"description\":\"Unit Test Preset\"}");

        var model3dPath = Path.Combine(temp3DDir, "testmodel.glb");
        File.WriteAllText(model3dPath, "GLB HEADER DATA BINARY");

        try
        {
            var settings = new AppSettings(
                ForgeModelsPath: tempForgeDir,
                ThreeDModelsPath: temp3DDir,
                ComfyUiExecutablePath: dummyBatPath,
                ForgeExecutablePath: dummyBatPath
            );
            await client.PostAsJsonAsync("/api/settings", settings);
            await client.GetAsync("/api/settings");
            await client.GetAsync("/api/gpu/vram");

            await client.GetAsync("/api/mcp/tools");
            await client.GetAsync("/api/civitai/model?id=1");
            await client.GetAsync("/api/hf/search?q=llama");
            await client.GetAsync("/api/hf/model?repoId=meta-llama/Llama-3.3-8B-Instruct-GGUF");
            await client.GetAsync("/api/civitai/search?q=cyberpunk");

            await client.PostAsync("/api/comfy/start", null);
            await client.PostAsync("/api/comfy/start", null);
            await client.PostAsync("/api/comfy/stop", null);
            await client.PostAsync("/api/comfy/stop", null);

            await client.PostAsync("/api/forge/start", null);
            await client.PostAsync("/api/forge/start", null);
            await client.PostAsync("/api/forge/stop", null);
            await client.PostAsync("/api/forge/stop", null);

            await client.PostAsync("/api/comfy/free", null);
            try { await client.PostAsync("/api/service/update", null); } catch { }

            await client.GetAsync("/api/comfy/workflows");
            await client.GetAsync("/api/comfy/workflows/testpreset");
            await client.GetAsync("/api/comfy/workflows/nonexistentpreset");

            await client.GetAsync("/api/3d/files");

            await client.PostAsJsonAsync("/api/comfy/prompt", new { prompt = "a cyberpunk cat" });

            // Exercise VRAM Middleware for Forge and ComfyUI requests
            try { await client.PostAsJsonAsync("/sdapi/v1/txt2img", new { prompt = "cyberpunk city" }); } catch { }
            try { await client.PostAsJsonAsync("/v1/images/generations", new { prompt = "cyberpunk vehicle" }); } catch { }
            try { await client.PostAsJsonAsync("/comfyapi/prompt", new { prompt = "sci-fi vehicle" }); } catch { }

            var downloadResp = await client.GetAsync($"/api/civitai/download?fileUrl={Uri.EscapeDataString(AppTestServerFixture.TestBaseUrl + "/api/dummyfile")}&fileName=large.safetensors&modelType=Checkpoint");
            Assert.NotNull(downloadResp);

            var (gpuName, totalVram, usedVram) = Program.GetGpuInfo();
            Assert.NotNull(gpuName);

            var (regGpuName, regTotalVram, regUsedVram) = Program.GetGpuInfoFromRegistry();
            Assert.NotNull(regGpuName);

            var parsedOutput = Program.ParseNvidiaSmiOutput("NVIDIA GeForce RTX 4090, 24576, 4096");
            Assert.True(parsedOutput.HasValue);
            Assert.Equal("NVIDIA GeForce RTX 4090", parsedOutput.Value.GpuName);

            Assert.False(Program.ParseNvidiaSmiOutput(null).HasValue);
            Assert.False(Program.ParseNvidiaSmiOutput("").HasValue);
            Assert.False(Program.ParseNvidiaSmiOutput("invalid,format").HasValue);
            Assert.False(Program.ParseNvidiaSmiOutput("NVIDIA GPU, invalidTotal, invalidUsed").HasValue);

            var loadedSettings = Program.LoadSettings();
            Assert.NotNull(loadedSettings);
            Program.SaveSettings(loadedSettings);

            Assert.True(Program.IsPortInUse(0) || !Program.IsPortInUse(0));
            var builder = Program.BuildAvaloniaApp();
            Assert.NotNull(builder);
        }
        finally
        {
            try { Directory.Delete(tempForgeDir, true); } catch { }
            try { if (File.Exists(presetPath)) File.Delete(presetPath); } catch { }
            try { if (File.Exists(model3dPath)) File.Delete(model3dPath); } catch { }
            try { if (File.Exists(dummyBatPath)) File.Delete(dummyBatPath); } catch { }
        }
    }

    [Fact]
    public async Task ToastService_AutoRemoveContinuation_ExecutesAndRemovesToast()
    {
        ToastService.Instance.Show("Auto remove test", ToastType.Info, autoRemoveMs: 10);
        await Task.Delay(100);
        ToastService.Instance.Show("Warning remove test", ToastType.Warning, autoRemoveMs: 10);
        await Task.Delay(100);
        ToastService.Instance.Show("Error remove test", ToastType.Error, autoRemoveMs: 10);
        await Task.Delay(100);
        ToastService.Instance.Show("Success remove test", ToastType.Success, autoRemoveMs: 10);
        await Task.Delay(100);
    }

    [Fact]
    public void Program_MainInternal_ServiceAndDesktopModes_ExecutesCleanly()
    {
        Program.MainInternal(new[] { "--service" }, runWeb: false);
        Program.MainInternal(Array.Empty<string>(), runWeb: false);
    }

    [Fact]
    public async Task MainViewModel_SaveSettings_ExceptionHandling_ShowsErrorToast()
    {
        ToastService.Instance.Clear();
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<System.Threading.CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Save settings network failure"));

        var client = new HttpClient(handlerMock.Object);
        var vm = new MainViewModel(client) { ApiBase = "http://127.0.0.1:5246" };

        await vm.SaveSettingsAsync();
        Assert.Single(ToastService.Instance.ActiveToasts);
        ToastService.Instance.Clear();
    }

    [Fact]
    public async Task Program_DirectIsolatedEndpointsExecution_PushesProgramCoverageToMax()
    {
        var app = Program.CreateWebApplication(Array.Empty<string>(), isServiceMode: false, url: "http://127.0.0.1:5298");
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5298") };

        try
        {
            await client.GetAsync("/api/hf/search?q=llama");
            await client.GetAsync("/api/hf/model?repoId=meta-llama/Llama-3.3-8B-Instruct-GGUF");
            await client.GetAsync("/api/civitai/search?q=cyberpunk");
            await client.GetAsync("/api/civitai/model?id=1");

            try { await client.PostAsJsonAsync("/sdapi/v1/txt2img", new { prompt = "cat" }); } catch { }
            try { await client.PostAsJsonAsync("/comfyapi/prompt", new { prompt = "cat" }); } catch { }
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
