using Bunit;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Shared.Forms;

public class DateRangeFilterTests : BlazorComponentTestContext
{
    [Fact]
    public void Start_And_End_RenderAsDateInputValues()
    {
        var cut = Render<DateRangeFilter>(p => p
            .Add(x => x.Id, "dr")
            .Add(x => x.Start, new DateOnly(2026, 1, 1))
            .Add(x => x.End, new DateOnly(2026, 1, 31)));

        cut.Find("#dr-start").GetAttribute("value").Should().Be("2026-01-01");
        cut.Find("#dr-end").GetAttribute("value").Should().Be("2026-01-31");
    }

    [Fact]
    public void TodayPreset_SetsStartAndEndToToday_AndFiresCallbacks()
    {
        DateOnly? start = null;
        DateOnly? end = null;
        var changedCount = 0;
        var cut = Render<DateRangeFilter>(p => p
            .Add(x => x.Id, "dr")
            .Add(x => x.StartChanged, EventCallback.Factory.Create<DateOnly?>(this, v => start = v))
            .Add(x => x.EndChanged, EventCallback.Factory.Create<DateOnly?>(this, v => end = v))
            .Add(x => x.OnChanged, EventCallback.Factory.Create(this, () => changedCount++)));

        cut.Find("button").Click(); // "Today" is the first preset button

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        start.Should().Be(today);
        end.Should().Be(today);
        changedCount.Should().Be(1);
    }

    [Fact]
    public void Last7DaysPreset_SetsStartTo7DaysAgo()
    {
        DateOnly? start = null;
        var cut = Render<DateRangeFilter>(p => p
            .Add(x => x.Id, "dr")
            .Add(x => x.StartChanged, EventCallback.Factory.Create<DateOnly?>(this, v => start = v)));

        cut.FindAll("button")[1].Click(); // "Last 7 Days"

        start.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7));
    }

    [Fact]
    public void ActivePreset_HighlightsMatchingButton()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var cut = Render<DateRangeFilter>(p => p.Add(x => x.Id, "dr").Add(x => x.Start, today).Add(x => x.End, today));

        cut.FindAll("button")[0].ClassList.Should().Contain("bg-accent-blue");
        cut.FindAll("button")[1].ClassList.Should().NotContain("bg-accent-blue");
    }

    [Fact]
    public void ChangingStartInput_UpdatesStart_AndFiresOnChanged()
    {
        DateOnly? captured = null;
        var changedCount = 0;
        var cut = Render<DateRangeFilter>(p => p
            .Add(x => x.Id, "dr")
            .Add(x => x.StartChanged, EventCallback.Factory.Create<DateOnly?>(this, v => captured = v))
            .Add(x => x.OnChanged, EventCallback.Factory.Create(this, () => changedCount++)));

        cut.Find("#dr-start").Change("2026-03-15");

        captured.Should().Be(new DateOnly(2026, 3, 15));
        changedCount.Should().Be(1);
    }

    [Fact]
    public void ClearButton_OnlyRendersWhenADateIsSet()
    {
        var cut = Render<DateRangeFilter>(p => p.Add(x => x.Id, "dr"));
        cut.FindAll("button").Should().HaveCount(3); // just the three presets, no Clear

        var cutWithDates = Render<DateRangeFilter>(p => p.Add(x => x.Id, "dr").Add(x => x.Start, DateOnly.FromDateTime(DateTime.UtcNow)));
        cutWithDates.FindAll("button").Should().HaveCount(4);
    }

    [Fact]
    public void ClearButton_ClearsBothDates()
    {
        DateOnly? start = new DateOnly(2026, 1, 1);
        DateOnly? end = new DateOnly(2026, 1, 31);
        var cut = Render<DateRangeFilter>(p => p
            .Add(x => x.Id, "dr")
            .Add(x => x.Start, start)
            .Add(x => x.End, end)
            .Add(x => x.StartChanged, EventCallback.Factory.Create<DateOnly?>(this, v => start = v))
            .Add(x => x.EndChanged, EventCallback.Factory.Create<DateOnly?>(this, v => end = v)));

        cut.FindAll("button").Last().Click(); // Clear

        start.Should().BeNull();
        end.Should().BeNull();
    }
}
