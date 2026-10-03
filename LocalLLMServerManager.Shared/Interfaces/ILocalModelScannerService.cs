using System.Collections.Generic;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.Interfaces;

public interface ILocalModelScannerService
{
    Task<IReadOnlyList<LocalModelItem>> ScanAllModelsAsync(AppSettings? settings = null, string? baseDirectory = null);
    IReadOnlyList<LocalModelItem> GetCachedModels();
}
