using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Extensions;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Pages.Guilds.RatWatch;

/// <summary>
/// How Rat Watch values read on screen: status words (never enum names), the time zone select
/// that keeps a value it does not know, and the text version of a heatmap.
/// </summary>
public class RatWatchDisplayTests
{
    [Theory]
    [InlineData(RatWatchStatus.ClearedEarly, "Cleared early")]
    [InlineData(RatWatchStatus.NotGuilty, "Not guilty")]
    [InlineData(RatWatchStatus.Voting, "Voting")]
    public void DisplayName_IsPlainWords(RatWatchStatus status, string expected)
    {
        status.DisplayName().Should().Be(expected);
    }

    [Fact]
    public void EveryStatus_HasADisplayNameThatIsNotItsEnumName()
    {
        foreach (var status in Enum.GetValues<RatWatchStatus>())
        {
            var name = status.DisplayName();
            name.Should().NotBe("Unknown", $"{status} needs a name");
            name.Should().NotMatch("*[a-z][A-Z]*", "no run-together enum words");
        }
    }

    [Fact]
    public void ItemStatusText_UsesTheSameWords()
    {
        new RatWatchItemViewModel { Status = RatWatchStatus.ClearedEarly }.StatusText.Should().Be("Cleared early");
    }

    [Fact]
    public void TimezoneOptions_ForAKnownZone_AreTheFiveFixedChoices()
    {
        var options = new RatWatchIndexViewModel { Timezone = "Central Standard Time" }.TimezoneOptions;

        options.Select(o => o.Value).Should().Equal(
            "Eastern Standard Time", "Central Standard Time", "Mountain Standard Time", "Pacific Standard Time", "UTC");
    }

    [Fact]
    public void TimezoneOptions_KeepAStoredZoneTheListDoesNotKnow()
    {
        var options = new RatWatchIndexViewModel { Timezone = "America/Toronto" }.TimezoneOptions;

        options.Should().HaveCount(6);
        options[0].Value.Should().Be("America/Toronto", "saving the form must not turn it into another zone");
        options[0].Text.Should().Contain("America/Toronto");
    }

    [Fact]
    public void HeatmapTable_HasAWeekdayRowPer7Days_And24HourColumns()
    {
        var table = ChartDataTableViewModel.ForHeatmap("h", "Heat", new (int, int, long)[] { (5, 20, 3), (9, 99, 7) });

        table.Columns.Should().HaveCount(25);
        table.Columns[1].Should().Be("00:00");
        table.Columns[24].Should().Be("23:00");
        table.Rows.Should().HaveCount(7);
        table.Rows[5][0].Should().Be("Friday");
        table.Rows[5][21].Should().Be("3", "hour 20 is the 21st value after the day name");
        table.Rows.SelectMany(r => r.Skip(1)).Count(c => c != "0").Should().Be(1, "an out-of-range cell is ignored");
    }

    [Fact]
    public void DateRangePresets_AreKeysTheScriptUnderstands()
    {
        DateRangePreset.Standard.Select(p => p.Key).Should().Equal("today", "7days", "30days");
        DateRangePreset.Long.Select(p => p.Key).Should().Equal("7days", "30days", "90days");
    }
}
