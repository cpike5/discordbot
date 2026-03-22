using DiscordBot.Bot.Helpers;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Services.Search;

/// <summary>
/// Unit tests for <see cref="SearchDisplayHelper"/>.
/// </summary>
public class SearchDisplayHelperTests
{
    [Fact]
    public void Truncate_ShortText_ReturnsOriginal()
    {
        var result = SearchDisplayHelper.Truncate("hello", 50);
        result.Should().Be("hello");
    }

    [Fact]
    public void Truncate_ExactLength_ReturnsOriginal()
    {
        var text = new string('a', 50);
        var result = SearchDisplayHelper.Truncate(text, 50);
        result.Should().Be(text);
    }

    [Fact]
    public void Truncate_LongText_TruncatesWithEllipsis()
    {
        var text = new string('a', 100);
        var result = SearchDisplayHelper.Truncate(text, 50);
        result.Should().HaveLength(50);
        result.Should().EndWith("...");
    }

    [Theory]
    [InlineData("SuperAdmin", "danger")]
    [InlineData("Admin", "warning")]
    [InlineData("Moderator", "info")]
    [InlineData("Viewer", "success")]
    [InlineData("Unknown", "secondary")]
    public void GetRoleBadgeVariant_ReturnsExpectedVariant(string role, string expected)
    {
        SearchDisplayHelper.GetRoleBadgeVariant(role).Should().Be(expected);
    }

    [Theory]
    [InlineData("Security", "danger")]
    [InlineData("Configuration", "warning")]
    [InlineData("Moderation", "info")]
    [InlineData("User", "primary")]
    [InlineData("Other", "secondary")]
    public void GetAuditLogBadgeVariant_ReturnsExpectedVariant(string category, string expected)
    {
        SearchDisplayHelper.GetAuditLogBadgeVariant(category).Should().Be(expected);
    }

    [Theory]
    [InlineData("Main", "primary")]
    [InlineData("Guild", "success")]
    [InlineData("Admin", "warning")]
    [InlineData("Performance", "info")]
    [InlineData("Account", "secondary")]
    [InlineData("Dev", "dark")]
    [InlineData(null, "secondary")]
    [InlineData("Unknown", "secondary")]
    public void GetSectionBadgeVariant_ReturnsExpectedVariant(string? section, string expected)
    {
        SearchDisplayHelper.GetSectionBadgeVariant(section).Should().Be(expected);
    }

    [Fact]
    public void GetRelativeTime_FutureWithinMinute_ReturnsInLessThanAMinute()
    {
        var future = DateTime.UtcNow.AddSeconds(30);
        var result = SearchDisplayHelper.GetRelativeTime(future);
        result.Should().Be("in less than a minute");
    }

    [Fact]
    public void GetRelativeTime_PastWithinMinute_ReturnsLessThanAMinuteAgo()
    {
        var past = DateTime.UtcNow.AddSeconds(-30);
        var result = SearchDisplayHelper.GetRelativeTime(past);
        result.Should().Be("less than a minute ago");
    }

    [Fact]
    public void GetRelativeTime_FutureSeveralMinutes_ReturnsInNMinutes()
    {
        var future = DateTime.UtcNow.AddMinutes(5);
        var result = SearchDisplayHelper.GetRelativeTime(future);
        result.Should().MatchRegex(@"in [45] minutes");
    }

    [Fact]
    public void GetRelativeTime_PastSeveralMinutes_ReturnsNMinutesAgo()
    {
        var past = DateTime.UtcNow.AddMinutes(-5);
        var result = SearchDisplayHelper.GetRelativeTime(past);
        result.Should().Be("5 minutes ago");
    }

    [Fact]
    public void GetRelativeTime_FutureSeveralDays_ReturnsInNDays()
    {
        var future = DateTime.UtcNow.AddDays(3);
        var result = SearchDisplayHelper.GetRelativeTime(future);
        result.Should().MatchRegex(@"in [23] days");
    }
}
