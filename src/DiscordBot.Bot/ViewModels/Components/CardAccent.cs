// src/DiscordBot.Bot/ViewModels/Components/CardAccent.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// Defines accent color variants for enhanced card components.
/// Each accent adds a colored gradient border at the top of the card.
/// </summary>
public enum CardAccent
{
    /// <summary>
    /// No accent color (no gradient border).
    /// </summary>
    None,

    /// <summary>
    /// Blue accent (#098ecf gradient) - typically used for general information or primary actions.
    /// </summary>
    Blue,

    /// <summary>
    /// Orange accent (#cb4e1b gradient) - typically used for important metrics or highlights.
    /// </summary>
    Orange,

    /// <summary>
    /// Success/Green accent (#10b981 gradient) - typically used for positive trends or successful operations.
    /// </summary>
    Success,

    /// <summary>
    /// Info/Cyan accent (#06b6d4 gradient) - typically used for informational content or secondary metrics.
    /// </summary>
    Info
}
