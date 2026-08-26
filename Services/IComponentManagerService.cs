using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Services;

public interface IComponentManagerService
{
    bool IsVideoPackInstalled { get; }
    bool IsAudioPackInstalled { get; }
    bool IsMusicPackInstalled { get; }
    Task<IEnumerable<ComponentPackInfo>> GetComponentsAsync();
    Task<bool> InstallComponentAsync(string componentId, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    Task<bool> UninstallComponentAsync(string componentId);
}
