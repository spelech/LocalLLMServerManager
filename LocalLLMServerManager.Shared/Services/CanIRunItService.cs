using System;
using System.Text.RegularExpressions;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.Services;

/// <summary>
/// Core mathematical sizing and performance estimation engine.
/// Evaluates memory footprints, GPU layer offloading, and generation speeds
/// across LLM, Image, Video, Audio, and 3D modalities.
/// </summary>
public class CanIRunItService : ICanIRunItService
{
    private const long CudaOverheadMb = 600;

    /// <summary>
    /// Returns the average bits per weight for a given quantization string.
    /// </summary>
    public static double GetBitsPerWeight(string? quant)
    {
        if (string.IsNullOrWhiteSpace(quant))
            return 4.50;

        var normalized = quant.Trim().ToUpperInvariant();
        return normalized switch
        {
            "Q2_K" or "Q2_K_S" or "Q2_K_L" or "Q2_0" => 2.65,
            "Q3_K_S" or "Q3_K" => 3.20,
            "Q3_K_M" => 3.50,
            "Q3_K_L" => 3.80,
            "Q4_0" or "Q4_1" or "Q4_K_M" or "Q4_K" or "Q4" => 4.50,
            "Q4_K_S" => 4.25,
            "Q5_0" or "Q5_1" or "Q5_K_M" or "Q5_K" or "Q5" => 5.50,
            "Q5_K_S" => 5.25,
            "Q6_K" or "Q6" => 6.60,
            "Q8_0" or "Q8_K" or "Q8_1" or "Q8" => 8.50,
            "FP8" or "E4M3" or "E5M2" => 8.00,
            "FP16" or "F16" or "BF16" => 16.00,
            "FP32" or "F32" => 32.00,
            _ when normalized.Contains("Q2") => 2.65,
            _ when normalized.Contains("Q3") => 3.50,
            _ when normalized.Contains("Q4") => 4.50,
            _ when normalized.Contains("Q5") => 5.50,
            _ when normalized.Contains("Q6") => 6.60,
            _ when normalized.Contains("Q8") => 8.50,
            _ when normalized.Contains("FP8") => 8.00,
            _ when normalized.Contains("16") => 16.00,
            _ when normalized.Contains("32") => 32.00,
            _ => 4.50
        };
    }

    /// <summary>
    /// Returns the bits per element for KV cache precision.
    /// </summary>
    public static double GetKvBitsPerElement(string? kvPrecision)
    {
        if (string.IsNullOrWhiteSpace(kvPrecision))
            return 16.0;

        var normalized = kvPrecision.Trim().ToUpperInvariant();
        return normalized switch
        {
            "Q4_0" or "Q4_K" or "Q4_1" or "Q4" or "INT4" => 4.0,
            "Q8_0" or "Q8_K" or "Q8_1" or "Q8" or "INT8" or "FP8" => 8.0,
            "FP16" or "F16" or "BF16" or "FLOAT16" => 16.0,
            _ when normalized.Contains("4") => 4.0,
            _ when normalized.Contains("8") => 8.0,
            _ => 16.0
        };
    }

    /// <summary>
    /// Heuristically determines standard transformer layer count, KV head count, and head dimension.
    /// </summary>
    public static (int Layers, int KvHeads, int HeadDim) GetDefaultArchitecture(double paramsBillions)
    {
        if (paramsBillions >= 600) // DeepSeek R1 / V3
            return (61, 64, 128);
        if (paramsBillions >= 65)  // Llama 70B
            return (80, 8, 128);
        if (paramsBillions >= 30)  // Qwen 32B
            return (64, 8, 128);
        if (paramsBillions >= 12)  // Qwen 14B / Yi 34B
            return (48, 8, 128);
        if (paramsBillions >= 6)   // Llama 8B / Qwen 7B
            return (32, 8, 128);
        if (paramsBillions >= 2)   // Llama 3B / Phi 3.5
            return (28, 4, 96);
        return (24, 4, 64);        // 1B - 1.5B
    }

