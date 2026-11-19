using Discord;
using Discord.Interactions;
using DiscordBot.Bot.Commands;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.Vox;

namespace DiscordBot.Bot.Autocomplete;

/// <summary>
/// Provides autocomplete suggestions for clip names in the <c>/vox</c> command.
/// Searches the clip library chosen by the command's <c>voice</c> option (VOX when it is not set)
/// based on the last word being typed.
/// </summary>
public class VoxClipAutocompleteHandler : AutocompleteHandler
{
    /// <summary>
    /// The name of the <c>/vox</c> option that selects the clip library.
    /// </summary>
    public const string VoiceOptionName = "voice";

    /// <summary>
    /// Generates autocomplete suggestions for clips within the current guild.
    /// Extracts the last word from the user's input and searches for matching clips
    /// in the library selected by the <c>voice</c> option.
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
        if (context.Guild == null)
        {
            return Task.FromResult(AutocompletionResult.FromSuccess());
        }

        var group = ResolveGroup(autocompleteInteraction.Data.Options?.Select(o => (o.Name, o.Value)));
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

    /// <summary>
    /// Reads the clip library from the <c>voice</c> option the user has already filled in.
    /// </summary>
    /// <param name="options">The name and value of each option sent with the autocomplete interaction.</param>
    /// <returns>The selected clip group, or <see cref="VoxClipGroup.Vox"/> when none is chosen.</returns>
    public static VoxClipGroup ResolveGroup(IEnumerable<(string Name, object? Value)>? options)
    {
        var voice = options?
            .FirstOrDefault(o => string.Equals(o.Name, VoiceOptionName, StringComparison.OrdinalIgnoreCase))
            .Value?
            .ToString();

        return VoxModule.ParseVoice(voice);
    }
}
