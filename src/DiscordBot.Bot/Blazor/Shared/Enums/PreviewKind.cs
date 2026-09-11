namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>Which preview <see cref="PreviewPopover{TModel}"/> is showing - drives the popup
/// width class, aria-label, and loading/error copy that <c>preview-popup.js</c> hardcoded per
/// <c>data-preview-type</c> value ("user" vs "guild").</summary>
public enum PreviewKind
{
    User,
    Guild
}
