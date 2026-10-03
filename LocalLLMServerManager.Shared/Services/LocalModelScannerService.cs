using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.Services;

public class LocalModelScannerService : ILocalModelScannerService
{
    private readonly object _lock = new();
    private List<LocalModelItem> _cachedModels = new();

    public IReadOnlyList<LocalModelItem> GetCachedModels()
    {
        lock (_lock)
        {
            return _cachedModels.ToList();
        }
    }

    public async Task<IReadOnlyList<LocalModelItem>> ScanAllModelsAsync(AppSettings? settings = null, string? baseDirectory = null)
    {
        var baseDir = !string.IsNullOrWhiteSpace(baseDirectory) ? baseDirectory : AppContext.BaseDirectory;
        var results = new List<LocalModelItem>();
        var scannedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Image Checkpoints (Directory scan)
        var checkpointDirs = new List<(string Path, string Label)>
        {
            (Path.Combine(baseDir, "models", "checkpoints"), "Local Checkpoints"),
            (Path.Combine(baseDir, "ComfyUI", "models", "checkpoints"), "ComfyUI Checkpoints"),
            (@"D:\AI\models\checkpoints", "Local AI Checkpoints"),
            (@"C:\AI\models\checkpoints", "Local AI Checkpoints"),
            (@"D:\models\checkpoints", "Local Checkpoints"),
            (@"C:\models\checkpoints", "Local Checkpoints")
        };
        if (!string.IsNullOrWhiteSpace(settings?.ForgeModelsPath))
        {
            checkpointDirs.Add((settings.ForgeModelsPath, "Forge Checkpoints"));
        }
        if (!string.IsNullOrWhiteSpace(settings?.ComfyModelsPath))
        {
            checkpointDirs.Add((settings.ComfyModelsPath, "ComfyUI Checkpoints"));
        }

        await Task.Run(() =>
        {
            foreach (var (dir, label) in checkpointDirs)
            {
                ScanDirectoryFiles(dir, label, LocalModelCategory.ImageCheckpoint, new[] { ".safetensors", ".ckpt" }, results, scannedPaths);
            }

            // 2. Image LoRAs
            var loraDirs = new List<(string Path, string Label)>
            {
                (Path.Combine(baseDir, "models", "Lora"), "Local LoRAs"),
                (Path.Combine(baseDir, "ComfyUI", "models", "loras"), "ComfyUI LoRAs"),
                (@"D:\AI\models\Lora", "Local LoRAs"),
                (@"C:\AI\models\Lora", "Local LoRAs")
            };
            foreach (var (dir, label) in loraDirs)
            {
                ScanDirectoryFiles(dir, label, LocalModelCategory.ImageLora, new[] { ".safetensors", ".ckpt" }, results, scannedPaths);
            }

            // 3. Video Models & Workflows
            var videoDirs = new List<(string Path, string Label)>
            {
                (Path.Combine(baseDir, "ComfyUI", "models", "diffusion_models"), "ComfyUI Diffusion Models"),
                (Path.Combine(baseDir, "Workflows", "Video"), "Video Workflows")
            };
            if (!string.IsNullOrWhiteSpace(settings?.VideoModelsPath))
            {
                videoDirs.Add((settings.VideoModelsPath, "Configured Video Models"));
            }
            foreach (var (dir, label) in videoDirs)
            {
                ScanDirectoryFiles(dir, label, LocalModelCategory.Video, new[] { ".safetensors", ".json" }, results, scannedPaths);
            }

            // 4. Audio Models & Workflows
            var audioDirs = new List<(string Path, string Label)>
            {
                (Path.Combine(baseDir, "models", "tts"), "Local TTS Models"),
                (Path.Combine(baseDir, "audio", "stt"), "STT Whisper Models"),
                (Path.Combine(baseDir, "Workflows", "Audio"), "Audio Workflows")
            };
            if (!string.IsNullOrWhiteSpace(settings?.AudioPath))
            {
                audioDirs.Add((settings.AudioPath, "Configured Audio Models"));
            }
            foreach (var (dir, label) in audioDirs)
            {
                ScanDirectoryFiles(dir, label, LocalModelCategory.Audio, new[] { ".safetensors", ".onnx", ".bin", ".json" }, results, scannedPaths);
            }

            // 5. 3D Mesh Pipelines & Models
            var threeDDirs = new List<(string Path, string Label)>
            {
                (Path.Combine(baseDir, "models", "3d"), "3D Models"),
                (Path.Combine(baseDir, "Workflows"), "3D Workflows")
            };
            if (!string.IsNullOrWhiteSpace(settings?.ThreeDModelsPath))
            {
                threeDDirs.Add((settings.ThreeDModelsPath, "Configured 3D Models"));
            }
            foreach (var (dir, label) in threeDDirs)
            {
                ScanDirectoryFiles(dir, label, LocalModelCategory.ThreeD, new[] { ".safetensors", ".json" }, results, scannedPaths, fileFilter: f =>
                {
                    var lower = Path.GetFileName(f).ToLowerInvariant();
                    return lower.Contains("3d") || lower.Contains("trellis") || lower.Contains("hunyuan3d");
                });
            }
        });

        // 6. Live WebUI Forge Checkpoints & LoRAs API Discovery
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var forgeModelsResp = await http.GetAsync("http://127.0.0.1:7860/sdapi/v1/sd-models");
            if (forgeModelsResp.IsSuccessStatusCode)
            {
                var json = await forgeModelsResp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        var title = elem.TryGetProperty("title", out var tp) ? tp.GetString() : null;
                        var modelName = elem.TryGetProperty("model_name", out var mnp) ? mnp.GetString() : null;
                        var filename = elem.TryGetProperty("filename", out var fnp) ? fnp.GetString() : null;

                        var name = !string.IsNullOrWhiteSpace(modelName) ? modelName : (!string.IsNullOrWhiteSpace(title) ? title : "Forge Checkpoint");
                        var fullPath = !string.IsNullOrWhiteSpace(filename) ? filename : name;

                        if (!scannedPaths.Add(fullPath)) continue;

                        long sizeBytes = 0;
                        if (!string.IsNullOrWhiteSpace(filename) && File.Exists(filename))
                        {
                            try { sizeBytes = new FileInfo(filename).Length; } catch { }
                        }

                        var isVideo = name.Contains("svd", StringComparison.OrdinalIgnoreCase);
                        var cat = isVideo ? LocalModelCategory.Video : LocalModelCategory.ImageCheckpoint;

                        results.Add(new LocalModelItem(
                            Id: fullPath,
                            Name: CleanDisplayName(name),
                            FileName: Path.GetFileName(fullPath),
                            FullPath: fullPath,
                            Category: cat,
                            Architecture: DetectArchitecture(name, cat),
                            SizeBytes: sizeBytes,
                            FormattedSize: sizeBytes > 0 ? FormatBytes(sizeBytes) : "Forge Live",
                            SourceLocation: "WebUI Forge (Active API)",
                            CreatedAt: DateTime.Now
                        ));

                        if (!string.IsNullOrWhiteSpace(filename) && File.Exists(filename))
                        {
                            var dir = Path.GetDirectoryName(filename);
                            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                            {
                                ScanDirectoryFiles(dir, "Forge Discovered Directory", LocalModelCategory.ImageCheckpoint, new[] { ".safetensors", ".ckpt" }, results, scannedPaths);
                            }
                        }
                    }
                }
            }

            var forgeLorasResp = await http.GetAsync("http://127.0.0.1:7860/sdapi/v1/loras");
            if (forgeLorasResp.IsSuccessStatusCode)
            {
                var json = await forgeLorasResp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in doc.RootElement.EnumerateArray())
                    {
                        var name = elem.TryGetProperty("name", out var np) ? np.GetString() : null;
                        var path = elem.TryGetProperty("path", out var pp) ? pp.GetString() : null;
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        var fullPath = !string.IsNullOrWhiteSpace(path) ? path : name;
                        if (!scannedPaths.Add(fullPath)) continue;

                        long sizeBytes = 0;
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        {
                            try { sizeBytes = new FileInfo(path).Length; } catch { }
                        }

                        results.Add(new LocalModelItem(
                            Id: fullPath,
                            Name: CleanDisplayName(name),
                            FileName: Path.GetFileName(fullPath),
                            FullPath: fullPath,
                            Category: LocalModelCategory.ImageLora,
                            Architecture: "LoRA",
                            SizeBytes: sizeBytes,
                            FormattedSize: sizeBytes > 0 ? FormatBytes(sizeBytes) : "Forge LoRA",
                            SourceLocation: "WebUI Forge (Active API)",
                            CreatedAt: DateTime.Now
                        ));
                    }
                }
            }
        }
        catch
        {
            // Forge is either offline or unreachable, ignore safely
        }

        lock (_lock)
        {
            _cachedModels = results.OrderBy(m => m.Name).ToList();
        }

        return _cachedModels;
    }

    private static void ScanDirectoryFiles(
        string directoryPath,
        string sourceLabel,
        LocalModelCategory category,
        string[] extensions,
        List<LocalModelItem> results,
        HashSet<string> scannedPaths,
        Func<string, bool>? fileFilter = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            var dirInfo = new DirectoryInfo(directoryPath);
            var files = dirInfo.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                var ext = file.Extension.ToLowerInvariant();
                if (!extensions.Contains(ext)) continue;

                if (fileFilter != null && !fileFilter(file.FullName)) continue;

                if (!scannedPaths.Add(file.FullName)) continue;

                var nameWithoutExt = Path.GetFileNameWithoutExtension(file.Name);
                var architecture = DetectArchitecture(file.Name, category);
                var formattedSize = FormatBytes(file.Length);

                results.Add(new LocalModelItem(
                    Id: file.FullName,
                    Name: CleanDisplayName(nameWithoutExt),
                    FileName: file.Name,
                    FullPath: file.FullName,
                    Category: category,
                    Architecture: architecture,
                    SizeBytes: file.Length,
                    FormattedSize: formattedSize,
                    SourceLocation: sourceLabel,
                    CreatedAt: file.CreationTime
                ));
            }
        }
        catch
        {
            // Safeguard against OS permission issues or inaccessible directories
        }
    }

    public static string DetectArchitecture(string fileName, LocalModelCategory category)
    {
        var lower = fileName.ToLowerInvariant();

        if (category == LocalModelCategory.ImageCheckpoint || category == LocalModelCategory.ImageLora)
        {
            if (lower.Contains("flux")) return "Flux.1";
            if (lower.Contains("pony")) return "Pony XL";
            if (lower.Contains("illustrious")) return "Illustrious XL";
            if (lower.Contains("sdxl") || lower.Contains("xl_") || lower.Contains("-xl")) return "SDXL";
            if (lower.Contains("sd3") || lower.Contains("sd_3")) return "SD 3.5";
            if (lower.Contains("sd15") || lower.Contains("v1-5") || lower.Contains("1.5") || lower.Contains("realisticvision")) return "SD 1.5";
            return category == LocalModelCategory.ImageLora ? "LoRA" : "SDXL / General";
        }

        if (category == LocalModelCategory.Video)
        {
            if (lower.Contains("wan")) return "Wan 2.2";
            if (lower.Contains("ltx")) return "LTX-Video 2.5";
            if (lower.Contains("hunyuanvideo")) return "HunyuanVideo";
            if (lower.Contains("animatediff")) return "AnimateDiff";
            if (lower.Contains("svd")) return "Stable Video Diffusion";
            return "Video Pipeline";
        }

        if (category == LocalModelCategory.Audio)
        {
            if (lower.Contains("kokoro")) return "Kokoro TTS";
            if (lower.Contains("whisper")) return "Whisper STT";
            if (lower.Contains("musicgen")) return "MusicGen";
            if (lower.Contains("stable_audio")) return "Stable Audio Open";
            if (lower.Contains("yue")) return "YuE Full Song";
            return "Audio Engine";
        }

        if (category == LocalModelCategory.ThreeD)
        {
            if (lower.Contains("trellis")) return "TRELLIS V2";
            if (lower.Contains("hunyuan3d")) return "Hunyuan3D V2";
            return "3D Synthesis";
        }

        return "Local Model";
    }

    public static string CleanDisplayName(string rawName)
    {
        // Replace underscores/hyphens and clean common suffixes
        var clean = rawName
            .Replace("_api", "", StringComparison.OrdinalIgnoreCase)
            .Replace(".api", "", StringComparison.OrdinalIgnoreCase)
            .Replace('_', ' ');

        return clean.Trim();
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        var kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:F1} KB";
        var mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:F1} MB";
        var gb = mb / 1024.0;
        return $"{gb:F2} GB";
    }
}
