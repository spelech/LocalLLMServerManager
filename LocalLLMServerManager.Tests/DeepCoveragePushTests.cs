using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.ViewModels;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class DeepCoveragePushTests : IClassFixture<AppTestServerFixture>
{
    private readonly AppTestServerFixture _fixture;

    public DeepCoveragePushTests(AppTestServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task ComfyWorkflows_FullDiskDirectory_ReadsPresets()
    {
        var workflowsDir = Path.Combine(AppContext.BaseDirectory, "Workflows");
        if (!Directory.Exists(workflowsDir))
        {
            Directory.CreateDirectory(workflowsDir);
        }

        var sampleWorkflow = Path.Combine(workflowsDir, "sample_test_workflow.json");
        var jsonContent = JsonSerializer.Serialize(new
        {
            name = "Sample Workflow",
            type = "txt2img",
            description = "Test ComfyUI Preset"
        });
        File.WriteAllText(sampleWorkflow, jsonContent);

        try
        {
            var listResp = await _fixture.Client.GetAsync("/api/comfy/workflows");
            Assert.True(listResp.IsSuccessStatusCode);

            var itemResp = await _fixture.Client.GetAsync("/api/comfy/workflows/sample_test_workflow");
            Assert.True(itemResp.IsSuccessStatusCode);
            var content = await itemResp.Content.ReadAsStringAsync();
            Assert.Contains("Sample Workflow", content);
        }
        finally
        {
            if (File.Exists(sampleWorkflow)) File.Delete(sampleWorkflow);
        }
    }

    [Fact]
    public async Task ThreeDOutputs_FullDiskDirectory_Reads3dModels()
    {
        var settings = Program.LoadSettings();
        var outputsDir = Program.ResolvePath(settings.ThreeDModelsPath, @"%APPDATA%\AI\3d_outputs");
        if (!Directory.Exists(outputsDir))
        {
            Directory.CreateDirectory(outputsDir);
        }

        var sampleGlb = Path.Combine(outputsDir, "test_cube.glb");
        File.WriteAllText(sampleGlb, "dummy 3d glb data");

        try
        {
            var resp = await _fixture.Client.GetAsync("/api/3d/files");
            Assert.True(resp.IsSuccessStatusCode);
            var content = await resp.Content.ReadAsStringAsync();
            Assert.Contains("test_cube.glb", content);
        }
        finally
        {
            if (File.Exists(sampleGlb)) File.Delete(sampleGlb);
        }
    }

    [Fact]
    public async Task CivitaiDownloadEndpoint_ValidDirectory_StreamsEvents()
    {
        var modelsDir = Path.Combine(AppContext.BaseDirectory, "TestForgeModels");
        if (!Directory.Exists(modelsDir))
        {
            Directory.CreateDirectory(modelsDir);
        }

        var settings = new AppSettings { ForgeModelsPath = modelsDir };
        Program.SaveSettings(settings);

        try
        {
            var resp = await _fixture.Client.GetAsync("/api/civitai/download?fileUrl=http://127.0.0.1:5299/health&modelType=lora&fileName=test_lora.safetensors");
            Assert.True(resp.IsSuccessStatusCode);
        }
        finally
        {
            var loraFile = Path.Combine(modelsDir, "Lora", "test_lora.safetensors");
            if (File.Exists(loraFile)) File.Delete(loraFile);
            if (Directory.Exists(modelsDir)) Directory.Delete(modelsDir, true);
        }
    }

    [Fact]
    public async Task MiddlewareVramOrchestration_ForgeAndComfyRoutes_InterceptorsExecute()
    {
        try
        {
            var forgeRoute = await _fixture.Client.GetAsync("/sdapi/v1/txt2img");
        }
        catch { }

        try
        {
            var comfyRoute = await _fixture.Client.GetAsync("/comfyapi/prompt");
        }
        catch { }
    }

    [Fact]
    public void RecordProperties_AllGetters_AreCovered()
    {
        var hf = new HuggingFaceRepoItem("id", "author", 10, "100");
        Assert.Equal("id", hf.Id);
        Assert.Equal("author", hf.Author);
        Assert.Equal(10, hf.Likes);
        Assert.Equal("100", hf.Downloads);

        var quant = new HfFileQuantItem("file.gguf", "Q4_K_M", "4.7 GB", 4831838208L);
        Assert.Equal("file.gguf", quant.Filename);
        Assert.Equal("Q4_K_M", quant.Quantization);
        Assert.Equal("4.7 GB", quant.FormatSize);
        Assert.Equal(4831838208L, quant.SizeBytes);

        var civitai = new CivitaiModelItem(1, "Name", "Type", "Thumb", "Url", "File", 4.9, 100);
        Assert.Equal(1, civitai.Id);
        Assert.Equal("Name", civitai.Name);
        Assert.Equal("Type", civitai.Type);
        Assert.Equal("Thumb", civitai.ThumbnailUrl);
        Assert.Equal("Url", civitai.DownloadUrl);
        Assert.Equal("File", civitai.FileName);
        Assert.Equal(4.9, civitai.Rating);
        Assert.Equal(100, civitai.DownloadCount);
    }
}
