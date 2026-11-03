using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Services;

public class OllamaModelService : IOllamaModelService
{
    public async Task<List<OllamaModelItem>> LoadInstalledModelsAsync(string apiBase, HttpClient http)
    {
        var result = new List<OllamaModelItem>();
        try
        {
            HttpResponseMessage? response = null;
            try
            {
                try
                {
                    response = await http.GetAsync($"{apiBase}/api/models");
                    if (!response.IsSuccessStatusCode && !OperatingSystem.IsBrowser())
                    {
                        response.Dispose();
                        response = await http.GetAsync("http://127.0.0.1:11434/api/tags");
                    }
                }
                catch
                {
                    response?.Dispose();
                    if (OperatingSystem.IsBrowser()) return result;
                    response = await http.GetAsync("http://127.0.0.1:11434/api/tags");
                }

                using var resp = response;
                if (resp.IsSuccessStatusCode)
                {
                    var jsonStr = await resp.Content.ReadAsStringAsync();
                    var doc = JsonNode.Parse(jsonStr);
                    var models = doc?["models"]?.AsArray();

                    if (models != null)
                    {
                        foreach (var m in models)
                        {
                            string name = m?["name"]?.ToString() ?? "Unknown";
                            long size = m?["size"]?.GetValue<long>() ?? 0L;
                            double sizeGb = Math.Round(size / (1024.0 * 1024.0 * 1024.0), 2);
                            string formatSize = sizeGb > 0 ? $"{sizeGb} GB" : "N/A";

                            string cap = "💻 Coding & General";
                            string color = "#38BDF8";
                            if (name.Contains("math", StringComparison.OrdinalIgnoreCase)) { cap = "🧮 Mathematics"; color = "#C084FC"; }
                            else if (name.Contains("r1", StringComparison.OrdinalIgnoreCase) || name.Contains("deepseek", StringComparison.OrdinalIgnoreCase)) { cap = "🧠 Reasoning profile"; color = "#A855F7"; }

                            int contextLength = 24576;
                            var ctxNode = m?["context_length"] ?? m?["details"]?["context_length"];
                            if (ctxNode != null && int.TryParse(ctxNode.ToString(), out var parsedCtx) && parsedCtx > 0)
                            {
                                contextLength = parsedCtx;
                            }

                            int maxOut = 8192;
                            var outNode = m?["max_output_tokens"] ?? m?["details"]?["max_output_tokens"];
                            if (outNode != null && int.TryParse(outNode.ToString(), out var parsedOut) && parsedOut > 0)
                            {
                                maxOut = parsedOut;
                            }

                            result.Add(new OllamaModelItem(name, formatSize, cap, color, false, null, size, false, contextLength, contextLength, maxOut));
                        }
                    }
                }
            }
            finally
            {
                response?.Dispose();
            }
        }
        catch { }

        return result;
    }

