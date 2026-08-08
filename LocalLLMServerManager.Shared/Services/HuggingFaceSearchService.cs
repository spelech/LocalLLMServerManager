using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Interfaces;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Services;

public class HuggingFaceSearchService : IHuggingFaceSearchService
{
    public async Task<List<HuggingFaceRepoItem>> SearchRepositoriesAsync(string apiBase, string query, HttpClient http)
    {
        var result = new List<HuggingFaceRepoItem>();
        try
        {
            var url = $"{apiBase}/api/hf/search?q={Uri.EscapeDataString(query)}";
            var response = await http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var doc = JsonNode.Parse(await response.Content.ReadAsStringAsync());
                var repos = doc?.AsArray();
                if (repos != null)
                {
                    foreach (var repo in repos)
                    {
                        string id = repo?["id"]?.ToString() ?? "";
                        string author = repo?["author"]?.ToString() ?? "Community";
                        int downloads = repo?["downloads"]?.GetValue<int>() ?? 0;
                        int likes = repo?["likes"]?.GetValue<int>() ?? 0;
                        if (!string.IsNullOrEmpty(id))
                        {
                            result.Add(new HuggingFaceRepoItem(id, author, likes, $"{downloads:N0} downloads"));
                        }
                    }
                }
            }
        }
        catch { }

        return result;
    }

    public async Task<List<HfFileQuantItem>> FetchQuantizationsAsync(string apiBase, string repoId, HttpClient http)
    {
        var result = new List<HfFileQuantItem>();
        try
        {
            var url = $"{apiBase}/api/hf/model?repoId={Uri.EscapeDataString(repoId)}";
            var response = await http.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var doc = JsonNode.Parse(await response.Content.ReadAsStringAsync());
                var siblings = doc?["siblings"]?.AsArray();
                if (siblings != null)
                {
                    foreach (var sib in siblings)
                    {
                        string rfilename = sib?["rfilename"]?.ToString() ?? "";
                        if (rfilename.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
                        {
                            long size = sib?["size"]?.GetValue<long>() ?? 0L;
                            double sizeGb = Math.Round(size / (1024.0 * 1024.0 * 1024.0), 2);
                            string sizeText = sizeGb > 0 ? $"{sizeGb} GB" : "N/A";
                            string quant = "Q4_K_M";
                            if (rfilename.Contains("Q8_0", StringComparison.OrdinalIgnoreCase)) quant = "Q8_0";
                            else if (rfilename.Contains("Q5_K_M", StringComparison.OrdinalIgnoreCase)) quant = "Q5_K_M";
                            else if (rfilename.Contains("FP16", StringComparison.OrdinalIgnoreCase)) quant = "FP16";

                            result.Add(new HfFileQuantItem(rfilename, quant, sizeText, size));
                        }
                    }
                }
            }
        }
        catch { }

        return result;
    }
}
