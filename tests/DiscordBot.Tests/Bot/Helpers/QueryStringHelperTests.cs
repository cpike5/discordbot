using DiscordBot.Bot.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace DiscordBot.Tests.Bot.Helpers;

public class QueryStringHelperTests
{
    private static IQueryCollection Query(string query) => new QueryCollection(Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(query));

    [Fact]
    public void Without_DropsTheNamedKey_AndKeepsTheOthers()
    {
        var url = QueryStringHelper.Without("/Guilds/1/Members", Query("?SearchTerm=ada&ActivityFilter=active-week&SortBy=JoinedAt"), "ActivityFilter");

        url.Should().Be("/Guilds/1/Members?SearchTerm=ada&SortBy=JoinedAt");
    }

    [Fact]
    public void Without_ResetsPaging()
    {
        var url = QueryStringHelper.Without("/Members", Query("?SearchTerm=ada&pageNumber=4"), "SearchTerm");

        url.Should().Be("/Members");
    }

    [Fact]
    public void Without_ResetsPaging_EvenWhenTheRemovedKeyIsUnrelated()
    {
        var url = QueryStringHelper.Without("/Members", Query("?SearchTerm=ada&PAGENUMBER=4"), "JoinedAfter");

        url.Should().Be("/Members?SearchTerm=ada");
    }

    [Fact]
    public void Without_KeepsRepeatedKeysItWasNotAskedToDrop()
    {
        var url = QueryStringHelper.Without("/Members", Query("?RoleFilter=1&RoleFilter=2&JoinedAfter=2026-01-01"), "JoinedAfter");

        url.Should().Be("/Members?RoleFilter=1&RoleFilter=2");
    }

    [Fact]
    public void Without_DropsEveryValueOfARepeatedKey_AndSeveralKeysAtOnce()
    {
        var url = QueryStringHelper.Without("/Analytics", Query("?StartDate=2026-01-01&EndDate=2026-01-31&tab=x&tab=y"), "StartDate", "EndDate", "tab");

        url.Should().Be("/Analytics");
    }

    [Fact]
    public void Without_MatchesKeysIgnoringCase()
    {
        var url = QueryStringHelper.Without("/Members", Query("?searchterm=ada&Role=1"), "SearchTerm");

        url.Should().Be("/Members?Role=1");
    }

    [Fact]
    public void Without_EncodesValues()
    {
        var url = QueryStringHelper.Without("/Members", Query("?SearchTerm=a%26b%20c&x=1"), "x");

        url.Should().Be("/Members?SearchTerm=a%26b%20c");
    }

    [Fact]
    public void Without_UsesThePathBaseAndPathOfTheRequest()
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = "/portal";
        context.Request.Path = "/Guilds/1/Members";
        context.Request.QueryString = new QueryString("?SearchTerm=ada&JoinedAfter=2026-01-01");

        QueryStringHelper.Without(context.Request, "JoinedAfter").Should().Be("/portal/Guilds/1/Members?SearchTerm=ada");
    }

    [Fact]
    public void WithoutValue_DropsOnlyThatValueOfARepeatedKey()
    {
        var url = QueryStringHelper.WithoutValue("/Members", Query("?RoleFilter=1&RoleFilter=2&RoleFilter=3&pageNumber=2"), "RoleFilter", "2");

        url.Should().Be("/Members?RoleFilter=1&RoleFilter=3");
    }

    [Fact]
    public void WithoutValue_OfTheLastValue_RemovesTheKey()
    {
        var url = QueryStringHelper.WithoutValue("/Members", Query("?RoleFilter=1&SearchTerm=ada"), "RoleFilter", "1");

        url.Should().Be("/Members?SearchTerm=ada");
    }

    [Fact]
    public void Pairs_ListsEveryValue_WithoutTheExcludedKeysOrPaging()
    {
        var pairs = QueryStringHelper.Pairs(Query("?SearchTerm=ada&RoleFilter=1&RoleFilter=2&pageNumber=3"), "SearchTerm");

        pairs.Should().Equal(
            new KeyValuePair<string, string>("RoleFilter", "1"),
            new KeyValuePair<string, string>("RoleFilter", "2"));
    }
}
