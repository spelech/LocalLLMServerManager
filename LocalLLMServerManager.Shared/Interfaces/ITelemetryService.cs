using System.Threading.Tasks;

namespace LocalLLMServerManager.Shared.Interfaces;

public record GpuTelemetryInfo(
    string GpuName,
    double UsedVramGb,
    double TotalVramGb,
    double FreeVramGb,
    int Percent
);

public interface ITelemetryService
{
    Task<GpuTelemetryInfo> QueryGpuVramAsync(string apiBase, HttpClient http);
    Task<(bool Ollama, bool Forge, bool ComfyUi)> CheckServiceHealthAsync(string apiBase, string comfyUrl, HttpClient http);
}
