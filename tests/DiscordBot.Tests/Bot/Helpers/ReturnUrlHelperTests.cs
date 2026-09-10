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
    }
}
