using System;
using System.Threading.Tasks;

namespace LocalLLMServerManager.Services;

public record GpuTelemetryResult(
    string GpuName,
    long TotalVramMb,
    long UsedVramMb,
    long FreeVramMb,
    double UtilizationPercent
);

public interface IGpuTelemetryProvider
{
    (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfo();
    (string GpuName, long TotalVramBytes, long UsedVramBytes)? GetLinuxMemoryInfo();
    (string GpuName, long TotalVramBytes, long UsedVramBytes)? ParseNvidiaSmiOutput(string output);
    (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfoFromRegistry();

    Task<GpuTelemetryResult> GetTelemetryAsync()
    {
        var (gpuName, totalBytes, usedBytes) = GetGpuInfo();
        var totalMb = totalBytes / (1024 * 1024);
        var usedMb = usedBytes / (1024 * 1024);
        var freeMb = Math.Max(0, (totalBytes - usedBytes) / (1024 * 1024));
        var utilPercent = totalBytes > 0 ? Math.Round((double)usedBytes / totalBytes * 100.0, 1) : 0.0;
        return Task.FromResult(new GpuTelemetryResult(gpuName, totalMb, usedMb, freeMb, utilPercent));
    }
}
