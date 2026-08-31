using System;
using System.Text.Json;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Models;
using LocalLLMServerManager.Shared.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LocalLLMServerManager.Endpoints;

public static class HardwareEndpoints
{
    public static void MapHardwareEndpoints(this WebApplication app)
    {
        app.MapGet("/api/hardware/fit", (
            HttpContext httpContext,
            ICanIRunItService canIRunItService,
            IGpuTelemetryProvider telemetryProvider) =>
        {
            var query = httpContext.Request.Query;

            string modality = query.TryGetValue("modality", out var modVal) && !string.IsNullOrWhiteSpace(modVal)
                ? modVal.ToString().Trim().ToLowerInvariant()
                : "llm";

            double parameters = 8.0;
            if (query.TryGetValue("params", out var pVal) && !string.IsNullOrWhiteSpace(pVal))
            {
                if (double.TryParse(pVal, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsedP))
                {
                    parameters = Math.Max(0.1, parsedP);
                }
            }

            string quant = query.TryGetValue("quant", out var qVal) && !string.IsNullOrWhiteSpace(qVal)
                ? qVal.ToString().Trim()
                : "Q4_K_M";

            int context = 8192;
            if (query.TryGetValue("context", out var cVal) && !string.IsNullOrWhiteSpace(cVal))
            {
                if (int.TryParse(cVal, out var parsedC))
                {
                    context = Math.Max(512, parsedC);
                }
            }

            string kvPrec = query.TryGetValue("kv_prec", out var kvVal) && !string.IsNullOrWhiteSpace(kvVal)
                ? kvVal.ToString().Trim()
                : (query.TryGetValue("kvPrecision", out var kvpVal) && !string.IsNullOrWhiteSpace(kvpVal) ? kvpVal.ToString().Trim() : "FP16");

            string? modelName = query.TryGetValue("model_name", out var mnVal) && !string.IsNullOrWhiteSpace(mnVal)
                ? mnVal.ToString().Trim()
                : (query.TryGetValue("modelName", out var mnVal2) && !string.IsNullOrWhiteSpace(mnVal2) ? mnVal2.ToString().Trim() : null);

            long? sizeBytes = null;
            if (query.TryGetValue("size_bytes", out var sbVal) && long.TryParse(sbVal, out var parsedSb))
            {
                sizeBytes = parsedSb;
            }
            else if (query.TryGetValue("fileSizeBytes", out var fsbVal) && long.TryParse(fsbVal, out var parsedFsb))
            {
                sizeBytes = parsedFsb;
            }

            long vramMb = 0;
            if ((query.TryGetValue("vram_mb", out var vmbVal) && long.TryParse(vmbVal, out var parsedVmb)) ||
                (query.TryGetValue("vramMb", out var vmbVal2) && long.TryParse(vmbVal2, out parsedVmb)))
            {
                vramMb = parsedVmb;
            }

            if (vramMb <= 0)
            {
                var (gpuName, totalBytes, usedBytes) = telemetryProvider.GetGpuInfo();
                vramMb = totalBytes > 0 ? (totalBytes / (1024 * 1024)) : 16384;
            }

            long ramMb = 0;
            if ((query.TryGetValue("ram_mb", out var rmbVal) && long.TryParse(rmbVal, out var parsedRmb)) ||
                (query.TryGetValue("ramMb", out var rmbVal2) && long.TryParse(rmbVal2, out parsedRmb)))
            {
                ramMb = parsedRmb;
            }

            if (ramMb <= 0)
            {
                var totalRamBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                ramMb = totalRamBytes > 0 ? (totalRamBytes / (1024 * 1024)) : 32768;
            }

            if (modality.Contains("diffusion") || modality.Contains("image"))
            {
                var result = canIRunItService.EvaluateDiffusionFit(new DiffusionFitRequest(
                    ModelName: modelName ?? "Flux.1 Dev",
                    Quantization: quant,
                    Resolution: context > 0 && context <= 4096 ? context : 1024,
                    AvailableVramMb: vramMb,
                    AvailableRamMb: ramMb
                ));
                return Results.Ok(result);
            }

            if (modality.Contains("video"))
            {
                var result = canIRunItService.EvaluateVideoFit(new VideoFitRequest(
                    ModelName: modelName ?? "Wan 2.2 14B",
                    Quantization: quant,
                    FrameCount: 49,
                    Resolution: 720,
                    AvailableVramMb: vramMb,
                    AvailableRamMb: ramMb
                ));
                return Results.Ok(result);
            }

            if (modality.Contains("audio") || modality.Contains("speech"))
            {
                var result = canIRunItService.EvaluateAudioFit(modelName ?? "Kokoro", vramMb, ramMb);
                return Results.Ok(result);
            }

            if (modality.Contains("3d") || modality.Contains("threed"))
            {
                var result = canIRunItService.Evaluate3DFit(modelName ?? "TRELLIS", vramMb, ramMb);
                return Results.Ok(result);
            }

            if (modality.Contains("badge") || modality.Contains("quick"))
            {
                var result = canIRunItService.EvaluateQuickFit(modelName ?? "", sizeBytes, "llm", vramMb, ramMb);
                return Results.Ok(result);
            }

            // Default: LLM
            var llmResult = canIRunItService.EvaluateLlmFit(new LlmFitRequest(
                ParametersBillions: parameters,
                Quantization: quant,
                ContextLength: context,
                KvPrecision: kvPrec,
                AvailableVramMb: vramMb,
                AvailableRamMb: ramMb
            ));

            return Results.Ok(llmResult);
        });

        app.MapPost("/api/hardware/evaluate", async (
            HttpContext httpContext,
            ICanIRunItService canIRunItService,
            IGpuTelemetryProvider telemetryProvider) =>
        {
            if (!httpContext.Request.HasJsonContentType())
            {
                return Results.BadRequest(new { error = "Content-Type must be application/json." });
            }

            JsonDocument doc;
            try
            {
                doc = await JsonDocument.ParseAsync(httpContext.Request.Body, cancellationToken: httpContext.RequestAborted);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { error = $"Invalid JSON payload: {ex.Message}" });
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return Results.BadRequest(new { error = "Request payload must be a JSON object." });
                }

                var (gpuName, totalBytes, usedBytes) = telemetryProvider.GetGpuInfo();
                long defaultVramMb = totalBytes > 0 ? (totalBytes / (1024 * 1024)) : 16384;
                long defaultRamMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes > 0
                    ? (GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024))
                    : 32768;

                string modality = GetString(root, "modality")?.ToLowerInvariant() ?? "";
                string modelName = GetString(root, "modelName", "model_name", "model") ?? "";
                string engineName = GetString(root, "engineName", "engine_name", "engine") ?? "";
                string quant = GetString(root, "quantization", "quant") ?? "Q4_K_M";
                string kvPrec = GetString(root, "kvPrecision", "kv_precision", "kvPrec", "kv_prec") ?? "FP16";

                long availableVram = GetLong(root, "availableVramMb", "available_vram_mb", "vramMb", "vram_mb") ?? defaultVramMb;
                long availableRam = GetLong(root, "availableRamMb", "available_ram_mb", "ramMb", "ram_mb") ?? defaultRamMb;
                long? fileSizeBytes = GetLong(root, "fileSizeBytes", "file_size_bytes", "sizeBytes", "size_bytes");

                int resolution = GetInt(root, "resolution") ?? 1024;
                int frameCount = GetInt(root, "frameCount", "frame_count", "frames") ?? 49;
                int contextLength = GetInt(root, "contextLength", "context_length", "context") ?? 4096;
                double? paramBillions = GetDouble(root, "parametersBillions", "parameters_billions", "parameters", "params");
                int? totalLayers = GetInt(root, "totalLayers", "total_layers");
                int? kvHeads = GetInt(root, "kvHeads", "kv_heads");
                int? headDim = GetInt(root, "headDim", "head_dim");

                if (modality.Contains("diffusion") || modality.Contains("image") ||
                    (!string.IsNullOrWhiteSpace(modelName) && (modelName.Contains("flux", StringComparison.OrdinalIgnoreCase) || modelName.Contains("sdxl", StringComparison.OrdinalIgnoreCase) || modelName.Contains("stable", StringComparison.OrdinalIgnoreCase)) && !paramBillions.HasValue && !modality.Contains("video")))
                {
                    var result = canIRunItService.EvaluateDiffusionFit(new DiffusionFitRequest(
                        ModelName: string.IsNullOrWhiteSpace(modelName) ? "Flux.1 Dev" : modelName,
                        Quantization: quant,
                        Resolution: resolution,
                        AvailableVramMb: availableVram,
                        AvailableRamMb: availableRam
                    ));
                    return Results.Ok(result);
                }

                if (modality.Contains("video") || (GetInt(root, "frameCount", "frame_count", "frames").HasValue && !paramBillions.HasValue) ||
                    (!string.IsNullOrWhiteSpace(modelName) && (modelName.Contains("wan", StringComparison.OrdinalIgnoreCase) || modelName.Contains("ltx", StringComparison.OrdinalIgnoreCase) || modelName.Contains("hunyuanvideo", StringComparison.OrdinalIgnoreCase))))
                {
                    var result = canIRunItService.EvaluateVideoFit(new VideoFitRequest(
                        ModelName: string.IsNullOrWhiteSpace(modelName) ? "Wan 2.2 14B" : modelName,
                        Quantization: quant,
                        FrameCount: frameCount,
                        Resolution: resolution > 0 && resolution <= 1080 ? resolution : 720,
                        AvailableVramMb: availableVram,
                        AvailableRamMb: availableRam
                    ));
                    return Results.Ok(result);
                }

                if (modality.Contains("audio") || modality.Contains("speech") || !string.IsNullOrWhiteSpace(engineName))
                {
                    var targetEngine = !string.IsNullOrWhiteSpace(engineName) ? engineName : (!string.IsNullOrWhiteSpace(modelName) ? modelName : "Kokoro");
                    var result = canIRunItService.EvaluateAudioFit(targetEngine, availableVram, availableRam);
                    return Results.Ok(result);
                }

                if (modality.Contains("3d") || modality.Contains("threed") || (!string.IsNullOrWhiteSpace(modelName) && (modelName.Contains("trellis", StringComparison.OrdinalIgnoreCase) || modelName.Contains("hunyuan3d", StringComparison.OrdinalIgnoreCase))))
                {
                    var result = canIRunItService.Evaluate3DFit(string.IsNullOrWhiteSpace(modelName) ? "TRELLIS" : modelName, availableVram, availableRam);
                    return Results.Ok(result);
                }

                if (modality.Contains("badge") || modality.Contains("quick"))
                {
                    var result = canIRunItService.EvaluateQuickFit(modelName, fileSizeBytes, "llm", availableVram, availableRam);
                    return Results.Ok(result);
                }

                // Default: LLM Fit
                double finalParams = paramBillions.GetValueOrDefault(8.0);
                var llmResult = canIRunItService.EvaluateLlmFit(new LlmFitRequest(
                    ParametersBillions: finalParams,
                    Quantization: quant,
                    ContextLength: contextLength,
                    KvPrecision: kvPrec,
                    AvailableVramMb: availableVram,
                    AvailableRamMb: availableRam,
                    TotalLayers: totalLayers,
                    KvHeads: kvHeads,
                    HeadDim: headDim
                ));

                return Results.Ok(llmResult);
            }
        });
    }

    private static string? GetString(JsonElement root, params string[] propertyNames)
    {
        foreach (var prop in root.EnumerateObject())
        {
            foreach (var name in propertyNames)
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.ToString();
                }
            }
        }
        return null;
    }

    private static long? GetLong(JsonElement root, params string[] propertyNames)
    {
        foreach (var prop in root.EnumerateObject())
        {
            foreach (var name in propertyNames)
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt64(out var val))
                    {
                        return val;
                    }
                    if (long.TryParse(prop.Value.ToString(), out var parsed))
                    {
                        return parsed;
                    }
                }
            }
        }
        return null;
    }

    private static int? GetInt(JsonElement root, params string[] propertyNames)
    {
        foreach (var prop in root.EnumerateObject())
        {
            foreach (var name in propertyNames)
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var val))
                    {
                        return val;
                    }
                    if (int.TryParse(prop.Value.ToString(), out var parsed))
                    {
                        return parsed;
                    }
                }
            }
        }
        return null;
    }

    private static double? GetDouble(JsonElement root, params string[] propertyNames)
    {
        foreach (var prop in root.EnumerateObject())
        {
            foreach (var name in propertyNames)
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetDouble(out var val))
                    {
                        return val;
                    }
                    if (double.TryParse(prop.Value.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    {
                        return parsed;
                    }
                }
            }
        }
        return null;
    }
}
