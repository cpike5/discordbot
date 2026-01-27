using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.Tts;

/// <summary>
/// Provides predefined style presets for TTS voice configuration.
/// Includes emotional voices, professional broadcasters, character voices, and assistant tones.
/// </summary>
public class StylePresetProvider : IStylePresetProvider
{
    private readonly ILogger<StylePresetProvider> _logger;
    private readonly IReadOnlyList<StylePreset> _presets;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<StylePreset>> _presetsByCategory;
    private readonly IReadOnlyList<StylePreset> _featuredPresets;
    private readonly IReadOnlyList<string> _categories;

    public StylePresetProvider(ILogger<StylePresetProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _presets = new[]
        {
            // Emotional - Female
            new StylePreset
            {
                PresetId = "jenny-cheerful",
                DisplayName = "Cheerful Jenny",
                VoiceName = "en-US-JennyNeural",
                Style = "cheerful",
                StyleDegree = 1.5,
                Description = "Upbeat and enthusiastic female voice",
                Category = "Emotional",
                IsFeatured = true
            },
            new StylePreset
            {
                PresetId = "aria-sad",
                DisplayName = "Sad Aria",
                VoiceName = "en-US-AriaNeural",
                Style = "sad",
                StyleDegree = 1.0,
                Description = "Melancholic and somber female voice",
                Category = "Emotional",
                IsFeatured = false
            },
            new StylePreset
            {
                PresetId = "jenny-angry",
                DisplayName = "Angry Jenny",
                VoiceName = "en-US-JennyNeural",
                Style = "angry",
                StyleDegree = 1.5,
                Description = "Frustrated and upset female voice",
                Category = "Emotional",
                IsFeatured = false
            },

            // Emotional - Male
            new StylePreset
            {
                PresetId = "guy-excited",
                DisplayName = "Excited Guy",
                VoiceName = "en-US-GuyNeural",
                Style = "excited",
                StyleDegree = 1.8,
                Description = "Energetic and thrilled male voice",
                Category = "Emotional",
                IsFeatured = true
            },
            new StylePreset
            {
                PresetId = "davis-angry",
                DisplayName = "Angry Davis",
                VoiceName = "en-US-DavisNeural",
                Style = "angry",
                StyleDegree = 1.5,
                Description = "Frustrated and stern male voice",
                Category = "Emotional",
                IsFeatured = false
            },

            // Professional
            new StylePreset
            {
                PresetId = "jenny-newscast",
                DisplayName = "Jenny Newscast",
                VoiceName = "en-US-JennyNeural",
                Style = "newscast",
                StyleDegree = 1.0,
                Description = "Professional news broadcaster tone",
                Category = "Professional",
                IsFeatured = true
            },
            new StylePreset
            {
                PresetId = "guy-newscast",
                DisplayName = "Guy Newscast",
                VoiceName = "en-US-GuyNeural",
                Style = "newscast",
                StyleDegree = 1.0,
                Description = "Professional male news broadcaster",
                Category = "Professional",
                IsFeatured = true
            },

            // Character Voices
            new StylePreset
            {
                PresetId = "jenny-whispering",
                DisplayName = "Whispering Jenny",
                VoiceName = "en-US-JennyNeural",
                Style = "whispering",
                StyleDegree = 1.0,
                Description = "Quiet, secretive whisper",
                Category = "Character",
                IsFeatured = false
            },
            new StylePreset
            {
                PresetId = "aria-terrified",
                DisplayName = "Terrified Aria",
                VoiceName = "en-US-AriaNeural",
                Style = "terrified",
                StyleDegree = 1.5,
                Description = "Frightened and scared voice",
                Category = "Character",
                IsFeatured = false
            },
            new StylePreset
            {
                PresetId = "guy-shouting",
                DisplayName = "Shouting Guy",
                VoiceName = "en-US-GuyNeural",
                Style = "shouting",
                StyleDegree = 1.5,
                Description = "Loud, yelling male voice",
                Category = "Character",
                IsFeatured = false
            },

            // Assistant/Helper
            new StylePreset
            {
                PresetId = "jenny-assistant",
                DisplayName = "Jenny Assistant",
                VoiceName = "en-US-JennyNeural",
                Style = "assistant",
                StyleDegree = 1.0,
                Description = "Helpful AI assistant tone",
                Category = "Assistant",
                IsFeatured = true
            },
            new StylePreset
            {
                PresetId = "jenny-customerservice",
                DisplayName = "Jenny Customer Service",
                VoiceName = "en-US-JennyNeural",
                Style = "customerservice",
                StyleDegree = 1.0,
                Description = "Polite customer support voice",
                Category = "Assistant",
                IsFeatured = false
            }
        };

        // Build category lookup
        var categoryDict = new Dictionary<string, List<StylePreset>>();
        foreach (var preset in _presets)
        {
            if (!string.IsNullOrWhiteSpace(preset.Category))
            {
                if (!categoryDict.ContainsKey(preset.Category))
                {
                    categoryDict[preset.Category] = new List<StylePreset>();
                }

                categoryDict[preset.Category].Add(preset);
            }
        }

        _presetsByCategory = categoryDict.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<StylePreset>)kvp.Value.AsReadOnly(),
            StringComparer.OrdinalIgnoreCase
        );

        _categories = categoryDict.Keys.OrderBy(c => c).ToList().AsReadOnly();
        _featuredPresets = _presets.Where(p => p.IsFeatured).ToList().AsReadOnly();

        _logger.LogInformation("StylePresetProvider initialized with {PresetCount} presets across {CategoryCount} categories",
            _presets.Count, _categories.Count);
    }

    /// <inheritdoc/>
    public IReadOnlyList<StylePreset> GetAllPresets()
    {
        return _presets;
    }

    /// <inheritdoc/>
    public StylePreset? GetPresetById(string presetId)
    {
        if (string.IsNullOrWhiteSpace(presetId))
        {
            return null;
        }

        return _presets.FirstOrDefault(p => p.PresetId.Equals(presetId, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc/>
    public IReadOnlyList<StylePreset> GetPresetsByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return Array.Empty<StylePreset>();
        }

        return _presetsByCategory.TryGetValue(category, out var presets)
            ? presets
            : Array.Empty<StylePreset>();
    }

    /// <inheritdoc/>
    public IReadOnlyList<StylePreset> GetFeaturedPresets()
    {
        return _featuredPresets;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetCategories()
    {
        return _categories;
    }
}
