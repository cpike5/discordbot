using Discord;
using Discord.Interactions;
using DiscordBot.Core.Constants;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Autocomplete;

/// <summary>
/// Provides autocomplete suggestions for audio filter selection in the /play command.
/// Displays all available audio filters with their descriptions.
/// </summary>
public class FilterAutocompleteHandler : AutocompleteHandler
{
    /// <summary>
    /// Generates autocomplete suggestions for audio filters.
    /// Returns all available filters except None when no input is provided,
    /// or filters matching the user's input.
    /// </summary>
    /// <param name="context">The interaction context for the current command execution.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction data.</param>
    /// <param name="parameter">Information about the command parameter being completed.</param>
    /// <param name="services">Service provider for resolving dependencies.</param>
    /// <returns>An AutocompletionResult containing matching audio filter options.</returns>
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;

        // Get all filter definitions except None (None is the default when no filter selected)
        var results = AudioFilters.Definitions
            .Where(kvp => kvp.Key != AudioFilter.None)
            .Where(kvp => string.IsNullOrEmpty(userInput) ||
                         kvp.Value.Name.Contains(userInput, StringComparison.OrdinalIgnoreCase) ||
                         kvp.Value.Description.Contains(userInput, StringComparison.OrdinalIgnoreCase))
            .OrderBy(kvp => kvp.Value.Name)
            .Take(25) // Discord autocomplete result limit
            .Select(kvp => new AutocompleteResult(
                $"{kvp.Value.Name} - {kvp.Value.Description}",
                (int)kvp.Key))
            .ToList();

        return Task.FromResult(AutocompletionResult.FromSuccess(results));
    }
}
