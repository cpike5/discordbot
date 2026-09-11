namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>
/// One search result for <see cref="Autocomplete"/> - the Blazor equivalent of the JSON objects
/// <c>autocomplete.js</c>'s <c>renderItem</c> reads (<c>id</c>, <c>displayText</c>, and an optional
/// <c>channelType</c> shown as a trailing meta label). The component has no HTTP client of its
/// own (see <see cref="Autocomplete.SearchFunc"/>'s doc) - the caller's search function returns
/// these directly instead of the component deserializing an API response itself.
/// </summary>
/// <param name="Id">The value stored when this item is selected (what the original's hidden input
/// held) - opaque to the component, round-tripped back through <see cref="Autocomplete.Value"/>.</param>
/// <param name="Text">Display text shown in the result list and copied into the search box on
/// selection.</param>
/// <param name="Description">Optional trailing meta text (the channel type, in the ported
/// endpoints) shown after <paramref name="Text"/>.</param>
public sealed record AutocompleteItem(string Id, string Text, string? Description = null);
