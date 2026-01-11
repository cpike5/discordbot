using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Autocomplete;

/// <summary>
/// Provides autocomplete suggestions for TTS voice names in the /tts command.
/// Filters available voices based on user input and returns up to 25 matching results.
/// </summary>
public class VoiceAutocompleteHandler : AutocompleteHandler
{
    /// <summary>
    /// Generates autocomplete suggestions for TTS voice names.
    /// Matches voices by display name or short name in a case-insensitive manner.
    /// </summary>
    /// <param name="context">The interaction context for the current command execution.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction data.</param>
    /// <param name="parameter">Information about the command parameter being completed.</param>
    /// <param name="services">Service provider for resolving dependencies.</param>
    /// <returns>An AutocompletionResult containing matching voice names, or empty if none available.</returns>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        // Guard against non-guild contexts (direct messages, etc.)
        if (context.Guild == null)
        {
            return AutocompletionResult.FromSuccess();
        }

        var ttsService = services.GetRequiredService<ITtsService>();
        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;

        // Check if TTS service is configured
        if (!ttsService.IsConfigured)
        {
            return AutocompletionResult.FromSuccess(new[]
            {
                new AutocompleteResult("TTS service not configured", "")
            });
        }

        // Get available voices (en-US by default)
        var voices = await ttsService.GetAvailableVoicesAsync();

        // Filter and format results for autocomplete
        var results = voices
            .Where(v => string.IsNullOrEmpty(userInput) ||
                       v.DisplayName.Contains(userInput, StringComparison.OrdinalIgnoreCase) ||
                       v.ShortName.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .OrderBy(v => v.DisplayName)
            .Take(25) // Discord autocomplete result limit
            .Select(v => new AutocompleteResult($"{v.DisplayName} ({v.Gender})", v.ShortName))
            .ToList();

        return AutocompletionResult.FromSuccess(results);
    }
}
