using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Autocomplete;

/// <summary>
/// Autocomplete handler that provides suggestions for mod tag names based on tags assigned to a specific user.
/// Used when removing tags from a user - only shows tags the user actually has.
/// </summary>
public class UserModTagAutocompleteHandler : AutocompleteHandler
{
    /// <summary>
    /// Generates autocomplete suggestions for mod tag names assigned to the target user.
    /// Returns up to 25 tags that match the user's input from the tags assigned to the specified user.
    /// </summary>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var tagService = services.GetRequiredService<IModTagService>();
        var input = autocompleteInteraction.Data.Current.Value?.ToString() ?? "";

        // Try to get the target user from the command options
        var userOption = autocompleteInteraction.Data.Options.FirstOrDefault(o => o.Name == "user");
        if (userOption?.Value is not ulong userId)
        {
            // No user specified yet, return empty results
            return AutocompletionResult.FromSuccess(Array.Empty<AutocompleteResult>());
        }

        // Get tags assigned to this specific user
        var userTags = await tagService.GetUserTagsAsync(context.Guild.Id, userId);

        // Filter based on user input (case-insensitive)
        var results = userTags
            .Where(t => string.IsNullOrEmpty(input) ||
                       t.TagName.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.TagName)
            .Take(25)
            .Select(t => new AutocompleteResult(t.TagName, t.TagName));

        return AutocompletionResult.FromSuccess(results);
    }
}
