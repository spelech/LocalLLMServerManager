namespace LocalLLMServerManager.Shared.Models;

public enum NavDomain
{
    Studio,
    Models,
    HardwareFit,
    Settings
}

public enum StickerPipelineStage
{
    Idle,
    GeneratingDiffusion,
    IsolatingSubject,
    ApplyingContour,
    Ready,
    Failed
}

public class StickerStylePreset
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PositiveTokens { get; set; } = string.Empty;
    public string NegativeTokens { get; set; } = string.Empty;
    public int DefaultBorderWidth { get; set; } = 12;
}

public class StickerGenerationRequest
{
    public string? ImagePath { get; set; }
    public byte[]? ImageBytes { get; set; }
    public string StylePresetId { get; set; } = string.Empty;
    public string CustomPrompt { get; set; } = string.Empty;
    public string NegativePrompt { get; set; } = string.Empty;
    public int BorderWidth { get; set; } = 12;
    public bool IsAutoCutoutEnabled { get; set; } = true;
}

public class StickerResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public string? OutputImagePath { get; set; }
    public byte[]? OutputPngBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
