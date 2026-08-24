namespace LocalLLMServerManager.Shared.Models;

public class ComponentPackInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Installed { get; set; }
    public string DiskSizeEstimate { get; set; } = string.Empty;
    public string MinVramRequired { get; set; } = string.Empty;
}

public class ComponentInstallRequest
{
    public string ComponentId { get; set; } = string.Empty;
}
