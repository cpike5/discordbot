using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Autocomplete;

/// <summary>
/// Autocomplete handler that provides suggestions for mod tag names based on all available tags in the guild.
/// </summary>
public class ModTagAutocompleteHandler : AutocompleteHandler
{
    /// <summary>
    /// Generates autocomplete suggestions for mod tag names.
    /// Returns up to 25 tags that match the user's input.
    /// </summary>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var tagService = services.GetRequiredService<IModTagService>();
        var input = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        // Get all guild tags
        var tags = await tagService.GetGuildTagsAsync(context.Guild.Id);

        // Filter based on user input (case-insensitive)
        var results = tags
            .Where(t => string.IsNullOrEmpty(input) ||
                       t.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.Name)
            .Take(25)
            .Select(t => new AutocompleteResult(t.Name, t.Name));

        return AutocompletionResult.FromSuccess(results);
    }
}
