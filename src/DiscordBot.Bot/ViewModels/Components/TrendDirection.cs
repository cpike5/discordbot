// src/DiscordBot.Bot/ViewModels/Components/TrendDirection.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// Defines the direction of a trend indicator in metric displays.
/// Used in HeroMetricCard to show whether a metric is increasing, decreasing, or stable.
/// </summary>
public enum TrendDirection
{
    /// <summary>
    /// Upward trend (positive change) - displayed with green color and up arrow.
    /// Example: "+124 users today"
    /// </summary>
    Up,

    /// <summary>
    /// Downward trend (negative change) - displayed with red color and down arrow.
    /// Example: "-15% from yesterday"
    /// </summary>
    Down,

    /// <summary>
    /// No change or neutral trend - displayed with gray color and horizontal line.
    /// Example: "0% vs yesterday"
    /// </summary>
    Neutral
}
