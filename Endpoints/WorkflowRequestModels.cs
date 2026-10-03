namespace LocalLLMServerManager.Endpoints;

public record AudioGenerateRequest(
    string WorkflowId = "stable_audio_open_sfx",
    string Prompt = "",
    string? NegativePrompt = null,
    int DurationSeconds = 30,
    long Seed = -1
);

public record VideoGenerateRequest(
    string? WorkflowId = "wan2.2_t2v",
    string? Workflow = null,
    string? Prompt = "",
    string? NegativePrompt = "",
    int Width = 832,
    int Height = 480,
    int Frames = 49,
    int Fps = 16,
    long Seed = -1,
    string? ImageUrl = null,
    string? Image = null
);

public record ThreeDGenerateRequest(
    string? WorkflowId = "trellis_v2_api",
    string? Workflow = null,
    string? Prompt = "",
    string? NegativePrompt = "",
    string? Format = "glb",
    long Seed = -1
);

public record EngineToggleRequest(string? Engine);
