using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;

namespace LocalLLMServerManager.Tests;

public class WorkflowPerformanceTests
{
    private readonly ITestOutputHelper _output;

    public WorkflowPerformanceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Benchmark_WorkflowLoading_SyncVsAsyncConcurrent()
    {
        // 1. Setup temporary directory with 100 json files of substantial size (representative of ComfyUI workflows)
        var tempDir = Path.Combine(Path.GetTempPath(), "WorkflowPerformanceTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            int fileCount = 100;
            // Create some substantial dummy payload to simulate real workflows (e.g. 50 KB each)
            string largePayload = new string('x', 50 * 1024);

            for (int i = 0; i < fileCount; i++)
            {
                var filePath = Path.Combine(tempDir, $"workflow_{i:D3}.json");
                var data = new
                {
                    name = $"Workflow Preset {i}",
                    type = i % 2 == 0 ? "image" : "mesh",
                    description = $"Detailed description of workflow {i} to make the file size slightly larger.",
                    extra_data = largePayload
                };
                await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(data));
            }

            // Warm up
            LoadWorkflowsSync(tempDir);
            await LoadWorkflowsAsyncConcurrent(tempDir);

            // 2. Measure synchronous baseline loading
            var swSync = Stopwatch.StartNew();
            var syncResult = LoadWorkflowsSync(tempDir);
            swSync.Stop();
            var syncTimeMs = swSync.Elapsed.TotalMilliseconds;

            // 3. Measure asynchronous concurrent loading
            var swAsync = Stopwatch.StartNew();
            var asyncResult = await LoadWorkflowsAsyncConcurrent(tempDir);
            swAsync.Stop();
            var asyncTimeMs = swAsync.Elapsed.TotalMilliseconds;

            _output.WriteLine($"[BENCHMARK RESULT] Loading {fileCount} workflow files (each ~50KB):");
            _output.WriteLine($"Synchronous baseline: {syncTimeMs:F2} ms");
            _output.WriteLine($"Asynchronous concurrent: {asyncTimeMs:F2} ms");
            if (syncTimeMs > 0)
            {
                var speedup = syncTimeMs / asyncTimeMs;
                _output.WriteLine($"Speedup factor: {speedup:F2}x");
            }

            // 4. Verify correctness
            Assert.Equal(syncResult.Count, asyncResult.Count);
            for (int i = 0; i < syncResult.Count; i++)
            {
                var sItem = syncResult[i];
                var aItem = asyncResult[i];
                Assert.Equal(sItem.id, aItem.id);
                Assert.Equal(sItem.name, aItem.name);
                Assert.Equal(sItem.type, aItem.type);
                Assert.Equal(sItem.description, aItem.description);
                Assert.Equal(sItem.filePath, aItem.filePath);
            }
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private class WorkflowItem
    {
        public string id { get; set; } = "";
        public string name { get; set; } = "";
        public string type { get; set; } = "";
        public string description { get; set; } = "";
        public string filePath { get; set; } = "";
    }

    private List<WorkflowItem> LoadWorkflowsSync(string workflowsDir)
    {
        var result = new List<WorkflowItem>();

        if (Directory.Exists(workflowsDir))
        {
            var jsonFiles = Directory.GetFiles(workflowsDir, "*.json");
            foreach (var file in jsonFiles)
            {
                try
                {
                    var content = File.ReadAllText(file);
                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;

                    var name = root.TryGetProperty("name", out var n) ? n.GetString() : Path.GetFileNameWithoutExtension(file);
                    var type = root.TryGetProperty("type", out var t) ? t.GetString() : "general";
                    var description = root.TryGetProperty("description", out var d) ? d.GetString() : "";

                    result.Add(new WorkflowItem
                    {
                        id = Path.GetFileNameWithoutExtension(file),
                        name = name ?? "",
                        type = type ?? "general",
                        description = description ?? "",
                        filePath = file
                    });
                }
                catch { }
            }
        }

        return result;
    }

    private async Task<List<WorkflowItem>> LoadWorkflowsAsyncConcurrent(string workflowsDir)
    {
        if (!Directory.Exists(workflowsDir))
        {
            return new List<WorkflowItem>();
        }

        var jsonFiles = Directory.GetFiles(workflowsDir, "*.json");
        using var semaphore = new SemaphoreSlim(16);

        var tasks = jsonFiles.Select(async file =>
        {
            await semaphore.WaitAsync();
            try
            {
                // Let's read via stream or ReadAllTextAsync
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                using var doc = await JsonDocument.ParseAsync(stream);
                var root = doc.RootElement;

                var name = root.TryGetProperty("name", out var n) ? n.GetString() : Path.GetFileNameWithoutExtension(file);
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : "general";
                var description = root.TryGetProperty("description", out var d) ? d.GetString() : "";

                return new WorkflowItem
                {
                    id = Path.GetFileNameWithoutExtension(file),
                    name = name ?? "",
                    type = type ?? "general",
                    description = description ?? "",
                    filePath = file
                };
            }
            catch
            {
                return null;
            }
            finally
            {
                semaphore.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r != null).Cast<WorkflowItem>().ToList();
    }
}
