using System.Globalization;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Cutting user text for display without breaking it (UX plan E-1). <c>name[..2]</c> and
/// <c>Substring(0, 1)</c> count UTF-16 code units, so on a name that starts with an emoji or a
/// combining accent they leave half a character and the browser draws a replacement box. These
/// helpers count text elements (what a person sees as one character) instead. The script twin is
/// <c>Format.initials</c> / <c>Format.truncate</c> in <c>wwwroot/js/format.js</c>.
/// </summary>
public static class TextDisplay
{
    /// <summary>
    /// The first <paramref name="count"/> characters of a name, upper-cased: the letters in an
    /// avatar placeholder. Emoji and accented letters stay whole. Blank text gives <paramref name="fallback"/>.
    /// </summary>
    public static string Initials(string? text, int count = 2, string fallback = "?")
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return fallback;

        var taken = Take(trimmed, count);
        return taken.Length == 0 ? fallback : taken.ToUpperInvariant();
    }

    /// <summary>
    /// The first character of each of the first <paramref name="words"/> words ("Ada Lovelace" gives
    /// "AL"); a single word gives its first <paramref name="words"/> characters ("Ada" gives "AD").
    /// </summary>
    public static string WordInitials(string? text, int words = 2, string fallback = "?")
    {
        var parts = (text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return fallback;
        if (parts.Length == 1) return Initials(parts[0], words, fallback);

        var letters = string.Concat(parts.Take(words).Select(part => Take(part, 1)));
        return letters.Length == 0 ? fallback : letters.ToUpperInvariant();
    }

    /// <summary>
    /// Text cut to at most <paramref name="maxElements"/> characters (the ellipsis is added after,
    /// not counted), never inside an emoji or a letter with its accent.
    /// </summary>
    public static string Truncate(string? text, int maxElements, string ellipsis = "...")
    {
        if (string.IsNullOrEmpty(text) || maxElements < 0) return string.Empty;
        // Fast path: a string of this many code units can hold no more elements than that.
        if (text.Length <= maxElements) return text;

        var info = new StringInfo(text);
        return info.LengthInTextElements <= maxElements
            ? text
            : info.SubstringByTextElements(0, maxElements) + ellipsis;
    }

    /// <summary>The first <paramref name="count"/> text elements of <paramref name="text"/>, with no ellipsis.</summary>
    public static string Take(string? text, int count)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var info = new StringInfo(text);
        return info.SubstringByTextElements(0, Math.Min(Math.Max(count, 0), info.LengthInTextElements));
    }
}
