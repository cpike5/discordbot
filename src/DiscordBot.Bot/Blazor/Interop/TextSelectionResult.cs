namespace DiscordBot.Bot.Blazor.Interop;

/// <summary>
/// A textarea's current selection, as read by <see cref="BrowserInterop.GetSelectionAsync"/>.
/// </summary>
/// <param name="Start">Selection start offset, in UTF-16 code units.</param>
/// <param name="End">Selection end offset, in UTF-16 code units.</param>
/// <param name="Value">The selected text (empty when the caret has no selection).</param>
public sealed record TextSelectionResult(int Start, int End, string Value);

/// <summary>
/// The result of registering a <c>matchMedia</c> watch via
/// <see cref="BrowserInterop.MatchMediaAsync{TComponent}"/>.
/// </summary>
/// <param name="Handle">Pass to <see cref="BrowserInterop.UnwatchMediaAsync"/> to stop watching.</param>
/// <param name="Matches">Whether the query matched at the moment it was registered.</param>
public sealed record MediaWatchResult(int Handle, bool Matches);
