using DiscordBot.Bot.Blazor.Common;
using FluentAssertions;

namespace DiscordBot.Tests.Blazor.Common;

/// <summary>
/// Unit tests for <see cref="LocalUrl.IsLocal"/> - the guard that keeps a query-string
/// <c>returnUrl</c> from becoming an XSS (<c>javascript:</c>) or open-redirect vector when it is
/// rendered into an <c>href</c>. See the Phase 4 cluster 4a review finding on
/// <c>Blazor/Pages/Admin/AuditLogs/Details.razor.cs</c>.
/// </summary>
public class LocalUrlTests
{
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("JavaScript:alert(1)")]
    [InlineData("https://evil.example")]
    [InlineData("http://evil.example/path")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("mailto:a@b.com")]
    public void IsLocal_RejectsUnsafeValues(string? value)
    {
        LocalUrl.IsLocal(value).Should().BeFalse();
    }

    [Theory]
    [InlineData("/ok?x=1")]
    [InlineData("/Admin/Logs?tab=audit")]
    [InlineData("/")]
    [InlineData("/Admin/AuditLogs/Details/123")]
    public void IsLocal_AcceptsSameOriginRelativePaths(string value)
    {
        LocalUrl.IsLocal(value).Should().BeTrue();
    }
}
