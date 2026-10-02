namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Guards CSV exports against formula injection. A cell that starts with <c>=</c>,
/// <c>+</c>, <c>-</c>, <c>@</c>, a tab or a carriage return is run as a formula by
/// spreadsheet apps, so user-controlled text (names, messages, audit details) is
/// prefixed with a single quote, which makes the app treat it as text.
/// </summary>
public static class CsvField
{
    private static readonly char[] FormulaPrefixes = { '=', '+', '-', '@', '\t', '\r' };

    /// <summary>
    /// Returns <paramref name="value"/> with a leading <c>'</c> when it would otherwise be
    /// read as a formula. Quoting and quote-doubling are left to the caller.
    /// </summary>
    /// <param name="value">The cell text.</param>
    public static string NeutralizeFormula(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return Array.IndexOf(FormulaPrefixes, value[0]) >= 0 ? "'" + value : value;
    }
}
