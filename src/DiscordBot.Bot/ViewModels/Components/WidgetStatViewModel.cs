namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// One figure in a dashboard widget: a small label with an icon, and the value under it.
/// Rendered by <c>Pages/Guilds/Widgets/_WidgetStat.cshtml</c>.
/// </summary>
public record WidgetStatViewModel
{
    public string Label { get; init; } = string.Empty;

    /// <summary>The value, already formatted for display (the view encodes it).</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Rendered markup to show instead of <see cref="Value"/>, for a value that is itself an
    /// element (a <c>&lt;time&gt;</c> from <c>DisplayFormat.Time</c>).
    /// </summary>
    public Microsoft.AspNetCore.Html.IHtmlContent? ValueContent { get; init; }

    /// <summary>
    /// The value's colour: <c>default</c>, <c>blue</c>, <c>success</c>, <c>warning</c> or <c>error</c>.
    /// Colour is never the only carrier of meaning; the label says what the figure is.
    /// </summary>
    public string Tone { get; init; } = "default";

    /// <summary>SVG path for the icon beside the label.</summary>
    public string? IconPath { get; init; }

    /// <summary>Show the value in the smaller text size (words rather than a count).</summary>
    public bool Compact { get; init; }

    /// <summary>Optional second line under the value.</summary>
    public string? Detail { get; init; }

    /// <summary>Tailwind text colour class for <see cref="Tone"/>.</summary>
    public string ToneClass => Tone switch
    {
        "blue" => "text-accent-blue",
        "success" => "text-success",
        "warning" => "text-warning",
        "error" => "text-error",
        _ => "text-text-primary"
    };

    /// <summary>Tailwind text colour class for the icon.</summary>
    public string IconClass => Tone switch
    {
        "blue" => "text-accent-blue",
        "success" => "text-success",
        "warning" => "text-warning",
        "error" => "text-error",
        _ => "text-text-tertiary"
    };
}
