using Discord;
using Discord.Interactions;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.Vox;

namespace DiscordBot.Bot.Autocomplete;

/// <summary>
/// Provides autocomplete suggestions for VOX clip names in the /vox command.
/// Searches the VOX clip library based on the last word being typed.
/// </summary>
public class VoxClipAutocompleteHandler : AutocompleteHandler
{
    /// <summary>
    /// Generates autocomplete suggestions for VOX clips within the current guild.
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
            VoxClipGroup.Vox);
    }

    /// <summary>
    /// Generates autocomplete suggestions for a specific VOX clip group.
    /// </summary>
    /// <param name="context">The interaction context.</param>
    /// <param name="autocompleteInteraction">The autocomplete interaction data.</param>
    /// <param name="parameter">Information about the command parameter.</param>
    /// <param name="services">Service provider for resolving dependencies.</param>
    /// <param name="group">The VOX clip group to search.</param>
    /// <returns>An AutocompletionResult containing matching clip suggestions.</returns>
    protected static Task<AutocompletionResult> GenerateSuggestionsForGroup(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services,
        VoxClipGroup group)
    {
        if (context.Guild == null)
        {
            return Task.FromResult(AutocompletionResult.FromSuccess());
        }

        var clipLibrary = services.GetRequiredService<IVoxClipLibrary>();
        var userInput = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;

        // Extract the last word being typed
        var lastSpaceIndex = userInput.LastIndexOf(' ');
        var partialWord = lastSpaceIndex >= 0
            ? userInput[(lastSpaceIndex + 1)..]
            : userInput;

        var prefix = lastSpaceIndex >= 0
            ? userInput[..(lastSpaceIndex + 1)]
            : string.Empty;

        // If no partial word, return empty results
        if (string.IsNullOrWhiteSpace(partialWord))
        {
            return Task.FromResult(AutocompletionResult.FromSuccess());
        }

        // Search for clips matching the partial word
        var matchingClips = clipLibrary.SearchClips(group, partialWord, maxResults: 25);

        // Build autocomplete results with full message text preserved
        var results = matchingClips
            .Select(clip => new AutocompleteResult(
                name: prefix + clip.Name,
                value: prefix + clip.Name))
            .ToList();

        return Task.FromResult(AutocompletionResult.FromSuccess(results));
    }
}
