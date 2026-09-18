using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace LocalLLMServerManager.Shared.ViewModels;

public record DocStep(
    int StepNumber,
    string Title,
    string Action,
    string ExpectedResult
);

public record DocSection(
    string Id,
    string Title,
    string Icon,
    string Summary,
    string Prerequisite,
    List<DocStep> Steps,
    List<string>? Notes = null,
    List<string>? Warnings = null
);

public partial class DocumentationViewModel : ObservableObject
{
    public ObservableCollection<DocSection> Sections { get; } = new();

    [ObservableProperty]
    private DocSection? _selectedSection;

    public DocumentationViewModel()
    {
        InitializeSections();
        SelectedSection = Sections.FirstOrDefault();
    }

    [RelayCommand]
    public void SelectSection(string sectionId)
    {
        var match = Sections.FirstOrDefault(s => s.Id.Equals(sectionId, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            SelectedSection = match;
        }
    }

    private void InitializeSections()
    {
        Sections.Clear();

        // 1. Image Generation Flow
        Sections.Add(new DocSection(
            Id: "image-generation",
            Title: "How to Generate Images",
            Icon: "🎨",
            Summary: "Use this procedure to generate images with Stable Diffusion or ComfyUI.",
            Prerequisite: "Start the Forge or ComfyUI engine in the Workflows tab.",
            Steps: new List<DocStep>
            {
                new(1, "Open Workflows Tab", "Click 'Workflows' in the top navigation bar.", "The Workflows workspace opens."),
                new(2, "Select Image Studio", "Click the '🎨 Image' button in the studio selector.", "The Image generation controls display."),
                new(3, "Choose Engine", "Select 'Stable Diffusion / Forge' or 'ComfyUI'.", "The engine status shows Online."),
                new(4, "Select Style Preset", "Click a preset chip such as 'Standard Square' or 'Landscape Wallpaper'.", "Resolution and sampling parameters load automatically."),
                new(5, "Enter Text Prompt", "Type your descriptive prompt into the Prompt box. Add unwanted features into the Negative Prompt box.", "Prompt text is ready for generation."),
                new(6, "Click Generate Image", "Click the '🎨 Generate Image' button.", "The stage tracker shows generation progress and the final image appears in the gallery.")
            },
            Notes: new List<string>
            {
                "Higher resolution requires more VRAM. Use 512x512 for SD 1.5 and 1024x1024 for SDXL.",
                "Preset configurations automatically set safe aspect ratios for your selected engine."
            },
            Warnings: new List<string>
            {
                "Do not close the manager during generation. This will stop the generation process."
            }
        ));

        // 2. Text / LLM Workflow
        Sections.Add(new DocSection(
            Id: "text-generation",
            Title: "How to Run Text LLM Models",
            Icon: "💬",
            Summary: "Use this procedure to download and run local Large Language Models with Ollama.",
            Prerequisite: "Verify that Ollama status shows Online in the top telemetry header.",
            Steps: new List<DocStep>
            {
                new(1, "Open Models Tab", "Click 'Models' in the top navigation bar.", "The Models management view opens."),
                new(2, "Download or Select Model", "Select 'Downloaded & Manage'. Choose a model from your installed list, or pull a new model from the library.", "The model is loaded and ready for inference."),
                new(3, "Open Workflows Tab", "Click 'Workflows' in the top navigation bar, then select '💬 Text'.", "The Text LLM generation studio displays."),
                new(4, "Configure Context Window", "Adjust target context tokens if necessary. Review the estimated KV cache memory text.", "Target memory stays within available GPU VRAM limits."),
                new(5, "Enter Prompt", "Type your question or instructions into the text area.", "The prompt is ready to submit."),
                new(6, "Click Generate Text", "Click the '💬 Generate Text' button.", "The model generates the response in real time.")
            },
            Notes: new List<string>
            {
                "Context lengths above 16,384 tokens require significant VRAM for KV cache storage."
            }
        ));

        // 3. Video Generation Flow
        Sections.Add(new DocSection(
            Id: "video-generation",
            Title: "How to Generate AI Videos",
            Icon: "🎬",
            Summary: "Use this procedure to generate AI video clips with Wan 2.2, LTX-2.5, or HunyuanVideo.",
            Prerequisite: "Verify that at least 8 GB of VRAM is available and ComfyUI is running.",
            Steps: new List<DocStep>
            {
                new(1, "Open Workflows Tab", "Click 'Workflows' in the top navigation bar.", "The Workflows workspace opens."),
                new(2, "Select Video Studio", "Click the '🎬 Video' button in the studio selector.", "The Video generation studio displays."),
                new(3, "Select Video Preset", "Click a preset chip (for example, 'Quick 480p Preview' or 'Cinematic HD 720p').", "Workflow model, resolution, and frame counts populate automatically."),
                new(4, "Enter Prompt", "Type your scene description into the Prompt box.", "The scene description is validated."),
                new(5, "Click Generate Video", "Click the '🎬 Generate Video' button.", "The 4-stage pipeline tracker shows live progress, VRAM load, and denoising steps."),
                new(6, "Preview and Export", "When generation finishes, the clip loads in the interactive player. Click 'Export' to save the MP4 file.", "The MP4 video file is saved to your disk.")
            },
            Notes: new List<string>
            {
                "Use 'Quick 480p Preview' first to test your prompt before generating at higher resolutions."
            },
            Warnings: new List<string>
            {
                "Video generation is computationally intensive. Ensure adequate GPU cooling during long renders."
            }
        ));

        // 4. Audio & Speech Synthesis
        Sections.Add(new DocSection(
            Id: "audio-generation",
            Title: "How to Synthesize Speech and Audio",
            Icon: "🎵",
            Summary: "Use this procedure to create speech with Kokoro TTS or sound effects with Stable Audio.",
            Prerequisite: "Ensure the Audio Feature Pack is installed in the Settings tab.",
            Steps: new List<DocStep>
            {
                new(1, "Open Workflows Tab", "Click 'Workflows' in the top navigation bar.", "The Workflows workspace opens."),
                new(2, "Select Audio Studio", "Click the '🎵 Audio' button in the studio selector.", "The Audio generation studio displays."),
                new(3, "Choose Workflow", "Select 'Kokoro TTS' for speech narration or 'Stable Audio Open' for sound effects.", "Controls update with duration, seed, and voice parameters."),
                new(4, "Enter Audio Prompt", "Type the speech text or sound effect description into the prompt box.", "The input text is ready for synthesis."),
                new(5, "Queue Audio Generation", "Click 'Queue Audio Generation Workflow'.", "The audio stage tracker monitors generation and the waveform visualizer activates."),
                new(6, "Listen and Play", "Click the Play button in the player bar to listen to the synthesized audio.", "Audio plays through your default speaker output.")
            }
        ));

        // 5. 3D Mesh Generation Flow
        Sections.Add(new DocSection(
            Id: "mesh-generation",
            Title: "How to Generate 3D Meshes",
            Icon: "📦",
            Summary: "Use this procedure to convert images or text into 3D mesh files (.GLB).",
            Prerequisite: "Ensure ComfyUI 3D pack is enabled with at least 12 GB VRAM free.",
            Steps: new List<DocStep>
            {
                new(1, "Open Workflows Tab", "Click 'Workflows' in the top navigation bar.", "The Workflows workspace opens."),
                new(2, "Select 3D Mesh Studio", "Click the '📦 3D' button in the studio selector.", "The 3D studio controls display."),
                new(3, "Select Mode", "Choose 'Text-to-3D' or 'Image-to-3D'.", "Input parameters load for the selected mode."),
                new(4, "Enter Prompt or Image", "Type the object description or supply an input image.", "Input data is staged for 3D reconstruction."),
                new(5, "Click Generate 3D Mesh", "Click the '📦 Generate 3D Mesh' button.", "The system runs sparse-structure and texture generation passes."),
                new(6, "Preview and Download", "Inspect the rendered model and click 'Download GLB'.", "The 3D GLB file is ready for import into Blender or 3D engines.")
            }
        ));

        // 6. Model Discovery & Downloads
        Sections.Add(new DocSection(
            Id: "models-hub",
            Title: "How to Download and Manage Models",
            Icon: "🌐",
            Summary: "Use this procedure to find, evaluate, and download models from Hugging Face and CivitAI.",
            Prerequisite: "Ensure an active internet connection is available.",
            Steps: new List<DocStep>
            {
                new(1, "Open Models Tab", "Click 'Models' in the top navigation bar.", "The Models hub opens."),
                new(2, "Select Hub", "Select '🤗 Hugging Face Hub' for GGUF/multimodal models, or '🎨 CivitAI Hub' for SD checkpoints and LoRAs.", "The corresponding search view displays."),
                new(3, "Search or Click Suggestion", "Type keywords into the search box or click a suggestion chip (such as 'SDXL' or 'Photorealistic').", "The loading indicator displays while searching."),
                new(4, "Verify Hardware Fit", "Inspect the green, yellow, or red compatibility badge on the model card.", "The badge confirms whether the model fits into your GPU VRAM."),
                new(5, "Click Download", "Click 'Download' on the desired model card.", "A download toast notification confirms the operation and progress begins.")
            }
        ));

        // 7. Hardware Fit (Can I Run It)
        Sections.Add(new DocSection(
            Id: "can-i-run-it",
            Title: "How to Check Hardware Compatibility",
            Icon: "⚡",
            Summary: "Use this procedure to verify whether an AI model fits into your GPU memory before downloading.",
            Prerequisite: "Check that GPU memory telemetry is visible in the top header.",
            Steps: new List<DocStep>
            {
                new(1, "Open Can I Run It Tab", "Click '⚡ Can I Run It' in the top navigation bar.", "The hardware fit calculator view opens."),
                new(2, "Select Modality", "Choose LLM, Image, Video, Audio, or 3D.", "Parameters for the chosen modality appear."),
                new(3, "Enter Model Size", "Enter the model name, size in GB, or select a known preset.", "The system computes VRAM and system RAM requirements."),
                new(4, "Read Verdict Banner", "Review the verdict: Full VRAM (Fastest), Partial Offload (Usable), CPU Only (Slow), or Out of Memory (Incompatible).", "You know if the model will run smoothly without system crashes.")
            }
        ));

        // 8. System Health & Troubleshooting
        Sections.Add(new DocSection(
            Id: "troubleshooting",
            Title: "System Health & Troubleshooting",
            Icon: "🛠️",
            Summary: "Use this procedure to identify and fix common server or engine connection issues.",
            Prerequisite: "Check the service status pills in the top header.",
            Steps: new List<DocStep>
            {
                new(1, "Check Service Badges", "Inspect the Ollama, Forge, and ComfyUI status pills in the header.", "Green indicates Online. Red indicates Offline. Yellow indicates Busy."),
                new(2, "Start an Offline Engine", "If an engine shows Offline, open the Workflows tab and click 'Toggle Engine' or click 'Refresh' in the header.", "The engine starts and the pill turns green."),
                new(3, "Clear GPU Memory", "If a model fails with Out Of Memory, click 'Refresh' or unload unused models in the Models tab.", "GPU VRAM is freed for the next generation."),
                new(4, "Verify Tool Discovery", "Open Settings and review the Tool Discovery section if an engine fails to launch.", "Paths to Python, Git, and engines are validated.")
            }
        ));
    }
}
