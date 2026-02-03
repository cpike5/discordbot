using Discord;
using Discord.Interactions;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Autocomplete;

/// <summary>
/// Provides autocomplete suggestions for FVOX clip names in the /fvox command.
/// Searches the FVOX clip library based on the last word being typed.
/// </summary>
public class FvoxClipAutocompleteHandler : VoxClipAutocompleteHandler
{
    /// <summary>
    /// Generates autocomplete suggestions for FVOX clips within the current guild.
    /// Extracts the last word from the user's input and searches for matching clips.
    /// </summary>
    /// <param name="context">The interaction context for the current command execution.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction data.</param>
    /// <param name="parameter">Information about the command parameter being completed.</param>
    /// <param name="services">Service provider for resolving dependencies.</param>
    /// <returns>An AutocompletionResult containing matching clip suggestions with full message preserved.</returns>
    public override Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        return GenerateSuggestionsForGroup(
            context,
            autocompleteInteraction,
            parameter,
            services,
            VoxClipGroup.Fvox);
    }
}
