using DiscordBot.Bot.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace DiscordBot.Tests.Bot.Extensions;

/// <summary>
/// Unit tests for the URL-building helpers behind <see cref="LegacyRedirectExtensions.MapLegacyRouteRedirects"/>.
/// Covers the tab-selection scheme used when redirecting retired Performance and
/// Logs pages to their unified replacements, including query-string preservation.
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
    public void BuildPerformanceTabUrl_NoQueryString_ReturnsUnifiedDashboardUrlWithHash(string tabId, string expected)
    {
        // Act
        var result = LegacyRedirectExtensions.BuildPerformanceTabUrl(tabId);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void BuildPerformanceTabUrl_ExistingQuery_PreservesQueryStringBeforeHash()
    {
        // Arrange - the bug this guards: /Admin/Performance/Commands?hours=720 used to lose
        // its time range entirely because the query string was dropped.
        var query = new QueryString("?hours=720");

        // Act
        var result = LegacyRedirectExtensions.BuildPerformanceTabUrl("commands", query);

        // Assert
        result.Should().Be("/Admin/Performance?hours=720#commands");
    }

    [Fact]
    public void BuildPerformanceTabUrl_EmptyQueryString_DoesNotEmitBareQuestionMark()
    {
        // Arrange
        var query = new QueryString();

        // Act
        var result = LegacyRedirectExtensions.BuildPerformanceTabUrl("system", query);

        // Assert
        result.Should().Be("/Admin/Performance#system");
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

    [Fact]
    public void BuildLogsTabUrl_QueryContainsHandlerExport_HandlerIsDropped()
    {
        // Arrange - an old bookmark such as /Admin/AuditLogs?handler=Export&SearchTerm=foo
        // must not forward `handler` to the unified page: its export handler binds different
        // parameter names, and forwarding would silently return an unfiltered CSV instead of
        // the expected filtered export (or a filtered page render instead of a CSV at all).
        var query = new QueryString("?handler=Export&SearchTerm=foo");

        // Act
        var result = LegacyRedirectExtensions.BuildLogsTabUrl("audit", query);

        // Assert
        result.Should().Be("/Admin/Logs?SearchTerm=foo&tab=audit");
        result.Should().NotContain("handler");
    }

    [Fact]
    public void BuildLogsTabUrl_QueryAlreadyContainsTab_DoesNotDuplicateTab()
    {
        // Arrange - a bookmark of the already-redirected URL, or a stale ?tab= from
        // somewhere else, must not produce two `tab` query parameters.
        var query = new QueryString("?tab=messages&SearchTerm=foo");

        // Act
        var result = LegacyRedirectExtensions.BuildLogsTabUrl("audit", query);

        // Assert
        result.Should().Be("/Admin/Logs?SearchTerm=foo&tab=audit");
    }

    [Fact]
    public void BuildLogsTabUrl_BareQuestionMark_DoesNotEmitDoubleDelimiter()
    {
        // Arrange - HttpRequest.QueryString can carry a bare "?" (HasValue true, Value "?")
        // for a URL like "/Admin/AuditLogs?" with no parameters after the question mark.
        var query = new QueryString("?");

        // Act
        var result = LegacyRedirectExtensions.BuildLogsTabUrl("audit", query);

        // Assert
        result.Should().Be("/Admin/Logs?tab=audit");
    }

    #endregion
}
