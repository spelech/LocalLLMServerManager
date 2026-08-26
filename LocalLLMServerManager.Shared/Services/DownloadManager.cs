using System;
using System.IO;

namespace LocalLLMServerManager.Shared.Services;

public static class DownloadManager
{
    public static string ResolveTargetDirectory(string? modelTypeOrPipelineTag, string? fileName = null, string? rootPath = null)
    {
        var baseDir = rootPath ?? AppContext.BaseDirectory;
        var tagOrType = (modelTypeOrPipelineTag ?? "").ToLowerInvariant();
        var file = (fileName ?? "").ToLowerInvariant();

        // Video models -> ComfyUI/models/diffusion_models
        if (tagOrType.Contains("video") || tagOrType.Contains("text-to-video") || tagOrType.Contains("image-to-video") ||
            file.Contains("wan") || file.Contains("ltx") || file.Contains("hunyuanvideo"))
        {
            return Path.Combine(baseDir, "ComfyUI", "models", "diffusion_models");
        }

        // STT / Speech-to-Text / Whisper -> audio/stt
        if (tagOrType.Contains("automatic-speech-recognition") || tagOrType.Contains("speech-to-text") ||
            tagOrType.Contains("stt") || file.Contains("whisper"))
        {
            return Path.Combine(baseDir, "audio", "stt");
        }

        // Audio engines -> audio/engines
        if (tagOrType.Contains("audio-engine") || tagOrType.Contains("voice-clone") ||
            file.Contains("alltalk") || file.Contains("kokoro-fastapi"))
        {
            return Path.Combine(baseDir, "audio", "engines");
        }

        // TTS / Audio models -> models/tts
        if (tagOrType.Contains("speech") || tagOrType.Contains("tts") || tagOrType.Contains("audio") ||
            tagOrType.Contains("text-to-speech") || tagOrType.Contains("text-to-audio") ||
            file.Contains("kokoro") || file.Contains("f5-tts") || file.Contains("stable-audio"))
        {
            return Path.Combine(baseDir, "models", "tts");
        }

        // 3D models -> models/3d
        if (tagOrType.Contains("3d") || tagOrType.Contains("text-to-3d") || file.Contains("trellis") || file.Contains("hunyuan3d"))
        {
            return Path.Combine(baseDir, "models", "3d");
        }

        // LoRA -> models/Lora
        if (tagOrType.Contains("lora"))
        {
            return Path.Combine(baseDir, "models", "Lora");
        }

        // Default / Checkpoints / LLMs
        return Path.Combine(baseDir, "models", "checkpoints");
    }
}
