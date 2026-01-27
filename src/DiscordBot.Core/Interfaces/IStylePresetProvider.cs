using DiscordBot.Core.Models;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Provides access to predefined style presets for TTS voice configuration.
/// </summary>
public interface IStylePresetProvider
{
    /// <summary>
    /// Gets all available style presets.
    /// </summary>
    /// <returns>A read-only list of all style presets.</returns>
    IReadOnlyList<StylePreset> GetAllPresets();

    /// <summary>
    /// Gets a specific preset by its ID.
    /// </summary>
    /// <param name="presetId">The unique preset identifier.</param>
    /// <returns>The preset if found; otherwise null.</returns>
    StylePreset? GetPresetById(string presetId);

    /// <summary>
    /// Gets all presets in a specific category.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <returns>A read-only list of presets in the category.</returns>
    IReadOnlyList<StylePreset> GetPresetsByCategory(string category);

    /// <summary>
    /// Gets all featured/popular presets.
    /// </summary>
    /// <returns>A read-only list of featured presets.</returns>
    IReadOnlyList<StylePreset> GetFeaturedPresets();

    /// <summary>
    /// Gets all available category names.
    /// </summary>
    /// <returns>A read-only list of category names.</returns>
    IReadOnlyList<string> GetCategories();
}
