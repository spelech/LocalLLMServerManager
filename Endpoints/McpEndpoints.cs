using Microsoft.AspNetCore.Builder;

namespace LocalLLMServerManager.Endpoints;

public static class McpEndpoints
{
    public static void MapMcpEndpoints(this WebApplication app)
    {
        // Standard Model Context Protocol (MCP) Streamable HTTP & SSE endpoint compliant with 2026-07-28 stateless spec
        try
        {
            app.MapMcp("/mcp");
        }
        catch (InvalidOperationException)
        {
            // Handled when invoked on bare WebApplication instances without MCP services registered
        }
    }
}

