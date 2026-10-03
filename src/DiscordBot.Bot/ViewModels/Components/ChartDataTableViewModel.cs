using DiscordBot.Bot.Helpers;

namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model for <c>_ChartDataTable</c>: the figures behind a chart as a real table, visually
/// hidden but in the accessibility tree, so a chart is never the only way to read its numbers.
/// The canvas points at it with <c>aria-describedby</c>. Cells are plain text; the partial encodes
/// them, so names that come from guild members are safe here.
/// </summary>
public record ChartDataTableViewModel
{
    /// <summary>Element id; the chart's canvas names it in <c>aria-describedby</c>.</summary>
    public string Id { get; init; } = "chartData";

    /// <summary>What the figures are ("Messages and active members per day").</summary>
    public string Caption { get; init; } = string.Empty;

    public IReadOnlyList<string> Columns { get; init; } = Array.Empty<string>();

    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = Array.Empty<IReadOnlyList<string>>();

    private static readonly string[] DayNames = { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };

    /// <summary>A calendar day for a table cell ("Oct 1, 2026"), the same on every machine.</summary>
    public static string Day(DateTime date) => date.ToString("MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture);

    public static string Count(long value) => DisplayFormat.Number(value);

    /// <summary>
    /// A table for a day-of-week by hour-of-day heatmap: one row per weekday, one column per hour (UTC).
    /// Missing cells read 0.
    /// </summary>
    public static ChartDataTableViewModel ForHeatmap(
        string id,
        string caption,
        IEnumerable<(int DayOfWeek, int Hour, long Count)> cells)
    {
        var grid = new long[7, 24];
        foreach (var (day, hour, count) in cells)
        {
            if (day is >= 0 and <= 6 && hour is >= 0 and <= 23)
            {
                grid[day, hour] = count;
            }
        }

        var columns = new List<string> { "Day" };
        columns.AddRange(Enumerable.Range(0, 24).Select(h => $"{h:00}:00"));

        var rows = new List<IReadOnlyList<string>>();
        for (var day = 0; day < 7; day++)
        {
            var row = new List<string> { DayNames[day] };
            for (var hour = 0; hour < 24; hour++)
            {
                row.Add(Count(grid[day, hour]));
            }

            rows.Add(row);
        }

        return new ChartDataTableViewModel { Id = id, Caption = caption, Columns = columns, Rows = rows };
    }
}
