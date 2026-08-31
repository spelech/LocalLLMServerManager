using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Interfaces;

public interface IOllamaModelService
{
    Task<List<OllamaModelItem>> LoadInstalledModelsAsync(string apiBase, HttpClient http);
    Task<bool> UnloadAllVramAsync(string apiBase, HttpClient http);
    Task<bool> PreloadModelAsync(string apiBase, string modelName, HttpClient http);
    Task<List<OllamaModelItem>> GetInstalledModelsAsync();
    Task<bool> PullModelAsync(string modelName);
    Task<bool> DeleteModelAsync(string apiBase, string modelName, HttpClient http) => Task.FromResult(true);
    Task<bool> DeleteLocalModelFileAsync(string apiBase, string filePath, HttpClient http) => Task.FromResult(true);
}
