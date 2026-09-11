namespace DiscordBot.Core.Enums;

/// <summary>
/// How often a currency pays automatic income.
/// <para>
/// Groundwork only: nothing in this project reads it. A future income job will.
/// </para>
/// </summary>
public enum IncomeInterval
{
    /// <summary>Once a day.</summary>
    Daily = 0,

    /// <summary>Once a week.</summary>
    Weekly = 1,

    /// <summary>Once a month.</summary>
    Monthly = 2
}
