using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace LocalLLMServerManager.Endpoints;

public static class ModelProxyEndpoints
{
    public static void MapModelProxyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/models", async (IHttpClientFactory clientFactory) =>
        {
            try
            {
                var http = clientFactory.CreateClient();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var response = await http.GetAsync("http://127.0.0.1:11434/api/tags", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cts.Token);
                    return Results.Content(content, "application/json");
                }
            }
            catch { }
            return Results.Ok(new { models = new object[0] });
        });

        app.MapGet("/api/ollama/ps", async (IHttpClientFactory clientFactory) =>
        {
            try
            {
                var http = clientFactory.CreateClient();
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var response = await http.GetAsync("http://127.0.0.1:11434/api/ps", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cts.Token);
                    return Results.Content(content, "application/json");
                }
            }
            catch { }
            return Results.Ok(new { models = new object[0] });
        });

        app.MapGet("/api/hf/search", async (string? q, HttpClient httpClient) =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var query = string.IsNullOrWhiteSpace(q) ? "llama" : q;
                var requestUrl = $"https://huggingface.co/api/models?search={Uri.EscapeDataString(query)}&filter=gguf&sort=downloads&direction=-1&limit=20";
                
                using var req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                req.Headers.UserAgent.ParseAdd("LocalLLMServerManager/3.5.0");
                var response = await httpClient.SendAsync(req, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonStr = await response.Content.ReadAsStringAsync(cts.Token);
                    return Results.Content(jsonStr, "application/json");
                }
                return Results.StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        app.MapGet("/api/hf/model", async (string repoId, HttpClient httpClient) =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var requestUrl = $"https://huggingface.co/api/models/{Uri.EscapeDataString(repoId)}";
                using var req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                req.Headers.UserAgent.ParseAdd("LocalLLMServerManager/3.5.0");
                var response = await httpClient.SendAsync(req, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonStr = await response.Content.ReadAsStringAsync(cts.Token);
                    return Results.Content(jsonStr, "application/json");
                }
                return Results.StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        app.MapGet("/api/civitai/search", async (HttpClient http, string? q, string? types, string? sort) =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var query = string.IsNullOrWhiteSpace(q) ? "cyberpunk" : q;
                var typeParam = string.IsNullOrWhiteSpace(types) ? "" : $"&types={Uri.EscapeDataString(types)}";
                var sortParam = string.IsNullOrWhiteSpace(sort) ? "Most Downloaded" : sort;
                var requestUrl = $"https://civitai.com/api/v1/models?query={Uri.EscapeDataString(query)}&limit=15&sort={Uri.EscapeDataString(sortParam)}{typeParam}";

                using var req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                req.Headers.UserAgent.ParseAdd("LocalLLMServerManager/3.5.0");
                var response = await http.SendAsync(req, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var jsonStr = await response.Content.ReadAsStringAsync(cts.Token);
                    return Results.Content(jsonStr, "application/json");
                }
                return Results.StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });

        app.MapGet("/api/civitai/model", async (HttpClient http, int id) =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                var requestUrl = $"https://civitai.com/api/v1/models/{id}";
                using var req = new HttpRequestMessage(HttpMethod.Get, requestUrl);
                req.Headers.UserAgent.ParseAdd("LocalLLMServerManager/3.5.0");
                var response = await http.SendAsync(req, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var jsonStr = await response.Content.ReadAsStringAsync(cts.Token);
                    return Results.Content(jsonStr, "application/json");
                }
                return Results.StatusCode((int)response.StatusCode);
            }
            catch (Exception ex)
            {
                return Results.Problem(ex.Message);
            }
        });
    }
}
