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

public record SystemMemorySnapshot(
    long TotalRamBytes,
    long UsedRamBytes,
    long AvailableRamBytes,
    int RamUsedPercent,
    long ProcessWorkingSetBytes,
    long NonPagedPoolBytes
);

public interface IGpuTelemetryProvider
{
    (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfo();
    (string GpuName, long TotalVramBytes, long UsedVramBytes)? GetLinuxMemoryInfo();
    (string GpuName, long TotalVramBytes, long UsedVramBytes)? ParseNvidiaSmiOutput(string output);
    (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfoFromRegistry();

    SystemMemorySnapshot GetSystemMemoryInfo()
    {
        var gcInfo = GC.GetGCMemoryInfo();
        long totalBytes = gcInfo.TotalAvailableMemoryBytes > 0 ? gcInfo.TotalAvailableMemoryBytes : 32L * 1024 * 1024 * 1024;
        long usedBytes = gcInfo.MemoryLoadBytes > 0 ? gcInfo.MemoryLoadBytes : totalBytes / 2;
        long availBytes = Math.Max(0, totalBytes - usedBytes);
        int pct = totalBytes > 0 ? (int)Math.Clamp(Math.Round((double)usedBytes / totalBytes * 100.0), 0, 100) : 0;
        return new SystemMemorySnapshot(totalBytes, usedBytes, availBytes, pct, Environment.WorkingSet, 0);
    }

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
