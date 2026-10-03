using System.Globalization;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// How the server writes dates, counts, durations and money into markup (UX plan D6). The mirror
/// of <c>wwwroot/js/format.js</c>: same styles, same wording, same rules, so a value rendered here
/// and then upgraded by the script does not visibly jump.
/// <para>
/// The server cannot know the viewer's time zone or locale, so it never pretends to. Date helpers
/// produce a UTC fallback labelled "UTC" and put the exact instant on the element
/// (<see cref="Time"/>); <c>format.js</c> and <c>timezone.js</c> then rewrite it into the browser's
/// own zone, locale and 12/24-hour setting. Counts, numbers and durations do not depend on the
/// zone and are final as rendered. Every method takes an explicit <see cref="CultureInfo"/>
/// (default: the current request culture) so tests are deterministic.
/// </para>
/// </summary>
public static class DisplayFormat
{
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Default;

    /// <summary>Relative wording is used up to this age; older values show as a date.</summary>
    private const int RelativeLimitDays = 30;

    /// <summary>
    /// The instant as UTC. A <see cref="DateTimeKind.Unspecified"/> value is taken to be UTC: the
    /// database stores UTC, and with legacy timestamp behaviour its values come back unspecified.
    /// </summary>
    public static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };

    /// <summary>
    /// The ISO 8601 form with a zone designator (<c>2026-10-03T12:00:00.000Z</c>, milliseconds: <c>&lt;time datetime&gt;</c> allows at most three fractional digits) that scripts
    /// can parse without guessing. Use this for <c>data-utc</c> attributes instead of
    /// <c>ToString("o")</c>, which has no <c>Z</c> on an unspecified value.
    /// </summary>
    public static string Iso(DateTime value) => ToUtc(value).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    /// <summary>
    /// A UTC fallback in one of the <c>format.js</c> styles: <c>date</c>, <c>date-short</c>,
    /// <c>datetime</c> (default), <c>datetime-short</c>, <c>datetime-seconds</c>, <c>time</c>.
    /// Anything with a time of day ends in " UTC".
    /// </summary>
    public static string Date(DateTime value, string style = "datetime", CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var utc = ToUtc(value);
        var time = utc.ToString(culture.DateTimeFormat.ShortTimePattern, culture);
        var timeSeconds = utc.ToString(culture.DateTimeFormat.LongTimePattern, culture);

        return style switch
        {
            "date" => utc.ToString("MMM d, yyyy", culture),
            "date-short" => utc.ToString("MMM d", culture),
            "datetime-short" => $"{utc.ToString("MMM d", culture)}, {time} UTC",
            "datetime-seconds" => $"{utc.ToString("MMM d, yyyy", culture)}, {timeSeconds} UTC",
            "time" => $"{time} UTC",
            _ => $"{utc.ToString("MMM d, yyyy", culture)}, {time} UTC"
        };
    }

    /// <summary>
    /// How long ago (or from now) a time is, in English: "now", "5 minutes ago", "yesterday",
    /// "in 2 hours". After 30 days it falls back to a date. The script uses the viewer's language;
    /// this is the placeholder it replaces.
    /// </summary>
    public static string RelativeTime(DateTime value, DateTime? now = null, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var diff = ToUtc(value) - ToUtc(now ?? DateTime.UtcNow);
        var abs = diff.Duration();

        if (abs >= TimeSpan.FromDays(RelativeLimitDays)) return Date(value, "date", culture);
        if (abs < TimeSpan.FromSeconds(45)) return "now";

        var past = diff < TimeSpan.Zero;
        if (abs < TimeSpan.FromHours(1)) return Phrase(RoundAtLeastOne(abs.TotalMinutes), "minute", past);
        if (abs < TimeSpan.FromDays(1)) return Phrase(RoundAtLeastOne(abs.TotalHours), "hour", past);

        var days = RoundAtLeastOne(abs.TotalDays);
        if (days == 1) return past ? "yesterday" : "tomorrow";
        return Phrase(days, "day", past);
    }

    // Round to the nearer whole unit, but never to 0 (that would read as "now").
    private static long RoundAtLeastOne(double n) => Math.Max(1, (long)Math.Round(n, MidpointRounding.AwayFromZero));

    private static string Phrase(long n, string unit, bool past)
    {
        var text = n == 1 ? $"1 {unit}" : $"{n} {unit}s";
        return past ? $"{text} ago" : $"in {text}";
    }

    /// <summary>
    /// A <c>&lt;time&gt;</c> element the scripts upgrade to the viewer's zone and locale. The
    /// content is a labelled UTC fallback, so the page reads correctly before (or without) script
    /// and the swap barely changes the width.
    /// </summary>
    /// <param name="value">The instant; null renders <paramref name="empty"/>.</param>
    /// <param name="style">A <c>format.js</c> date style.</param>
    /// <param name="relative">Show "5 minutes ago" (refreshing) instead of a date.</param>
    /// <param name="empty">Text for a null value (an em dash by default).</param>
    public static IHtmlContent Time(DateTime? value, string style = "datetime", bool relative = false,
        string empty = "—", CultureInfo? culture = null)
    {
        if (value is null) return new HtmlString(Encoder.Encode(empty));

        var iso = Iso(value.Value);
        var fallback = relative ? RelativeTime(value.Value, culture: culture) : Date(value.Value, style, culture);
        var attributes = relative
            ? $"data-relative-time=\"{Encoder.Encode(iso)}\""
            : $"data-utc=\"{Encoder.Encode(iso)}\" data-format=\"{Encoder.Encode(style)}\"";

        return new HtmlString($"<time {attributes} datetime=\"{Encoder.Encode(iso)}\">{Encoder.Encode(fallback)}</time>");
    }

    /// <summary>"1 server", "2 servers": the count (grouped for the culture) and the matching word.</summary>
    /// <param name="other">The plural word; defaults to <paramref name="one"/> plus "s".</param>
    public static string Plural(long count, string one, string? other = null, CultureInfo? culture = null)
    {
        var word = count == 1 ? one : other ?? one + "s";
        return $"{Number(count, culture: culture)} {word}";
    }

    /// <summary>A number with the culture's grouping and decimal marks.</summary>
    public static string Number(double value, int? maxFractionDigits = null, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (double.IsNaN(value) || double.IsInfinity(value)) return string.Empty;

        // Default to as many digits as the value needs, which is what Intl.NumberFormat does up to
        // three; callers wanting a different cap pass one.
        var digits = Math.Max(0, maxFractionDigits ?? 3);
        var format = digits == 0 ? "#,0" : "#,0." + new string('#', digits);
        return value.ToString(format, culture);
    }

    /// <summary>
    /// A length of time as at most <paramref name="maxUnits"/> of its largest units:
    /// "2d 5h", "5h 30m", "45s", "&lt;1s".
    /// </summary>
    public static string Duration(TimeSpan value, int maxUnits = 2)
    {
        if (value < TimeSpan.Zero) return string.Empty;

        var rest = (long)Math.Floor(value.TotalSeconds);
        if (rest == 0) return "<1s";

        var parts = new List<string>();
        foreach (var (label, size) in new[] { ("d", 86400L), ("h", 3600L), ("m", 60L), ("s", 1L) })
        {
            if (parts.Count >= maxUnits) break;
            var amount = rest / size;
            if (amount > 0) parts.Add($"{amount}{label}");
            rest -= amount * size;
        }

        return string.Join(' ', parts);
    }

    /// <summary>
    /// An amount of a virtual currency: the whole-unit amount, then the symbol ("1,250 🪙"), the
    /// same as <see cref="CurrencyFormatting.Amount"/> but grouped for the viewer's culture. An ISO
    /// 4217 code is written the same way ("1,234.50 USD"): the server does not know the viewer's
    /// currency convention, and <c>Format.currency(x, code, { iso: true })</c> does.
    /// </summary>
    public static string Currency(decimal amount, string symbolOrCode, bool iso = false, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        var text = iso
            ? amount.ToString("N2", culture)
            : Math.Round(amount, MidpointRounding.AwayFromZero).ToString("N0", culture);
        return $"{text} {symbolOrCode}".Trim();
    }
}
