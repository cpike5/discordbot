using DiscordBot.Bot.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DiscordBot.Tests.Bot.Helpers;

public class FilterChipHelperTests
{
    private static HttpRequest Request(string query)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/Guilds/Analytics/1";
        context.Request.QueryString = new QueryString(query);
        return context.Request;
    }

    [Fact]
    public void DateRange_IsOneChip_ThatDropsBothDatesAndPaging()
    {
        var chips = FilterChipHelper.DateRange(Request("?StartDate=2026-11-01&EndDate=2026-11-10&tab=x&pageNumber=3"),
            new DateTime(2026, 11, 1), new DateTime(2026, 11, 10));

        var chip = chips.Should().ContainSingle().Subject;
        chip.Label.Should().Be("Dates: Nov 1, 2026 – Nov 10, 2026");
        chip.RemoveUrl.Should().Be("/Guilds/Analytics/1?tab=x");
    }

    [Fact]
    public void DateRange_IsEmpty_WhenTheUrlSetsNoRange()
    {
        // The page applies a default range on its own: that is not a filter the person chose
        FilterChipHelper.DateRange(Request(""), new DateTime(2026, 10, 1), new DateTime(2026, 10, 31)).Should().BeEmpty();
    }

    [Fact]
    public void DateRange_NamesAnOpenEnd()
    {
        var chips = FilterChipHelper.DateRange(Request("?StartDate=2026-11-01"), new DateTime(2026, 11, 1), null);

        chips.Should().ContainSingle().Which.Label.Should().Be("Dates: Nov 1, 2026 – now");
    }
}
