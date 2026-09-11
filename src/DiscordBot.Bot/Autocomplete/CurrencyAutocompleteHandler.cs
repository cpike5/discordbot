using Discord;
using Discord.Interactions;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Autocomplete;

/// <summary>
/// Autocomplete handler for the <c>currency</c> option on the wallet commands. Suggests the
/// active currencies visible in the guild — the guild's own plus every global one — and sends the
/// currency ID as the value so a rename never breaks an in-flight command.
/// </summary>
public class CurrencyAutocompleteHandler : AutocompleteHandler
{
    /// <summary>
    /// Generates up to 25 currency suggestions matching what the user has typed.
    /// </summary>
    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction autocompleteInteraction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        // The feature can be switched off entirely, in which case the service is not registered.
        var currencyService = services.GetService<ICurrencyService>();
        if (currencyService == null || context.Guild == null)
        {
            return AutocompletionResult.FromSuccess(Array.Empty<AutocompleteResult>());
        }

        var input = autocompleteInteraction.Data.Current.Value?.ToString() ?? string.Empty;

        var currencies = await currencyService.GetVisibleInGuildAsync(context.Guild.Id);

        var results = currencies
            .Where(c => string.IsNullOrEmpty(input) || c.Name.Contains(input, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name)
            .Take(25)
            .Select(c => new AutocompleteResult($"{c.Symbol} {c.Name}".Trim(), c.Id.ToString()));

        return AutocompletionResult.FromSuccess(results);
    }
}
