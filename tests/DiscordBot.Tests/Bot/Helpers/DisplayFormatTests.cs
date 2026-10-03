using System.Globalization;
using System.Text.Encodings.Web;
using DiscordBot.Bot.Helpers;
using FluentAssertions;
using Xunit;

namespace DiscordBot.Tests.Bot.Helpers;

public class DisplayFormatTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    // Newer ICU builds put a narrow no-break space before AM/PM.
    private static string Plain(string s) => System.Net.WebUtility.HtmlDecode(s).Replace(' ', ' ').Replace(' ', ' ');

    [Fact]
    public void ToUtc_TreatsUnspecifiedAsUtc_AndConvertsLocal()
    {
        var unspecified = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Unspecified);
        DisplayFormat.ToUtc(unspecified).Kind.Should().Be(DateTimeKind.Utc);
        DisplayFormat.ToUtc(unspecified).Hour.Should().Be(12);

        var local = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Local);
        DisplayFormat.ToUtc(local).Should().Be(local.ToUniversalTime());
    }

    [Fact]
    public void Iso_AlwaysEndsInZ()
    {
        DisplayFormat.Iso(new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Unspecified)).Should().EndWith("Z");
        DisplayFormat.Iso(Now).Should().Be("2026-10-03T12:00:00.000Z");
    }

    [Fact]
    public void Iso_HasAtMostThreeFractionalDigits_AsTimeDatetimeRequires()
    {
        var withTicks = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc).AddTicks(1234567);

        DisplayFormat.Iso(withTicks).Should().Be("2026-10-03T12:00:00.123Z");
        DisplayFormat.Iso(withTicks).Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$");
    }

    [Theory]
    [InlineData("date", "Oct 3, 2026")]
    [InlineData("date-short", "Oct 3")]
    [InlineData("datetime", "Oct 3, 2026, 6:05 PM UTC")]
    [InlineData("datetime-short", "Oct 3, 6:05 PM UTC")]
    [InlineData("time", "6:05 PM UTC")]
    [InlineData("datetime-seconds", "Oct 3, 2026, 6:05:09 PM UTC")]
    public void Date_UsesTheNamedStyle_InUtc(string style, string expected)
    {
        var value = new DateTime(2026, 10, 3, 18, 5, 9, DateTimeKind.Utc);
        Plain(DisplayFormat.Date(value, style, En)).Should().Be(expected);
    }

    [Fact]
    public void Date_FollowsTheCultureClock()
    {
        var value = new DateTime(2026, 10, 3, 18, 5, 0, DateTimeKind.Utc);
        DisplayFormat.Date(value, "time", De).Should().Be("18:05 UTC");
    }

    [Theory]
    [InlineData(-10, "now")]
    [InlineData(-50, "1 minute ago")]
    [InlineData(-300, "5 minutes ago")]
    [InlineData(-10800, "3 hours ago")]
    [InlineData(-86400, "yesterday")]
    [InlineData(-345600, "4 days ago")]
    [InlineData(7200, "in 2 hours")]
    [InlineData(86400, "tomorrow")]
    public void RelativeTime_WordsThePastAndFuture(int offsetSeconds, string expected)
    {
        DisplayFormat.RelativeTime(Now.AddSeconds(offsetSeconds), Now, En).Should().Be(expected);
    }

    [Fact]
    public void RelativeTime_NeverRoundsARealGapDownToNow()
    {
        DisplayFormat.RelativeTime(Now.AddSeconds(-50), Now, En).Should().Be("1 minute ago");
        DisplayFormat.RelativeTime(Now.AddMinutes(-70), Now, En).Should().Be("1 hour ago");
    }

    [Fact]
    public void RelativeTime_FallsBackToADateAfterThirtyDays()
    {
        DisplayFormat.RelativeTime(Now.AddDays(-45), Now, En).Should().Be("Aug 19, 2026");
    }

    [Fact]
    public void Time_RendersAnUpgradableElementWithAUtcFallback()
    {
        var html = Render(DisplayFormat.Time(new DateTime(2026, 10, 3, 18, 5, 0, DateTimeKind.Unspecified), "datetime", culture: En));

        html.Should().StartWith("<time data-utc=\"2026-10-03T18:05:00.000Z\" data-format=\"datetime\"");
        html.Should().Contain("datetime=\"2026-10-03T18:05:00.000Z\"");
        Plain(html).Should().Contain("Oct 3, 2026, 6:05 PM UTC");
    }

    [Fact]
    public void Time_Relative_UsesTheRefreshingAttribute()
    {
        var html = Render(DisplayFormat.Time(DateTime.UtcNow.AddMinutes(-5), relative: true, culture: En));

        html.Should().StartWith("<time data-relative-time=\"");
        html.Should().NotContain("data-utc");
        html.Should().Contain("5 minutes ago");
    }

    [Fact]
    public void Time_NullShowsTheEmptyText_Encoded()
    {
        Render(DisplayFormat.Time(null)).Should().NotContain("<time");
        Render(DisplayFormat.Time(null, empty: "<none>")).Should().Be("&lt;none&gt;");
    }

    [Theory]
    [InlineData(0, "0 servers")]
    [InlineData(1, "1 server")]
    [InlineData(2, "2 servers")]
    [InlineData(1234, "1,234 servers")]
    public void Plural_PicksTheFormAndGroupsTheCount(long count, string expected)
    {
        DisplayFormat.Plural(count, "server", culture: En).Should().Be(expected);
    }

    [Fact]
    public void Plural_UsesAnIrregularPlural()
    {
        DisplayFormat.Plural(1, "entry", "entries", En).Should().Be("1 entry");
        DisplayFormat.Plural(3, "entry", "entries", En).Should().Be("3 entries");
    }

    [Fact]
    public void Number_UsesTheCultureMarks()
    {
        DisplayFormat.Number(1234567.5, culture: En).Should().Be("1,234,567.5");
        DisplayFormat.Number(1234567.5, culture: De).Should().Be("1.234.567,5");
        DisplayFormat.Number(1.23456, 2, En).Should().Be("1.23");
        DisplayFormat.Number(1234.9, 0, En).Should().Be("1,235");
        DisplayFormat.Number(double.NaN, culture: En).Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, "<1s")]
    [InlineData(500, "<1s")]
    [InlineData(45_000, "45s")]
    [InlineData(90_000, "1m 30s")]
    [InlineData(1_800_000, "30m")]
    [InlineData(19_810_000, "5h 30m")]
    [InlineData(192_600_000, "2d 5h")]
    public void Duration_ShowsTheTwoLargestUnits(long ms, string expected)
    {
        DisplayFormat.Duration(TimeSpan.FromMilliseconds(ms)).Should().Be(expected);
    }

    [Fact]
    public void Duration_MaxUnits_AndNegative()
    {
        DisplayFormat.Duration(TimeSpan.FromMilliseconds(192_600_000), 3).Should().Be("2d 5h 30m");
        DisplayFormat.Duration(TimeSpan.FromSeconds(-1)).Should().BeEmpty();
    }

    [Fact]
    public void Currency_WritesTheSymbolAfterTheWholeAmount()
    {
        DisplayFormat.Currency(1250m, "🪙", culture: En).Should().Be("1,250 🪙");
        DisplayFormat.Currency(1250.9m, "GEM", culture: En).Should().Be("1,251 GEM");
        DisplayFormat.Currency(1250m, "🪙", culture: De).Should().Be("1.250 🪙");
    }

    [Fact]
    public void Currency_MatchesTheDiscordCommandWording()
    {
        DisplayFormat.Currency(5m, "🪙", culture: En).Should().Be(CurrencyFormatting.Amount(5, "🪙"));
    }

    private static string Render(Microsoft.AspNetCore.Html.IHtmlContent content)
    {
        using var writer = new StringWriter();
        content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }
}
