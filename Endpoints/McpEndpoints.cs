using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LocalLLMServerManager.Endpoints;

public static class McpEndpoints
{
    public static void MapMcpEndpoints(this WebApplication app)
    {
        // Standard Model Context Protocol (MCP) Streamable HTTP & SSE endpoint
        try
        {
            app.MapMcp("/mcp");
        }
        catch { }

        // Backwards-compatible discovery endpoint
        app.MapGet("/api/mcp/tools", () => Results.Ok(new
        {
            protocol = "mcp",
            version = "2024-11-05",
            endpoint = "/mcp",
            tools = new[]
            {
                new { name = "get_gpu_vram", description = "Get real-time GPU VRAM allocation, total memory, used memory, and GPU hardware name via NVML CUDA." },
                new { name = "check_health", description = "Check real-time health and connectivity of Ollama, Stable Diffusion Forge, and ComfyUI backend ports." },
                new { name = "list_models", description = "List all installed Ollama LLM models, quantization formats, and memory/disk footprint." },
                new { name = "pull_model", description = "Trigger a model pull from the Ollama library or Hugging Face repository." },
                new { name = "unload_vram", description = "Unload all LLM models currently residing in GPU VRAM to free memory for diffusion or 3D workflows." },
                new { name = "start_engine", description = "Start an AI backend engine process ('forge' or 'comfyui')." },
                new { name = "stop_engine", description = "Gracefully terminate an AI backend engine process ('forge' or 'comfyui')." },
                new { name = "detect_tools", description = "Scan system drives and PATH for installed Ollama, ComfyUI, and SD Forge directories." }
            }
        }));
    }
}
