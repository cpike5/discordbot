using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Autocomplete;

/// <summary>
/// Provides autocomplete suggestions for style presets in the /tts-styled command.
/// Filters available presets based on user input and returns up to 25 matching results.
/// </summary>
public class StylePresetAutocompleteHandler : AutocompleteHandler
{
    /// <summary>
    /// Generates autocomplete suggestions for style preset IDs.
    /// Matches presets by preset ID, display name, or category in a case-insensitive manner.
    /// Featured presets are prioritized in the results.
    /// </summary>
    /// <param name="context">The interaction context for the current command execution.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction data.</param>
    /// <param name="parameter">Information about the command parameter being completed.</param>
    /// <param name="services">Service provider for resolving dependencies.</param>
    /// <returns>An AutocompletionResult containing matching preset names, or empty if none available.</returns>
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        // Guard against non-guild contexts (direct messages, etc.)
        if (context.Guild == null)
        {
            return Task.FromResult(AutocompletionResult.FromSuccess());
        }

        var presetProvider = services.GetRequiredService<IStylePresetProvider>();
        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;

        // Get all presets
        var allPresets = presetProvider.GetAllPresets();

        // Filter and format results for autocomplete
        var results = allPresets
            .Where(p => string.IsNullOrEmpty(userInput) ||
                       p.PresetId.Contains(userInput, StringComparison.OrdinalIgnoreCase) ||
                       p.DisplayName.Contains(userInput, StringComparison.OrdinalIgnoreCase) ||
                       (p.Category != null && p.Category.Contains(userInput, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(p => p.IsFeatured) // Featured presets first
            .ThenBy(p => p.Category ?? string.Empty)
            .ThenBy(p => p.DisplayName)
            .Take(25) // Discord autocomplete result limit
            .Select(p =>
            {
                var displayText = p.Category != null
                    ? $"{p.Category}: {p.DisplayName}"
                    : p.DisplayName;
                return new AutocompleteResult(displayText, p.PresetId);
            })
            .ToList();

        return Task.FromResult(AutocompletionResult.FromSuccess(results));
    }
}
