namespace LocalLLMServerManager.Services;

public interface IGpuTelemetryProvider
{
    (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfo();
    (string GpuName, long TotalVramBytes, long UsedVramBytes)? GetLinuxMemoryInfo();
    (string GpuName, long TotalVramBytes, long UsedVramBytes)? ParseNvidiaSmiOutput(string output);
    (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfoFromRegistry();
}
