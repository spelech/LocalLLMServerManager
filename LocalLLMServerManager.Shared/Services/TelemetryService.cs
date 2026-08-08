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
                string gpuName = doc?["gpuName"]?.ToString() ?? "GPU Telemetry Active";
                double used = doc?["usedVramGb"]?.GetValue<double>() ?? 0.0;
                double total = doc?["totalVramGb"]?.GetValue<double>() ?? 16.0;
                double free = doc?["freeVramGb"]?.GetValue<double>() ?? (total - used);
                int pct = doc?["usedPercent"]?.GetValue<int>() ?? (int)Math.Round((used / (total > 0 ? total : 1.0)) * 100.0);

                return new GpuTelemetryInfo(gpuName, used, total, free, pct);
            }
        }
        catch { }

        return new GpuTelemetryInfo("GPU Telemetry Active", 0.0, 16.0, 16.0, 0);
    }

    public async Task<(bool Ollama, bool Forge, bool ComfyUi)> CheckServiceHealthAsync(string apiBase, string comfyUrl, HttpClient http)
    {
        bool ollama = false;
        bool forge = false;
        bool comfy = false;

        try
        {
            var healthResp = await http.GetAsync($"{apiBase}/health");
            if (healthResp.IsSuccessStatusCode)
            {
                var doc = JsonNode.Parse(await healthResp.Content.ReadAsStringAsync());
                ollama = doc?["ollama"]?.ToString() == "Online";
                forge = doc?["stableDiffusion"]?.ToString() == "Online";
                comfy = doc?["comfyUI"]?.ToString() == "Online";
                return (ollama, forge, comfy);
            }
            return (false, false, false);
        }
        catch { }

        try { var r = await http.GetAsync("http://127.0.0.1:11434/"); ollama = r.IsSuccessStatusCode; } catch { }
        try { var r = await http.GetAsync("http://127.0.0.1:7860/"); forge = r.IsSuccessStatusCode; } catch { }
        try { var r = await http.GetAsync($"{comfyUrl.TrimEnd('/')}/system_stats"); comfy = r.IsSuccessStatusCode; } catch { }

        return (ollama, forge, comfy);
    }
}
