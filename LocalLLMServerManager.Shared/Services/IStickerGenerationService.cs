using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LocalLLMServerManager.Shared.Models;

namespace LocalLLMServerManager.Shared.Services;

/// <summary>
/// Service interface for sticker generation, isolating subjects, and applying die-cut borders.
/// </summary>
public interface IStickerGenerationService
{
    /// <summary>
    /// Generates a die-cut sticker based on the specified request parameters.
    /// </summary>
    Task<StickerResult> GenerateStickerAsync(StickerGenerationRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets the curated list of default sticker style presets.
    /// </summary>
    IReadOnlyList<StickerStylePreset> GetDefaultPresets() => StickerGenerationService.DefaultPresets;
}
