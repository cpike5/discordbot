using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Autocomplete;

/// <summary>
/// Provides autocomplete suggestions for sound names in the /play command.
/// Filters guild sounds based on user input and returns up to 25 matching results.
/// </summary>
public class SoundAutocompleteHandler : AutocompleteHandler
{
    /// <summary>
    /// Generates autocomplete suggestions for sound names within the current guild.
    /// Matches sounds by name in a case-insensitive manner and returns up to 25 results.
    /// </summary>
    /// <param name="context">The interaction context for the current command execution.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction data.</param>
    /// <param name="parameter">Information about the command parameter being completed.</param>
    /// <param name="services">Service provider for resolving dependencies.</param>
    /// <returns>An AutocompletionResult containing matching sound names, or empty if no guild context.</returns>
    /// <remarks>
    /// If the interaction context has no guild (e.g., direct message), returns an empty result
    /// as sounds are guild-specific. The handler retrieves all sounds for the guild and filters
    /// them based on the current user input.
    /// </remarks>
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

        var soundService = services.GetRequiredService<ISoundService>();
        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;

        // Get all sounds for the guild
        var sounds = await soundService.GetAllByGuildAsync(context.Guild.Id);

        // Filter and format results for autocomplete
        var results = sounds
            .Where(s => string.IsNullOrEmpty(userInput) ||
                       s.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Name)
            .Take(25) // Discord autocomplete result limit
            .Select(s => new AutocompleteResult(s.Name, s.Name))
            .ToList();

        return AutocompletionResult.FromSuccess(results);
    }
}
