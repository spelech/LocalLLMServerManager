using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.ViewModels;

namespace LocalLLMServerManager.Shared.Interfaces;

public interface ICivitaiSearchService
{
    Task<List<CivitaiModelItem>> SearchModelsAsync(string apiBase, string query, string types, string sort, HttpClient http);
}