    /// <inheritdoc />
    public LlmFitResult EvaluateLlmFit(LlmFitRequest request)
    {
        var arch = GetDefaultArchitecture(request.ParametersBillions);
        int totalLayers = request.TotalLayers.GetValueOrDefault() > 0 ? request.TotalLayers!.Value : arch.Layers;
        int kvHeads = request.KvHeads.GetValueOrDefault() > 0 ? request.KvHeads!.Value : arch.KvHeads;
        int headDim = request.HeadDim.GetValueOrDefault() > 0 ? request.HeadDim!.Value : arch.HeadDim;

        double bitsPerWeight = GetBitsPerWeight(request.Quantization);
        long modelWeightMb = (long)Math.Round(request.ParametersBillions * 1024.0 * (bitsPerWeight / 8.0) * 1.05);

        double kvBits = GetKvBitsPerElement(request.KvPrecision);
        double kvBytes = 2.0 * totalLayers * kvHeads * headDim * request.ContextLength * (kvBits / 8.0);
        long kvCacheMb = (long)Math.Round(kvBytes / (1024.0 * 1024.0));

        long totalNeededMb = modelWeightMb + kvCacheMb + CudaOverheadMb;

        long availableVram = Math.Max(0, request.AvailableVramMb);
        long availableRam = Math.Max(0, request.AvailableRamMb);

        int gpuLayers;
        int cpuLayers;
        long totalVramMb;
        long totalRamMb;
        FitVerdict verdict;

        long vramForWeights = Math.Max(0, availableVram - kvCacheMb - CudaOverheadMb);

        if (availableVram >= totalNeededMb)
        {
            gpuLayers = totalLayers;
            cpuLayers = 0;
            totalVramMb = totalNeededMb;
            totalRamMb = 0;
            verdict = FitVerdict.FullVram;
        }
        else
        {
            double weightPerLayer = (double)modelWeightMb / totalLayers;
            gpuLayers = Math.Min(totalLayers, Math.Max(0, (int)Math.Floor(vramForWeights / weightPerLayer)));
            cpuLayers = totalLayers - gpuLayers;

            if (gpuLayers > 0)
            {
                totalVramMb = kvCacheMb + CudaOverheadMb + (long)Math.Round(gpuLayers * weightPerLayer);
                totalRamMb = (long)Math.Round(cpuLayers * weightPerLayer);

                if (totalRamMb <= availableRam)
                {
                    verdict = FitVerdict.PartialOffload;
                }
                else
                {
                    verdict = FitVerdict.OutOfMemory;
                }
            }
            else
            {
                totalVramMb = 0;
                totalRamMb = modelWeightMb + kvCacheMb;

                if (totalRamMb <= availableRam)
                {
                    verdict = FitVerdict.CpuOnly;
                }
                else
                {
                    verdict = FitVerdict.OutOfMemory;
                }
            }
        }

        // Speed estimation (tok/sec)
        double weightGb = Math.Max(0.5, modelWeightMb / 1024.0);
        double gpuTokSpeed = 800.0 / weightGb;
        double cpuTokSpeed = 40.0 / weightGb;

        double estimatedTokPerSec = verdict switch
        {
            FitVerdict.FullVram => Math.Round(Math.Max(1.0, gpuTokSpeed), 1),
            FitVerdict.PartialOffload => Math.Round(Math.Max(0.5, cpuTokSpeed * (1.0 + 1.5 * ((double)gpuLayers / totalLayers))), 1),
            FitVerdict.CpuOnly => Math.Round(Math.Max(0.2, cpuTokSpeed), 1),
            _ => 0.0
        };

        string recommendation = verdict switch
        {
            FitVerdict.FullVram =>
                $"Runs 100% in GPU VRAM with full hardware acceleration. Excellent performance expected (~{estimatedTokPerSec} tok/s).",
            FitVerdict.PartialOffload =>
                $"Offloading {gpuLayers}/{totalLayers} layers to GPU ({cpuLayers} layers in RAM). Expect reduced generation speed (~{estimatedTokPerSec} tok/s). Consider a smaller quant or shorter context for full GPU fit.",
            FitVerdict.CpuOnly =>
                $"Insufficient VRAM to offload transformer layers. Model runs entirely on CPU/System RAM (~{estimatedTokPerSec} tok/s). Generation will be slow.",
            _ =>
                $"Insufficient combined memory to run this model ({totalVramMb + totalRamMb:N0} MB needed, {availableVram + availableRam:N0} MB available). Consider a smaller quantization or fewer parameters."
        };

        return new LlmFitResult(
            ModelWeightMb: modelWeightMb,
            KvCacheMb: kvCacheMb,
            OverheadMb: CudaOverheadMb,
            TotalVramMb: totalVramMb,
            TotalRamMb: totalRamMb,
            GpuLayers: gpuLayers,
            CpuLayers: cpuLayers,
            TotalLayers: totalLayers,
            FitVerdict: verdict,
            EstimatedTokPerSec: estimatedTokPerSec,
            RecommendationMessage: recommendation
        );
    }