    public async Task<bool> UnloadAllVramAsync(string apiBase, HttpClient http)
    {
        try
        {
            HttpResponseMessage? psResp = null;
            try
            {
                try
                {
                    psResp = await http.GetAsync($"{apiBase}/api/ollama/ps");
                    if (!psResp.IsSuccessStatusCode)
                    {
                        psResp.Dispose();
                        psResp = await http.GetAsync("http://127.0.0.1:11434/api/ps");
                    }
                }
                catch
                {
                    psResp?.Dispose();
                    psResp = await http.GetAsync("http://127.0.0.1:11434/api/ps");
                }

                using var resp = psResp;
                if (resp.IsSuccessStatusCode)
                {
                    var doc = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
                    var models = doc?["models"]?.AsArray();
                    if (models != null)
                    {
                        foreach (var m in models)
                        {
                            string name = m?["name"]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(name))
                            {
                                var content = new StringContent(
                                    JsonSerializer.Serialize(new { model = name, keep_alive = 0 }),
                                    System.Text.Encoding.UTF8,
                                    "application/json"
                                );
                                await http.PostAsync("http://127.0.0.1:11434/api/generate", content);
                            }
                        }
                    }
                }
                return true;
            }
            finally
            {
                psResp?.Dispose();
            }
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> PreloadModelAsync(string apiBase, string modelName, HttpClient http)
    {
        try
        {
            using var content = new StringContent(
                JsonSerializer.Serialize(new { model = modelName, keep_alive = -1 }),
                System.Text.Encoding.UTF8,
                "application/json"
            );
            using var resp = await http.PostAsync("http://127.0.0.1:11434/api/generate", content);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<OllamaModelItem>> GetInstalledModelsAsync()
    {
        using var client = new HttpClient();
        return await LoadInstalledModelsAsync("http://127.0.0.1:11434", client);
    }

    public async Task<bool> PullModelAsync(string modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return false;
        try
        {
            using var client = new HttpClient();
            using var content = new StringContent(
                JsonSerializer.Serialize(new { name = modelName, stream = false }),
                System.Text.Encoding.UTF8,
                "application/json"
            );
            using var resp = await client.PostAsync("http://127.0.0.1:11434/api/pull", content);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteModelAsync(string apiBase, string modelName, HttpClient http)
    {
        if (string.IsNullOrWhiteSpace(modelName)) return false;
        try
        {
            var baseAddress = !string.IsNullOrWhiteSpace(apiBase)
                ? apiBase.TrimEnd('/')
                : (http.BaseAddress != null ? "" : (OperatingSystem.IsBrowser() ? "" : "http://127.0.0.1:5246"));

            var endpointUrl = string.IsNullOrWhiteSpace(baseAddress)
                ? $"/api/models/delete?model={Uri.EscapeDataString(modelName)}&type=ollama"
                : $"{baseAddress}/api/models/delete?model={Uri.EscapeDataString(modelName)}&type=ollama";

            // 1. Try server proxy endpoint with query params via DELETE (body-less for browser fetch compatibility)
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Delete, endpointUrl);
                using var resp = await http.SendAsync(req);
                if (resp.IsSuccessStatusCode) return true;
            }
            catch { }

            // 2. Fallback to POST on proxy endpoint
            try
            {
                using var postContent = new StringContent(
                    JsonSerializer.Serialize(new { model = modelName, name = modelName, type = "ollama" }),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );
                using var postReq = new HttpRequestMessage(HttpMethod.Post, endpointUrl) { Content = postContent };
                using var postResp = await http.SendAsync(postReq);
                if (postResp.IsSuccessStatusCode) return true;
            }
            catch { }

            // 3. If on desktop/native, try direct Ollama daemon
            if (!OperatingSystem.IsBrowser())
            {
                try
                {
                    using var unloadContent = new StringContent(
                        JsonSerializer.Serialize(new { model = modelName, keep_alive = 0 }),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );
                    using var unloadResp = await http.PostAsync("http://127.0.0.1:11434/api/generate", unloadContent);
                }
                catch { }

                try
                {
                    using var directContent = new StringContent(
                        JsonSerializer.Serialize(new { model = modelName, name = modelName }),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );
                    using var directReq = new HttpRequestMessage(HttpMethod.Delete, "http://127.0.0.1:11434/api/delete") { Content = directContent };
                    using var directResp = await http.SendAsync(directReq);
                    if (directResp.IsSuccessStatusCode) return true;

                    var altName = modelName.EndsWith(":latest", StringComparison.OrdinalIgnoreCase)
                        ? modelName.Substring(0, modelName.Length - 7)
                        : $"{modelName}:latest";
                    using var altContent = new StringContent(
                        JsonSerializer.Serialize(new { model = altName, name = altName }),
                        System.Text.Encoding.UTF8,
                        "application/json"
                    );
                    using var altReq = new HttpRequestMessage(HttpMethod.Delete, "http://127.0.0.1:11434/api/delete") { Content = altContent };
                    using var altResp = await http.SendAsync(altReq);
                    return altResp.IsSuccessStatusCode;
                }
                catch { }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteLocalModelFileAsync(string apiBase, string filePath, HttpClient http)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        try
        {
            var baseAddress = !string.IsNullOrWhiteSpace(apiBase)
                ? apiBase.TrimEnd('/')
                : (http.BaseAddress != null ? "" : (OperatingSystem.IsBrowser() ? "" : "http://127.0.0.1:5246"));

            var endpointUrl = string.IsNullOrWhiteSpace(baseAddress)
                ? $"/api/models/delete?file_path={Uri.EscapeDataString(filePath)}&type=file"
                : $"{baseAddress}/api/models/delete?file_path={Uri.EscapeDataString(filePath)}&type=file";

            using var content = new StringContent(
                JsonSerializer.Serialize(new { file_path = filePath, filePath, type = "file" }),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            using var req = new HttpRequestMessage(HttpMethod.Delete, endpointUrl) { Content = content };
            using var resp = await http.SendAsync(req);
            if (resp.IsSuccessStatusCode) return true;

            using var postContent = new StringContent(
                JsonSerializer.Serialize(new { file_path = filePath, filePath, type = "file" }),
                System.Text.Encoding.UTF8,
                "application/json"
            );
            using var postReq = new HttpRequestMessage(HttpMethod.Post, endpointUrl) { Content = postContent };
            using var postResp = await http.SendAsync(postReq);
            return postResp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
