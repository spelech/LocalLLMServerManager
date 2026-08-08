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
}