    /// <inheritdoc />
    public DiffusionFitResult EvaluateDiffusionFit(DiffusionFitRequest request)
    {
        string name = (request.ModelName ?? "").ToLowerInvariant();
        string quant = (request.Quantization ?? "FP8").ToUpperInvariant();
        bool isFp16 = quant.Contains("16") || quant.Contains("F16");
        bool isQ4 = quant.Contains("Q4") || quant.Contains("4");

        long baseMb;
        long encMb;
        long vaeMb = 350;
        long latentMb;
        double baseSec;

        if (name.Contains("flux"))
        {
            baseMb = isFp16 ? 23800 : (isQ4 ? 7500 : 11900);
            encMb = isFp16 ? 9800 : (isQ4 ? 4000 : 4900);
            latentMb = (long)Math.Round(1200.0 * Math.Pow(request.Resolution / 1024.0, 2));
            baseSec = isFp16 ? 22.0 : (isQ4 ? 12.0 : 15.0);
        }
        else if (name.Contains("sdxl") || name.Contains("pony") || name.Contains("stable-diffusion-xl"))
        {
            baseMb = isFp16 ? 6600 : (isQ4 ? 2500 : 3500);
            encMb = isFp16 ? 2500 : (isQ4 ? 1000 : 1300);
            latentMb = (long)Math.Round(800.0 * Math.Pow(request.Resolution / 1024.0, 2));
            baseSec = 5.0;
        }
        else if (name.Contains("sd 3") || name.Contains("sd3"))
        {
            baseMb = isFp16 ? 16000 : (isQ4 ? 5500 : 8500);
            encMb = isFp16 ? 5000 : (isQ4 ? 2000 : 2500);
            latentMb = (long)Math.Round(1000.0 * Math.Pow(request.Resolution / 1024.0, 2));
            baseSec = 10.0;
        }
        else if (name.Contains("1.5") || name.Contains("sd-1-5") || name.Contains("realistic"))
        {
            baseMb = isFp16 ? 2000 : 1200;
            encMb = 500;
            latentMb = (long)Math.Round(400.0 * Math.Pow(request.Resolution / 512.0, 2));
            baseSec = 2.0;
        }
        else
        {
            baseMb = isFp16 ? 10000 : 6000;
            encMb = 2000;
            latentMb = (long)Math.Round(800.0 * Math.Pow(request.Resolution / 1024.0, 2));
            baseSec = 8.0;
        }

        long totalNeeded = baseMb + encMb + vaeMb + latentMb;
        long peakSamplingVram = baseMb + vaeMb + latentMb;
        long totalVramMb;
        long totalRamMb;
        FitVerdict verdict;

        if (totalNeeded <= request.AvailableVramMb)
        {
            verdict = FitVerdict.FullVram;
            totalVramMb = totalNeeded;
            totalRamMb = 0;
        }
        else if (peakSamplingVram <= request.AvailableVramMb && totalNeeded <= request.AvailableVramMb + request.AvailableRamMb)
        {
            // Sequential offload: Text encoders execute in RAM or sequentially, DiT executes in full VRAM
            verdict = FitVerdict.FullVram;
            totalVramMb = peakSamplingVram;
            totalRamMb = encMb;
        }
        else if (totalNeeded <= request.AvailableVramMb + request.AvailableRamMb)
        {
            verdict = FitVerdict.PartialOffload;
            totalVramMb = request.AvailableVramMb;
            totalRamMb = totalNeeded - request.AvailableVramMb;
        }
        else
        {
            verdict = FitVerdict.OutOfMemory;
            totalVramMb = totalNeeded;
            totalRamMb = totalNeeded;
        }

        double estSec = verdict switch
        {
            FitVerdict.FullVram => baseSec,
            FitVerdict.PartialOffload => baseSec * 3.5,
            _ => 0.0
        };

        string recommendation = verdict switch
        {
            FitVerdict.FullVram => $"Fits comfortably in GPU VRAM ({totalVramMb:N0} MB). Fast generation (~{estSec:F1}s/image).",
            FitVerdict.PartialOffload => $"Requires CPU offloading for text encoders ({totalRamMb:N0} MB in RAM). Generation will take ~{estSec:F1}s/image.",
            _ => $"Requires {totalNeeded:N0} MB memory which exceeds system capacity ({request.AvailableVramMb + request.AvailableRamMb:N0} MB)."
        };

        return new DiffusionFitResult(
            ModelName: request.ModelName,
            BaseModelMb: baseMb,
            EncodersMb: encMb,
            VaeMb: vaeMb,
            LatentBufferMb: latentMb,
            TotalVramMb: totalVramMb,
            TotalRamMb: totalRamMb,
            FitVerdict: verdict,
            EstimatedSecondsPerImage: estSec,
            RecommendationMessage: recommendation
        );
    }

