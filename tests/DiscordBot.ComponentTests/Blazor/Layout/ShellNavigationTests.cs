using DiscordBot.Bot.Blazor.Layout;
using FluentAssertions;
using Microsoft.AspNetCore.Components;

namespace DiscordBot.ComponentTests.Blazor.Layout;

/// <summary>
/// Plain xUnit (no bUnit needed) coverage for <see cref="ShellNavigation"/>'s pure string-matching
/// rules - see the class's own remarks for why it is written this way.
/// </summary>
public class ShellNavigationTests
{
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager(string uri)
        {
            Initialize("https://example.test/", uri);
        }
    }

    [Theory]
    [InlineData("https://example.test/", "/")]
    [InlineData("https://example.test/Admin/Settings", "/Admin/Settings")]
    [InlineData("https://example.test/Admin/Settings?tab=ai", "/Admin/Settings")]
    [InlineData("https://example.test/Guilds/123/Members#top", "/Guilds/123/Members")]
    [InlineData("https://example.test/Guilds/", "/Guilds")]
    public void GetCurrentPath_StripsQueryFragmentAndTrailingSlash(string uri, string expected)
    {
        var navigation = new TestNavigationManager(uri);

        ShellNavigation.GetCurrentPath(navigation).Should().Be(expected);
    }

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/admin/settings", "/Admin/Settings")]
    [InlineData("/Admin/Settings/", "/Admin/Settings")]
    public void IsActive_ExactMatch_IsCaseInsensitiveAndIgnoresTrailingSlash(string currentPath, string exact)
    {
        ShellNavigation.IsActive(currentPath, exact: [exact]).Should().BeTrue();
    }

    [Fact]
    public void IsActive_ExactMatch_DoesNotMatchAPrefixOfALongerPath()
    {
        ShellNavigation.IsActive("/Admin/Settings/Sub", exact: ["/Admin/Settings"]).Should().BeFalse();
    }

    [Theory]
    [InlineData("/Guilds", "/Guilds")]
    [InlineData("/guilds/123", "/Guilds")]
    [InlineData("/Admin/Logs/Export", "/Admin/Logs")]
    public void IsActive_PrefixMatch_IsCaseInsensitive(string currentPath, string prefix)
    {
        ShellNavigation.IsActive(currentPath, prefixes: [prefix]).Should().BeTrue();
    }

    [Fact]
    public void IsActive_PrefixMatch_DoesNotMatchAnUnrelatedPath()
    {
        ShellNavigation.IsActive("/Commands", prefixes: ["/CommandLogs"]).Should().BeFalse();
    }

    [Fact]
    public void IsActive_MatchesAnyOfMultiplePrefixes()
    {
        ShellNavigation.IsActive(
            "/Admin/MessageLogs/Details/5",
            prefixes: ["/Admin/Logs", "/Admin/MessageLogs/Details", "/Admin/AuditLogs/Details"])
            .Should().BeTrue();
    }

    [Fact]
    public void IsActive_WithNeitherExactNorPrefixes_IsFalse()
    {
        ShellNavigation.IsActive("/Anything").Should().BeFalse();
    }

    [Fact]
    public void IsActive_UnrelatedPath_MatchesNeitherExactNorPrefix()
    {
        ShellNavigation.IsActive("/Guilds", exact: ["/Admin/Settings"], prefixes: ["/Commands"])
            .Should().BeFalse();
    }
}
