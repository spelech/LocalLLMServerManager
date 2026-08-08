using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Interfaces;

public interface IHuggingFaceSearchService
{
    Task<List<HuggingFaceRepoItem>> SearchRepositoriesAsync(string apiBase, string query, HttpClient http);
    Task<List<HfFileQuantItem>> FetchQuantizationsAsync(string apiBase, string repoId, HttpClient http);
}