    /// <inheritdoc />
    public VideoFitResult EvaluateVideoFit(VideoFitRequest request)
    {
        string name = (request.ModelName ?? "").ToLowerInvariant();
        string quant = (request.Quantization ?? "FP8").ToUpperInvariant();
        bool isFp16 = quant.Contains("16") || quant.Contains("F16");
        bool isQ4 = quant.Contains("Q4") || quant.Contains("4");

        long ditMb;
        long frameMb;
        long vaeMb;
        double baseSecPerFrame;

        if (name.Contains("wan") && (name.Contains("14b") || !name.Contains("1.3b")))
        {
            ditMb = isFp16 ? 28000 : (isQ4 ? 8500 : 14000);
            frameMb = (long)Math.Round(6000.0 * (request.FrameCount / 49.0) * Math.Pow(request.Resolution / 720.0, 2));
            vaeMb = 2500;
            baseSecPerFrame = 0.8;
        }
        else if (name.Contains("1.3b") || (name.Contains("wan") && name.Contains("1.3")))
        {
            ditMb = isFp16 ? 2800 : 1500;
            frameMb = (long)Math.Round(1500.0 * (request.FrameCount / 49.0) * Math.Pow(request.Resolution / 720.0, 2));
            vaeMb = 1000;
            baseSecPerFrame = 0.15;
        }
        else if (name.Contains("ltx"))
        {
            ditMb = isFp16 ? 4500 : 2400;
            frameMb = (long)Math.Round(2000.0 * (request.FrameCount / 49.0) * Math.Pow(request.Resolution / 720.0, 2));
            vaeMb = 1200;
            baseSecPerFrame = 0.10;
        }
        else if (name.Contains("hunyuan"))
        {
            ditMb = isFp16 ? 26000 : 13000;
            frameMb = (long)Math.Round(5000.0 * (request.FrameCount / 49.0) * Math.Pow(request.Resolution / 720.0, 2));
            vaeMb = 2500;
            baseSecPerFrame = 0.90;
        }
        else
        {
            ditMb = 10000;
            frameMb = (long)Math.Round(3000.0 * (request.FrameCount / 49.0) * Math.Pow(request.Resolution / 720.0, 2));
            vaeMb = 1500;
            baseSecPerFrame = 0.50;
        }

        long totalNeeded = ditMb + frameMb + vaeMb;
        long totalVramMb;
        long totalRamMb;
        FitVerdict verdict;

        if (totalNeeded <= request.AvailableVramMb)
        {
            verdict = FitVerdict.FullVram;
            totalVramMb = totalNeeded;
            totalRamMb = 0;
        }
        else if (totalNeeded <= request.AvailableVramMb + request.AvailableRamMb)
        {
            verdict = FitVerdict.PartialOffload;
            totalVramMb = request.AvailableVramMb;
            totalRamMb = totalNeeded - request.AvailableVramMb;
        }
        else
        {
            verdict = FitVerdict.OutOfMemory;
            totalVramMb = totalNeeded;
            totalRamMb = totalNeeded;
        }

        double estSec = verdict switch
        {
            FitVerdict.FullVram => baseSecPerFrame,
            FitVerdict.PartialOffload => baseSecPerFrame * 4.0,
            _ => 0.0
        };

        string recommendation = verdict switch
        {
            FitVerdict.FullVram => $"Video pipeline fits entirely in VRAM ({totalNeeded:N0} MB). Generation speed ~{estSec:F2}s per frame.",
            FitVerdict.PartialOffload => $"Requires offloading DiT/VAE stages to System RAM ({totalRamMb:N0} MB). Slower generation ~{estSec:F2}s per frame.",
            _ => $"Video generation workload requires {totalNeeded:N0} MB, exceeding total system capacity."
        };

        return new VideoFitResult(
            ModelName: request.ModelName,
            DiTModelMb: ditMb,
            FrameContextMb: frameMb,
            VaeDecodeMb: vaeMb,
            TotalVramMb: totalVramMb,
            TotalRamMb: totalRamMb,
            FitVerdict: verdict,
            EstimatedSecondsPerFrame: estSec,
            RecommendationMessage: recommendation
        );
    }

