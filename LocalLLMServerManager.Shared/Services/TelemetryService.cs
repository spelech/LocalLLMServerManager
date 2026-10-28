using System;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Interfaces;

namespace LocalLLMServerManager.Shared.Services;

public class TelemetryService : ITelemetryService
{
    public async Task<GpuTelemetryInfo> QueryGpuVramAsync(string apiBase, HttpClient http)
    {
        try
        {
            var resp = await http.GetAsync($"{apiBase}/api/gpu/vram");
            if (resp.IsSuccessStatusCode)
            {
                var doc = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
                string gpuName = doc?["gpuName"]?.ToString() 
                    ?? doc?["GpuName"]?.ToString() 
                    ?? "GPU Telemetry Active";

                double totalVram = TryGetDouble(doc, "totalVramGb", "TotalVramGb") 
                    ?? TryGetDouble(doc, "vramGB", "VramGB") 
                    ?? (TryGetDouble(doc, "vramBytes", "VramBytes") is double vb ? Math.Round(vb / (1024.0 * 1024.0 * 1024.0), 2) : 16.0);

                double usedVram = TryGetDouble(doc, "usedVramGb", "UsedVramGb") 
                    ?? (TryGetDouble(doc, "usedVramBytes", "UsedVramBytes") is double uvb ? Math.Round(uvb / (1024.0 * 1024.0 * 1024.0), 2) : 0.0);

                double freeVram = TryGetDouble(doc, "freeVramGb", "FreeVramGb") 
                    ?? Math.Max(0.0, Math.Round(totalVram - usedVram, 2));

                int pct = TryGetInt(doc, "usedPercent", "UsedPercent") 
                    ?? (int)Math.Round((usedVram / (totalVram > 0 ? totalVram : 1.0)) * 100.0);

                double totalRam = TryGetDouble(doc, "totalRamGb", "TotalRamGb") ?? 32.0;
                double usedRam = TryGetDouble(doc, "usedRamGb", "UsedRamGb") ?? 0.0;
                int ramPct = TryGetInt(doc, "ramUsedPercent", "RamUsedPercent") 
                    ?? (int)Math.Round((usedRam / (totalRam > 0 ? totalRam : 1.0)) * 100.0);

                double processWs = TryGetDouble(doc, "processWorkingSetMb", "ProcessWorkingSetMb") ?? 0.0;
                double nonPaged = TryGetDouble(doc, "nonPagedPoolMb", "NonPagedPoolMb") ?? 0.0;
                int modelCacheTimeout = TryGetInt(doc, "modelCacheIdleTimeoutMinutes", "ModelCacheIdleTimeoutMinutes") ?? 15;
                int hibernationTimeout = TryGetInt(doc, "engineHibernationTimeoutMinutes", "EngineHibernationTimeoutMinutes") ?? 30;
                int ramPressureThreshold = TryGetInt(doc, "ramPressureThresholdPercent", "RamPressureThresholdPercent") ?? 75;

                return new GpuTelemetryInfo(
                    gpuName,
                    usedVram,
                    totalVram,
                    freeVram,
                    pct,
                    usedRam,
                    totalRam,
                    ramPct,
                    processWs,
                    nonPaged,
                    modelCacheTimeout,
                    hibernationTimeout,
                    ramPressureThreshold
                );
            }
        }
        catch { }

        return new GpuTelemetryInfo("GPU Telemetry Active", 0.0, 16.0, 16.0, 0, 0.0, 32.0, 0, 0.0, 0.0, 15, 30, 75);
    }

    private static double? TryGetDouble(JsonNode? node, string camel, string pascal)
    {
        var target = node?[camel] ?? node?[pascal];
        if (target == null) return null;
        try { return target.GetValue<double>(); } catch { return null; }
    }

    private static int? TryGetInt(JsonNode? node, string camel, string pascal)
    {
        var target = node?[camel] ?? node?[pascal];
        if (target == null) return null;
        try { return target.GetValue<int>(); } catch { return null; }
    }

    public async Task<(bool Ollama, bool Forge, bool ComfyUi)> CheckServiceHealthAsync(string apiBase, string comfyUrl, HttpClient http)
    {
        bool ollama = false;
        bool forge = false;
        bool comfy = false;

        if (OperatingSystem.IsBrowser())
        {
            try
            {
                var healthResp = await http.GetAsync($"{apiBase}/health");
                if (healthResp.IsSuccessStatusCode)
                {
                    var doc = JsonNode.Parse(await healthResp.Content.ReadAsStringAsync());
                    ollama = (doc?["ollama"]?.ToString() ?? doc?["Ollama"]?.ToString()) == "Online";
                    forge = (doc?["stableDiffusion"]?.ToString() ?? doc?["StableDiffusion"]?.ToString()) == "Online";
                    comfy = (doc?["comfyUI"]?.ToString() ?? doc?["ComfyUI"]?.ToString()) == "Online";
                }
            }
            catch { }
            return (ollama, forge, comfy);
        }

        try
        {
            var healthResp = await http.GetAsync($"{apiBase}/health");
            if (healthResp.IsSuccessStatusCode)
            {
                var doc = JsonNode.Parse(await healthResp.Content.ReadAsStringAsync());
                ollama = (doc?["ollama"]?.ToString() ?? doc?["Ollama"]?.ToString()) == "Online";
                forge = (doc?["stableDiffusion"]?.ToString() ?? doc?["StableDiffusion"]?.ToString()) == "Online";
                comfy = (doc?["comfyUI"]?.ToString() ?? doc?["ComfyUI"]?.ToString()) == "Online";
                return (ollama, forge, comfy);
            }
        }
        catch { }

        try { var r = await http.GetAsync("http://127.0.0.1:11434/"); ollama = r.IsSuccessStatusCode; } catch { }
        try { var r = await http.GetAsync("http://127.0.0.1:7860/"); forge = r.IsSuccessStatusCode; } catch { }
        try { var r = await http.GetAsync($"{comfyUrl.TrimEnd('/')}/system_stats"); comfy = r.IsSuccessStatusCode; } catch { }

        return (ollama, forge, comfy);
    }
}
