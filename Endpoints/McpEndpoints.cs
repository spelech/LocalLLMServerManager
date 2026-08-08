using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LocalLLMServerManager.Endpoints;

public static class McpEndpoints
{
    public static void MapMcpEndpoints(this WebApplication app)
    {
        app.MapGet("/api/mcp/tools", () => Results.Ok(new
        {
            tools = new[]
            {
                new { name = "list_models", description = "List installed Ollama LLM models and memory footprint" },
                new { name = "unload_vram", description = "Unload all LLM models from GPU memory" },
                new { name = "check_health", description = "Check health of Ollama, SD Forge, and ComfyUI backends" },
                new { name = "get_gpu_vram", description = "Get real-time GPU VRAM utilization via NVML CUDA" },
                new { name = "start_engine", description = "Start SD Forge or ComfyUI engine process" },
                new { name = "stop_engine", description = "Stop SD Forge or ComfyUI engine process" }
            }
        }));
    }
}
