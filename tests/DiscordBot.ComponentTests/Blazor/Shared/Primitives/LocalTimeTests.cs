using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.ComponentTests.Blazor.Shared.Primitives;

public class LocalTimeTests : BlazorComponentTestContext
{
    private static readonly DateTime SampleUtc = new(2026, 9, 14, 18, 30, 15, DateTimeKind.Utc);

    [Fact]
    public void RendersDatetimeAttribute_AsRoundTripUtc()
    {
        var cut = Render<LocalTime>(p => p.Add(x => x.Utc, SampleUtc));

        var time = cut.Find("time");
        time.GetAttribute("datetime").Should().Be(SampleUtc.ToString("o"));
    }

    [Fact]
    public void RendersDataUtcAndDataFormat_ForClientSideConversion()
    {
        var cut = Render<LocalTime>(p => p.Add(x => x.Utc, SampleUtc).Add(x => x.Format, "datetime-short"));

        var time = cut.Find("time");
        time.GetAttribute("data-utc").Should().Be(SampleUtc.ToString("o"));
        time.GetAttribute("data-format").Should().Be("datetime-short");
    }

    [Fact]
    public void DefaultFormat_IsDatetime()
    {
        var cut = Render<LocalTime>(p => p.Add(x => x.Utc, SampleUtc));

        cut.Find("time").GetAttribute("data-format").Should().Be("datetime");
    }

    [Theory]
    [InlineData("date", "Sep 14, 2026")]
    [InlineData("date-short", "Sep 14")]
    [InlineData("datetime-short", "Sep 14, 6:30 PM")]
    [InlineData("time", "6:30 PM")]
    [InlineData("datetime-seconds", "Sep 14, 2026, 6:30:15 PM")]
    [InlineData("datetime", "Sep 14, 2026, 6:30 PM")]
    public void ServerFallbackText_MatchesFormat(string format, string expected)
    {
        var cut = Render<LocalTime>(p => p.Add(x => x.Utc, SampleUtc).Add(x => x.Format, format));

        cut.Find("time").TextContent.Should().Be(expected);
    }

    [Fact]
    public void UnspecifiedKind_IsTreatedAsUtc()
    {
        var unspecified = new DateTime(2026, 9, 14, 18, 30, 15, DateTimeKind.Unspecified);

        var cut = Render<LocalTime>(p => p.Add(x => x.Utc, unspecified));

        cut.Find("time").GetAttribute("datetime").Should().Be(SampleUtc.ToString("o"));
    }

    [Fact]
    public void Title_IsRenderedAsAttribute()
    {
        var cut = Render<LocalTime>(p => p.Add(x => x.Utc, SampleUtc).Add(x => x.Title, "3 days ago"));

        cut.Find("time").GetAttribute("title").Should().Be("3 days ago");
    }

    [Fact]
    public void Class_IsAppended()
    {
        var cut = Render<LocalTime>(p => p.Add(x => x.Utc, SampleUtc).Add(x => x.Class, "text-xs"));

        cut.Find("time").ClassList.Should().Contain("text-xs");
    }

    [Fact]
    public void AdditionalAttributes_ArePassedThrough()
    {
        var cut = Render<LocalTime>(p => p.Add(x => x.Utc, SampleUtc).AddUnmatched("data-testid", "my-time"));

        cut.Find("time[data-testid='my-time']").Should().NotBeNull();
    }
}
