using System.Text.Json;
using LocalLLMServerManager.Services;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Endpoints;

public static class ComponentEndpoints
{
    public static void MapComponentEndpoints(this WebApplication app)
    {
        app.MapGet("/api/components", async (IComponentManagerService componentService) =>
        {
            var components = await componentService.GetComponentsAsync();
            return Results.Ok(components);
        });

        app.MapPost("/api/components/install", async (HttpContext httpContext, ComponentInstallRequest? request, IComponentManagerService componentService) =>
        {
            var componentId = request?.ComponentId;
            if (string.IsNullOrWhiteSpace(componentId))
            {
                return Results.BadRequest(new { error = "ComponentId is required." });
            }

            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers.CacheControl = "no-cache";
            httpContext.Response.Headers.Connection = "keep-alive";

            var syncLock = new SemaphoreSlim(1, 1);
            var progress = new Progress<double>(percent =>
            {
                _ = Task.Run(async () =>
                {
                    await syncLock.WaitAsync();
                    try
                    {
                        var eventData = JsonSerializer.Serialize(new { progress = percent, status = "installing" });
                        await httpContext.Response.WriteAsync($"data: {eventData}\n\n");
                        await httpContext.Response.Body.FlushAsync();
                    }
                    catch { }
                    finally
                    {
                        syncLock.Release();
                    }
                });
            });

            try
            {
                var result = await componentService.InstallComponentAsync(componentId, progress, httpContext.RequestAborted);
                await syncLock.WaitAsync();
                try
                {
                    var finalData = JsonSerializer.Serialize(new { progress = 100.0, status = result ? "completed" : "failed", success = result });
                    await httpContext.Response.WriteAsync($"data: {finalData}\n\n");
                    await httpContext.Response.Body.FlushAsync();
                }
                finally
                {
                    syncLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Request canceled
            }
            catch (Exception ex)
            {
                await syncLock.WaitAsync();
                try
                {
                    var errData = JsonSerializer.Serialize(new { progress = 0.0, status = "error", message = ex.Message });
                    await httpContext.Response.WriteAsync($"data: {errData}\n\n");
                    await httpContext.Response.Body.FlushAsync();
                }
                finally
                {
                    syncLock.Release();
                }
            }

            return Results.Empty;
        });

        app.MapPost("/api/components/uninstall", async (ComponentInstallRequest? request, IComponentManagerService componentService) =>
        {
            var componentId = request?.ComponentId;
            if (string.IsNullOrWhiteSpace(componentId))
            {
                return Results.BadRequest(new { error = "ComponentId is required." });
            }

            var success = await componentService.UninstallComponentAsync(componentId);
            if (!success)
            {
                return Results.BadRequest(new { error = $"Failed to uninstall component '{componentId}'." });
            }

            return Results.Ok(new { message = $"Component '{componentId}' uninstalled successfully.", success = true });
        });
    }
}