    /// <inheritdoc />
    public AudioFitResult EvaluateAudioFit(string engineName, long vramMb, long ramMb)
    {
        string name = (engineName ?? "").ToLowerInvariant();
        long vramReq;
        long ramReq;
        double rtFactor;
        string rec;

        if (name.Contains("kokoro"))
        {
            vramReq = 400;
            ramReq = 600;
            rtFactor = 45.0;
            rec = "Ultra-lightweight TTS model (~400MB VRAM). Runs effortlessly on almost any GPU or CPU.";
        }
        else if (name.Contains("whisper"))
        {
            vramReq = 2000;
            ramReq = 1500;
            rtFactor = 25.0;
            rec = "Whisper speech transcription engine (~2.0GB VRAM). Fast real-time transcribing.";
        }
        else if (name.Contains("xtts") || name.Contains("alltalk"))
        {
            vramReq = 2500;
            ramReq = 2000;
            rtFactor = 3.5;
            rec = "XTTS-v2 voice cloning engine (~2.5GB VRAM). High quality multi-lingual voice synthesis.";
        }
        else if (name.Contains("musicgen") || name.Contains("audiocraft"))
        {
            vramReq = 3200;
            ramReq = 2500;
            rtFactor = 1.2;
            rec = "MusicGen audio generation model (~3.2GB VRAM).";
        }
        else
        {
            vramReq = 1500;
            ramReq = 1500;
            rtFactor = 10.0;
            rec = "Standard audio model footprint.";
        }

        FitVerdict verdict;
        if (vramMb >= vramReq)
            verdict = FitVerdict.FullVram;
        else if (ramMb >= ramReq)
            verdict = FitVerdict.CpuOnly;
        else
            verdict = FitVerdict.OutOfMemory;

        return new AudioFitResult(
            EngineName: engineName,
            VramRequiredMb: vramReq,
            RamRequiredMb: ramReq,
            FitVerdict: verdict,
            EstimatedRealtimeFactor: rtFactor,
            RecommendationMessage: rec
        );
    }

