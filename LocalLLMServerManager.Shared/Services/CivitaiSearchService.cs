using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Services;

public class CivitaiSearchService : ICivitaiSearchService
{
    public async Task<List<CivitaiModelItem>> SearchModelsAsync(string apiBase, string query, string types, string sort, HttpClient http)
    {
        var result = new List<CivitaiModelItem>();
        try
        {
            var url = $"{apiBase}/api/civitai/search?q={Uri.EscapeDataString(query)}&types={Uri.EscapeDataString(types)}&sort={Uri.EscapeDataString(sort)}";
            var response = await http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var doc = JsonNode.Parse(await response.Content.ReadAsStringAsync());
                var items = doc?["items"]?.AsArray();
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        int id = item?["id"]?.GetValue<int>() ?? 0;
                        string name = item?["name"]?.ToString() ?? "Unknown";
                        string type = item?["type"]?.ToString() ?? "Checkpoint";

                        string imageUrl = "";
                        string fileUrl = "";
                        string fileName = "";

                        var versions = item?["modelVersions"]?.AsArray();
                        if (versions != null && versions.Count > 0)
                        {
                            var mv = versions[0];
                            var images = mv?["images"]?.AsArray();
                            if (images != null && images.Count > 0)
                            {
                                imageUrl = images[0]?["url"]?.ToString() ?? "";
                            }
                            var files = mv?["files"]?.AsArray();
                            if (files != null && files.Count > 0)
                            {
                                fileUrl = files[0]?["downloadUrl"]?.ToString() ?? "";
                                fileName = files[0]?["name"]?.ToString() ?? $"{name}.safetensors";
                            }
                        }

                        if (!string.IsNullOrEmpty(name))
                        {
                            result.Add(new CivitaiModelItem(id, name, type, imageUrl, fileUrl, fileName, 4.8, 1250));
                        }
                    }
                }
            }
        }
        catch { }

        return result;
    }
}
