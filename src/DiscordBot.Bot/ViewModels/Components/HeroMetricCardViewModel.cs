// src/DiscordBot.Bot/ViewModels/Components/HeroMetricCardViewModel.cs
namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for hero metric card component displaying key statistics with visual emphasis.
/// Hero metric cards feature large values, trend indicators, optional sparkline charts,
/// and color-coded accents for quick recognition.
/// </summary>
/// <remarks>
/// Example usage:
/// <code>
/// var viewModel = new HeroMetricCardViewModel
/// {
///     Title = "Total Servers",
///     Value = "12",
///     TrendValue = "+2",
///     TrendDirection = TrendDirection.Up,
///     TrendLabel = "this week",
///     AccentColor = CardAccent.Blue,
///     IconSvg = "&lt;path d=\"M5 12h14...\" /&gt;",
///     ShowSparkline = true,
///     SparklineData = new List&lt;int&gt; { 40, 65, 55, 70, 80, 75, 100 }
/// };
/// </code>
/// </remarks>
public record HeroMetricCardViewModel
{
    /// <summary>
    /// Metric title/label displayed above the value.
    /// Example: "Total Servers", "Active Users", "Commands Today"
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Large metric value to display prominently (3xl font).
    /// Can be a number, percentage, or any formatted string.
    /// Examples: "12", "1,847", "99.9%", "3.2K"
    /// </summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>
    /// Trend change value with sign indicator.
    /// Examples: "+2", "-5%", "0%", "+124"
    /// </summary>
    public string TrendValue { get; init; } = string.Empty;

    /// <summary>
    /// Direction of the trend (Up, Down, or Neutral).
    /// Controls the arrow icon and color coding:
    /// - Up: Green with upward arrow
    /// - Down: Red with downward arrow
    /// - Neutral: Gray with horizontal line
    /// </summary>
    public TrendDirection TrendDirection { get; init; } = TrendDirection.Neutral;

    /// <summary>
    /// Contextual label for the trend period.
    /// Examples: "this week", "today", "vs yesterday", "14 days stable"
    /// </summary>
    public string TrendLabel { get; init; } = string.Empty;

    /// <summary>
    /// Accent color for gradient top border and icon badge background.
    /// Available colors: Blue, Orange, Success (green), Info (cyan)
    /// </summary>
    public CardAccent AccentColor { get; init; } = CardAccent.Blue;

    /// <summary>
    /// SVG path data for the icon displayed in the top-right badge.
    /// Should be the inner &lt;path&gt; or &lt;circle&gt; elements only (without &lt;svg&gt; wrapper).
    /// Example: "&lt;path stroke-linecap=\"round\" d=\"M5 12h14...\" /&gt;"
    /// </summary>
    public string IconSvg { get; init; } = string.Empty;

    /// <summary>
    /// Whether to display the mini sparkline chart at the bottom of the card.
    /// Default: false
    /// </summary>
    public bool ShowSparkline { get; init; } = false;

    /// <summary>
    /// Sparkline bar height values (0-100 representing percentage of chart height).
    /// Each value creates a vertical bar in the sparkline visualization.
    /// Typically 7-12 values work well for visual balance.
    /// Example: new List&lt;int&gt; { 40, 65, 55, 70, 80, 75, 100 }
    /// </summary>
    public List<int> SparklineData { get; init; } = new();

    /// <summary>
    /// ID attribute for the card element (useful for testing or JavaScript targeting).
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// Additional CSS classes to apply to the card container.
    /// </summary>
    public string? CssClass { get; init; }

    /// <summary>
    /// Data attribute name for real-time updates (e.g., "data-total-commands").
    /// When specified, this attribute is added to the value element to enable SignalR real-time updates.
    /// </summary>
    public string? DataAttribute { get; init; }
}
