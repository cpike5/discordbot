using DiscordBot.Bot.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DiscordBot.Tests.Bot.Extensions;

/// <summary>
/// Unit tests for the URL-building helpers behind <see cref="LegacyRedirectExtensions.MapLegacyRouteRedirects"/>.
/// Covers the tab-selection scheme used when redirecting retired Performance and
/// Logs pages to their unified replacements.
/// </summary>
public class LegacyRedirectExtensionsTests
{
    #region BuildPerformanceTabUrl

    [Theory]
    [InlineData("system", "/Admin/Performance#system")]
    [InlineData("health", "/Admin/Performance#health")]
    [InlineData("commands", "/Admin/Performance#commands")]
    [InlineData("api", "/Admin/Performance#api")]
    [InlineData("alerts", "/Admin/Performance#alerts")]
    public void BuildPerformanceTabUrl_ReturnsUnifiedDashboardUrlWithHash(string tabId, string expected)
    {
        // Act
        var result = LegacyRedirectExtensions.BuildPerformanceTabUrl(tabId);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region BuildLogsTabUrl

    [Fact]
    public void BuildLogsTabUrl_NoExistingQuery_ReturnsTabOnlyQueryString()
    {
        // Arrange
        var query = new QueryString();

        // Act
        var result = LegacyRedirectExtensions.BuildLogsTabUrl("audit", query);

        // Assert
        result.Should().Be("/Admin/Logs?tab=audit");
    }

    [Fact]
    public void BuildLogsTabUrl_MessagesTabNoExistingQuery_ReturnsMessagesTabQueryString()
    {
        // Arrange
        var query = new QueryString();

        // Act
        var result = LegacyRedirectExtensions.BuildLogsTabUrl("messages", query);

        // Assert
        result.Should().Be("/Admin/Logs?tab=messages");
    }

    [Fact]
    public void BuildLogsTabUrl_ExistingQuery_PreservesQueryStringAndAppendsTab()
    {
        // Arrange
        var query = new QueryString("?SearchTerm=foo&GuildId=123");

        // Act
        var result = LegacyRedirectExtensions.BuildLogsTabUrl("audit", query);

        // Assert
        result.Should().Be("/Admin/Logs?SearchTerm=foo&GuildId=123&tab=audit");
    }

    #endregion
}
