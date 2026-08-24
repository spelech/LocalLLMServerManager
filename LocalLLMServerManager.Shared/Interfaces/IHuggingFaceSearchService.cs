using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Interfaces;

public interface IHuggingFaceSearchService
{
    Task<List<HuggingFaceRepoItem>> SearchRepositoriesAsync(string apiBase, string query, string? pipelineTag, HttpClient http);
    Task<List<HuggingFaceRepoItem>> SearchRepositoriesAsync(string apiBase, string query, HttpClient http) => SearchRepositoriesAsync(apiBase, query, null, http);
    Task<List<HuggingFaceRepoItem>> SearchModelsAsync(string query, string? pipelineTag = null, System.Threading.CancellationToken ct = default);
    Task<List<HfFileQuantItem>> FetchQuantizationsAsync(string apiBase, string repoId, HttpClient http);
}
