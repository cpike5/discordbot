using DiscordBot.Bot.Helpers;
using FluentAssertions;

namespace DiscordBot.Tests.Bot.Helpers;

public class ReturnUrlHelperTests
{
    [Theory]
    [InlineData("/login")]
    [InlineData("/Login")]
    [InlineData("/login/")]
    [InlineData("/login?foo=bar")]
    [InlineData("/Account/Login")]
    [InlineData("/account/login")]
    [InlineData("/Account/Login?ReturnUrl=%2Fdashboard")]
    [InlineData("~/Account/Login")]
    public void PointsToLoginPage_ReturnsTrue_ForLoginPaths(string returnUrl)
    {
        ReturnUrlHelper.PointsToLoginPage(returnUrl).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("~/")]
    [InlineData("/dashboard")]
    [InlineData("/Guilds/123/login-history")]
    [InlineData("/Account/Profile")]
    public void PointsToLoginPage_ReturnsFalse_ForOtherPaths(string? returnUrl)
    {
        ReturnUrlHelper.PointsToLoginPage(returnUrl).Should().BeFalse();
    }

    [Fact]
    public void Sanitize_ReturnsFallback_ForLoginOrEmpty()
    {
        ReturnUrlHelper.Sanitize("/login", "~/").Should().Be("~/");
        ReturnUrlHelper.Sanitize("/Account/Login", "~/").Should().Be("~/");
        ReturnUrlHelper.Sanitize(null, "~/").Should().Be("~/");
        ReturnUrlHelper.Sanitize("", "~/").Should().Be("~/");
    }

    [Fact]
    public void Sanitize_ReturnsOriginal_ForOtherPaths()
    {
        ReturnUrlHelper.Sanitize("/dashboard", "~/").Should().Be("/dashboard");
        ReturnUrlHelper.Sanitize("/Admin/Logs?tab=audit&page=2", "~/").Should().Be("/Admin/Logs?tab=audit&page=2");
        ReturnUrlHelper.Sanitize("~/Guilds", "/").Should().Be("~/Guilds");
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("http://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("~//evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("JaVaScRiPt:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("dashboard")]
    [InlineData("/\r\nLocation: https://evil.example")]
    public void Sanitize_ReturnsFallback_ForNonLocalUrls(string returnUrl)
    {
        ReturnUrlHelper.IsLocalUrl(returnUrl).Should().BeFalse();
        ReturnUrlHelper.Sanitize(returnUrl, "/fallback").Should().Be("/fallback");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("~/")]
    [InlineData("/Guilds/Details/123")]
    [InlineData("~/Admin/Logs?tab=audit")]
    public void IsLocalUrl_ReturnsTrue_ForAppRelativePaths(string url)
    {
        ReturnUrlHelper.IsLocalUrl(url).Should().BeTrue();
    }
}