    /// <inheritdoc />
    public ThreeDFitResult Evaluate3DFit(string modelName, long vramMb, long ramMb)
    {
        string name = (modelName ?? "").ToLowerInvariant();
        long vramReq;
        long ramReq;
        double secPerMesh;
        string rec;

        if (name.Contains("trellis"))
        {
            vramReq = 12000;
            ramReq = 8000;
            secPerMesh = 25.0;
            rec = "TRELLIS 3D mesh generation requires ~12GB VRAM for high-quality geometric latents.";
        }
        else if (name.Contains("hunyuan3d") || name.Contains("hunyuan-3d") || name.Contains("hunyuan"))
        {
            vramReq = 16000;
            ramReq = 12000;
            secPerMesh = 45.0;
            rec = "Hunyuan3D-2 requires ~16GB VRAM for full DiT 3D diffusion.";
        }
        else
        {
            vramReq = 10000;
            ramReq = 8000;
            secPerMesh = 30.0;
            rec = "Standard 3D mesh generative model footprint.";
        }

        FitVerdict verdict;
        if (vramMb >= vramReq)
            verdict = FitVerdict.FullVram;
        else if (vramMb + ramMb >= vramReq + ramReq)
            verdict = FitVerdict.PartialOffload;
        else
            verdict = FitVerdict.OutOfMemory;

        return new ThreeDFitResult(
            ModelName: modelName,
            VramRequiredMb: vramReq,
            RamRequiredMb: ramReq,
            FitVerdict: verdict,
            EstimatedSecondsPerMesh: secPerMesh,
            RecommendationMessage: rec
        );
    }

