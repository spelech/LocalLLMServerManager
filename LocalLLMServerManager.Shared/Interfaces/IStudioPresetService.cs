namespace LocalLLMServerManager.Shared.Interfaces;

using System.Collections.Generic;
using LocalLLMServerManager.Shared.Models;

/// <summary>
/// Service interface for managing built-in and user-defined studio presets.
/// </summary>
public interface IStudioPresetService
{
    IReadOnlyList<StudioPreset> GetPresets(StudioModality modality);
    IReadOnlyList<StudioPreset> GetAllPresets();
    StudioPreset? GetPresetById(string id);
    void SavePreset(StudioPreset preset);
    bool DeletePreset(string id);
    StudioPreset? DuplicatePreset(string id);
    string ExportJson();
    bool ImportJson(string json);
    void ResetToDefaults();
}
