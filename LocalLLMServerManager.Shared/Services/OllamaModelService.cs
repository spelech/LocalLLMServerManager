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
            HttpResponseMessage response;
            try
            {
                response = await http.GetAsync($"{apiBase}/api/models");
                if (!response.IsSuccessStatusCode && !OperatingSystem.IsBrowser())
                {
                    response = await http.GetAsync("http://127.0.0.1:11434/api/tags");
                }
            }
            catch
            {
                if (OperatingSystem.IsBrowser()) return result;
                response = await http.GetAsync("http://127.0.0.1:11434/api/tags");
            }

            if (response.IsSuccessStatusCode)
            {
                var jsonStr = await response.Content.ReadAsStringAsync();
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

                        result.Add(new OllamaModelItem(name, formatSize, cap, color, false, null, size));
                    }
                }
            }
        }
        catch { }

        return result;
    }

    public async Task<bool> UnloadAllVramAsync(string apiBase, HttpClient http)
    {
        try
        {
            HttpResponseMessage psResp;
            try
            {
                psResp = await http.GetAsync($"{apiBase}/api/ollama/ps");
                if (!psResp.IsSuccessStatusCode) psResp = await http.GetAsync("http://127.0.0.1:11434/api/ps");
            }
            catch
            {
                psResp = await http.GetAsync("http://127.0.0.1:11434/api/ps");
            }

            if (psResp.IsSuccessStatusCode)
            {
                var doc = JsonNode.Parse(await psResp.Content.ReadAsStringAsync());
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
        catch
        {
            return false;
        }
    }

    public async Task<bool> PreloadModelAsync(string apiBase, string modelName, HttpClient http)
    {
        try
        {
            var content = new StringContent(
                JsonSerializer.Serialize(new { model = modelName, keep_alive = -1 }),
                System.Text.Encoding.UTF8,
                "application/json"
            );
            var resp = await http.PostAsync("http://127.0.0.1:11434/api/generate", content);
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
            var content = new StringContent(
                JsonSerializer.Serialize(new { name = modelName, stream = false }),
                System.Text.Encoding.UTF8,
                "application/json"
            );
            var resp = await client.PostAsync("http://127.0.0.1:11434/api/pull", content);
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
            var content = new StringContent(
                JsonSerializer.Serialize(new { target = modelName, type = "ollama" }),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            using var req = new HttpRequestMessage(HttpMethod.Delete, $"{apiBase}/api/models/delete") { Content = content };
            var resp = await http.SendAsync(req);
            if (resp.IsSuccessStatusCode) return true;

            if (!OperatingSystem.IsBrowser())
            {
                var directContent = new StringContent(
                    JsonSerializer.Serialize(new { name = modelName }),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );
                using var directReq = new HttpRequestMessage(HttpMethod.Delete, "http://127.0.0.1:11434/api/delete") { Content = directContent };
                var directResp = await http.SendAsync(directReq);
                return directResp.IsSuccessStatusCode;
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
            var content = new StringContent(
                JsonSerializer.Serialize(new { target = filePath, type = "file" }),
                System.Text.Encoding.UTF8,
                "application/json"
            );

            using var req = new HttpRequestMessage(HttpMethod.Delete, $"{apiBase}/api/models/delete") { Content = content };
            var resp = await http.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
