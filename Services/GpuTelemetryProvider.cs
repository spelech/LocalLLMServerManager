using System.Diagnostics;

namespace LocalLLMServerManager.Services;

public class GpuTelemetryProvider : IGpuTelemetryProvider
{
    public (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfo()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=name,memory.total,memory.used --format=csv,noheader,nounits",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(psi);
            if (process != null)
            {
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                var parsed = ParseNvidiaSmiOutput(output);
                if (parsed.HasValue) return parsed.Value;
            }
        }
        catch { }

        if (OperatingSystem.IsLinux())
        {
            var linuxMem = GetLinuxMemoryInfo();
            if (linuxMem.HasValue) return linuxMem.Value;
        }

        return GetGpuInfoFromRegistry();
    }

    public (string GpuName, long TotalVramBytes, long UsedVramBytes)? GetLinuxMemoryInfo()
    {
        if (!OperatingSystem.IsLinux()) return null;
        try
        {
            if (File.Exists("/proc/meminfo"))
            {
                long totalKb = 0;
                long availableKb = 0;
                foreach (var line in File.ReadLines("/proc/meminfo"))
                {
                    if (line.StartsWith("MemTotal:"))
                    {
                        var parts = line.Split(':', StringSplitOptions.TrimEntries);
                        if (parts.Length >= 2)
                        {
                            var val = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                            long.TryParse(val, out totalKb);
                        }
                    }
                    else if (line.StartsWith("MemAvailable:"))
                    {
                        var parts = line.Split(':', StringSplitOptions.TrimEntries);
                        if (parts.Length >= 2)
                        {
                            var val = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                            long.TryParse(val, out availableKb);
                        }
                    }
                }
                if (totalKb > 0)
                {
                    long totalBytes = totalKb * 1024;
                    long usedBytes = Math.Max(0, (totalKb - availableKb) * 1024);
                    return ("Linux System Memory", totalBytes, usedBytes);
                }
            }
        }
        catch { }
        return null;
    }

    public (string GpuName, long TotalVramBytes, long UsedVramBytes)? ParseNvidiaSmiOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return null;
        var parts = output.Split(',');
        if (parts.Length >= 3)
        {
            string name = parts[0].Trim();
            if (long.TryParse(parts[1].Trim(), out long totalMb) &&
                long.TryParse(parts[2].Trim(), out long usedMb))
            {
                return (name, totalMb * 1024 * 1024, usedMb * 1024 * 1024);
            }
        }
        return null;
    }

    public (string GpuName, long TotalVramBytes, long UsedVramBytes) GetGpuInfoFromRegistry()
    {
        string bestGpuName = "Generic GPU";
        long bestVramBytes = 8L * 1024 * 1024 * 1024;
        int bestScore = -1;

        try
        {
            if (OperatingSystem.IsWindows())
            {
                const string regPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
                using var baseKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(regPath);
                if (baseKey != null)
                {
                    foreach (var subKeyName in baseKey.GetSubKeyNames())
                    {
                        if (subKeyName.Length == 4 && int.TryParse(subKeyName, out _))
                        {
                            try
                            {
                                using var subKey = baseKey.OpenSubKey(subKeyName);
                                if (subKey != null)
                                {
                                    var provider = subKey.GetValue("ProviderName")?.ToString() ?? "";
                                    var driverDesc = subKey.GetValue("DriverDesc")?.ToString() ?? "";

                                    if (driverDesc.Contains("Basic Render") ||
                                        (provider.Contains("Microsoft") && driverDesc.Contains("Indirect")) ||
                                        driverDesc.Contains("Virtual Desktop"))
                                    {
                                        continue;
                                    }

                                    int score = 1;
                                    if (driverDesc.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) ||
                                        provider.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                                    {
                                        score = 10;
                                    }
                                    else if (driverDesc.Contains("Radeon", StringComparison.OrdinalIgnoreCase) ||
                                             driverDesc.Contains("AMD", StringComparison.OrdinalIgnoreCase))
                                    {
                                        score = 5;
                                    }
                                    else if (driverDesc.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                                    {
                                        score = 0;
                                    }

                                    var qwMemSize = subKey.GetValue("HardwareInformation.qwMemorySize");
                                    long vram = 0;
                                    if (qwMemSize != null)
                                    {
                                        try { vram = Convert.ToInt64(qwMemSize); } catch { }
                                    }

                                    if (vram <= 0)
                                    {
                                        var dwMemSize = subKey.GetValue("HardwareInformation.MemorySize");
                                        if (dwMemSize != null)
                                        {
                                            try
                                            {
                                                byte[]? rawBytes = dwMemSize as byte[];
                                                if (rawBytes != null && rawBytes.Length >= 4)
                                                    vram = BitConverter.ToUInt32(rawBytes, 0);
                                                else
                                                    vram = Convert.ToInt64(dwMemSize);
                                            }
                                            catch { }
                                        }
                                    }

                                    if (score > bestScore || (score == bestScore && vram > bestVramBytes))
                                    {
                                        bestScore = score;
                                        bestGpuName = driverDesc;
                                        if (vram > 0) bestVramBytes = vram;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
        }
        catch { }

        return (bestGpuName, bestVramBytes, (long)(bestVramBytes * 0.25));
    }
}
