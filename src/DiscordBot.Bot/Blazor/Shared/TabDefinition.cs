namespace DiscordBot.Bot.Blazor.Shared;

/// <summary>
/// Describes a single tab rendered by <see cref="TabbedFormShell"/>: a stable id,
/// a display label, and an optional Heroicon outline path for the tab button.
/// </summary>
/// <param name="Id">Stable identifier used for the active-tab state and element ids.</param>
/// <param name="Label">Human-readable tab label.</param>
/// <param name="IconPath">Optional SVG path (Heroicons outline) shown before the label.</param>
public sealed record TabDefinition(string Id, string Label, string? IconPath = null);