    /// <inheritdoc />
    public QuickFitBadge EvaluateQuickFit(string modelName, long? fileSizeBytes, string modality, long vramMb, long ramMb)
    {
        string mod = (modality ?? "").ToLowerInvariant();
        string name = (modelName ?? "").ToLowerInvariant();

        FitVerdict verdict;
        string tooltipDetail = "";

        if (mod.Contains("audio") || mod.Contains("speech") || mod.Contains("tts") || name.Contains("kokoro") || name.Contains("whisper"))
        {
            var audioResult = EvaluateAudioFit(modelName, vramMb, ramMb);
            verdict = audioResult.FitVerdict;
            tooltipDetail = $"{audioResult.VramRequiredMb} MB VRAM needed. {audioResult.RecommendationMessage}";
        }
        else if (mod.Contains("3d") || name.Contains("trellis") || name.Contains("hunyuan3d"))
        {
            var threeDResult = Evaluate3DFit(modelName, vramMb, ramMb);
            verdict = threeDResult.FitVerdict;
            tooltipDetail = $"{threeDResult.VramRequiredMb} MB VRAM needed. {threeDResult.RecommendationMessage}";
        }
        else if (mod.Contains("video") || name.Contains("wan") || name.Contains("ltx"))
        {
            var vidResult = EvaluateVideoFit(new VideoFitRequest(modelName, "FP8", 49, 720, vramMb, ramMb));
            verdict = vidResult.FitVerdict;
            tooltipDetail = $"{vidResult.TotalVramMb + vidResult.TotalRamMb} MB needed. {vidResult.RecommendationMessage}";
        }
        else if (mod.Contains("image") || mod.Contains("diffusion") || name.Contains("flux") || name.Contains("sdxl") || name.Contains("sd 3"))
        {
            var diffResult = EvaluateDiffusionFit(new DiffusionFitRequest(modelName, "FP8", 1024, vramMb, ramMb));
            verdict = diffResult.FitVerdict;
            tooltipDetail = $"{diffResult.TotalVramMb + diffResult.TotalRamMb} MB needed. {diffResult.RecommendationMessage}";
        }
        else
        {
            // Default: LLM / text generation
            if (fileSizeBytes.HasValue && fileSizeBytes.Value > 0)
            {
                long fileMb = fileSizeBytes.Value / (1024 * 1024);
                long totalNeeded = fileMb + 1024 + CudaOverheadMb;

                if (vramMb >= totalNeeded)
                    verdict = FitVerdict.FullVram;
                else if (vramMb + ramMb >= totalNeeded)
                    verdict = FitVerdict.PartialOffload;
                else
                    verdict = FitVerdict.OutOfMemory;

                tooltipDetail = $"{fileMb} MB file size. Total needed: ~{totalNeeded} MB (VRAM: {vramMb} MB, RAM: {ramMb} MB).";
            }
            else
            {
                double paramBillions = ExtractParamBillions(modelName);
                string quant = ExtractQuantization(modelName);

                var llmResult = EvaluateLlmFit(new LlmFitRequest(
                    ParametersBillions: paramBillions,
                    Quantization: quant,
                    ContextLength: 4096,
                    KvPrecision: "FP16",
                    AvailableVramMb: vramMb,
                    AvailableRamMb: ramMb
                ));

                verdict = llmResult.FitVerdict;
                tooltipDetail = llmResult.RecommendationMessage;
            }
        }

        var (badgeText, badgeColorHex) = verdict switch
        {
            FitVerdict.FullVram => ("🟢 Full VRAM", "#10B981"),
            FitVerdict.PartialOffload => ("🟡 Partial Offload", "#F59E0B"),
            FitVerdict.CpuOnly => ("🟠 CPU Only", "#F97316"),
            _ => ("🔴 Won't Fit (OOM)", "#EF4444")
        };

        return new QuickFitBadge(
            BadgeText: badgeText,
            BadgeColorHex: badgeColorHex,
            Tooltip: tooltipDetail,
            FitVerdict: verdict
        );
    }

    private static double ExtractParamBillions(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return 8.0;

        var match = Regex.Match(modelName, @"(?i)(\d+(?:\.\d+)?)\s*b\b");
        if (match.Success && double.TryParse(match.Groups[1].Value, out double parsed))
        {
            return parsed;
        }

        string lower = modelName.ToLowerInvariant();
        if (lower.Contains("deepseek-r1") || lower.Contains("deepseek_r1") || lower.Contains("deepseek-v3") || lower.Contains("deepseek_v3") || lower.Contains("671"))
            return 671.0;
        if (lower.Contains("70") || lower.Contains("70b"))
            return 70.0;
        if (lower.Contains("72") || lower.Contains("72b"))
            return 72.0;
        if (lower.Contains("32") || lower.Contains("32b"))
            return 32.0;
        if (lower.Contains("14") || lower.Contains("14b"))
            return 14.0;
        if (lower.Contains("13") || lower.Contains("13b"))
            return 13.0;
        if (lower.Contains("8") || lower.Contains("8b"))
            return 8.0;
        if (lower.Contains("7") || lower.Contains("7b"))
            return 7.0;
        if (lower.Contains("3") || lower.Contains("3b"))
            return 3.0;
        if (lower.Contains("1.5") || lower.Contains("1.5b"))
            return 1.5;

        return 8.0;
    }

    private static string ExtractQuantization(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return "Q4_K_M";

        var match = Regex.Match(modelName, @"(?i)(Q\d+_[K01A-Z_]+|FP16|FP8|BF16|Q\d+_0|Q\d+_1)");
        if (match.Success)
        {
            return match.Value.ToUpperInvariant();
        }

        return "Q4_K_M";
    }
}
