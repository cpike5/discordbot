using System.Security.Claims;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Account;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Extensions;

/// <summary>
/// Unit tests for the three <c>Account</c> minimal-API handlers
/// (<see cref="AccountEndpointExtensions"/>, docs/plans/blazor-port-plan.md Phase 4 cluster 4c) -
/// the endpoint lambdas are extracted as <see langword="internal static"/> methods precisely so
/// they can be called directly here, the same pattern <c>LegacyRedirectExtensionsTests</c> uses
/// for its URL-building helpers.
/// </summary>
public class AccountEndpointExtensionsTests
{
    #region SanitizeReturnUrl

    [Theory]
    [InlineData("/components", "/components")]
    [InlineData(null, "/fallback")]
    [InlineData("", "/fallback")]
    [InlineData("https://evil.example", "/fallback")]
    [InlineData("//evil.example", "/fallback")]
    [InlineData("javascript:alert(1)", "/fallback")]
    [InlineData("/Account/Login", "/fallback")]
    public void SanitizeReturnUrl_RejectsNonLocalAndLoginUrls(string? input, string expected)
    {
        AccountEndpointExtensions.SanitizeReturnUrl(input, "/fallback").Should().Be(expected);
    }

    #endregion

    #region HandleLogoutAsync

    private static Mock<SignInManager<ApplicationUser>> CreateMockSignInManager()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var userManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, new Mock<IPasswordHasher<ApplicationUser>>().Object,
            Array.Empty<IUserValidator<ApplicationUser>>(), Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new Mock<ILookupNormalizer>().Object, new Mock<IdentityErrorDescriber>().Object,
            new Mock<IServiceProvider>().Object, new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        var signInManager = new Mock<SignInManager<ApplicationUser>>(
            userManager.Object, contextAccessor.Object, claimsFactory.Object, null!,
            new Mock<ILogger<SignInManager<ApplicationUser>>>().Object,
            new Mock<IAuthenticationSchemeProvider>().Object, null!);
        signInManager.Setup(s => s.SignOutAsync()).Returns(Task.CompletedTask);
        return signInManager;
    }

    private static Mock<IAuditLogService> CreateMockAuditLogService()
    {
        var service = new Mock<IAuditLogService>();
        var builder = new Mock<IAuditLogBuilder>();
        builder.Setup(x => x.ForCategory(It.IsAny<AuditLogCategory>())).Returns(builder.Object);
        builder.Setup(x => x.WithAction(It.IsAny<AuditLogAction>())).Returns(builder.Object);
        builder.Setup(x => x.ByUser(It.IsAny<string>())).Returns(builder.Object);
        builder.Setup(x => x.OnTarget(It.IsAny<string>(), It.IsAny<string>())).Returns(builder.Object);
        builder.Setup(x => x.FromIpAddress(It.IsAny<string>())).Returns(builder.Object);
        builder.Setup(x => x.WithDetails(It.IsAny<object>())).Returns(builder.Object);
        builder.Setup(x => x.Enqueue());
        service.Setup(x => x.CreateBuilder()).Returns(builder.Object);
        return service;
    }

    [Fact]
    public async Task HandleLogoutAsync_WithoutReturnUrl_RedirectsToLanding()
    {
        var signInManager = CreateMockSignInManager();
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, "user-1") }, "Test"))
        };

        var result = await AccountEndpointExtensions.HandleLogoutAsync(
            httpContext, null, signInManager.Object, CreateMockAuditLogService().Object, new Mock<ILogger<Program>>().Object);

        result.Should().BeOfType<RedirectHttpResult>()
            .Which.Url.Should().Be("/landing");
        signInManager.Verify(s => s.SignOutAsync(), Times.Once);
    }

    [Fact]
    public async Task HandleLogoutAsync_WithLocalReturnUrl_RedirectsToIt()
    {
        var signInManager = CreateMockSignInManager();
        var httpContext = new DefaultHttpContext();

        var result = await AccountEndpointExtensions.HandleLogoutAsync(
            httpContext, "/Guilds", signInManager.Object, CreateMockAuditLogService().Object, new Mock<ILogger<Program>>().Object);

        result.Should().BeOfType<RedirectHttpResult>()
            .Which.Url.Should().Be("/Guilds");
    }

    [Fact]
    public async Task HandleLogoutAsync_WithExternalReturnUrl_FallsBackToLanding()
    {
        var signInManager = CreateMockSignInManager();
        var httpContext = new DefaultHttpContext();

        var result = await AccountEndpointExtensions.HandleLogoutAsync(
            httpContext, "https://evil.example", signInManager.Object, CreateMockAuditLogService().Object, new Mock<ILogger<Program>>().Object);

        result.Should().BeOfType<RedirectHttpResult>()
            .Which.Url.Should().Be("/landing");
    }

    #endregion

    #region HandlePerformExternalLogin

    [Fact]
    public void HandlePerformExternalLogin_WhenOAuthNotConfigured_RedirectsToLoginWithDiscordError()
    {
        var signInManager = CreateMockSignInManager();
        var settings = new DiscordOAuthSettings { IsConfigured = false };

        var result = AccountEndpointExtensions.HandlePerformExternalLogin("/dashboard", signInManager.Object, settings);

        result.Should().BeOfType<RedirectHttpResult>()
            .Which.Url.Should().Be("/Account/Login?authError=discord_error");
    }

    [Fact]
    public void HandlePerformExternalLogin_WhenConfigured_ReturnsDiscordChallenge_WithCallbackRedirectUri()
    {
        var signInManager = CreateMockSignInManager();
        var settings = new DiscordOAuthSettings { IsConfigured = true };
        AuthenticationProperties? captured = null;
        signInManager.Setup(s => s.ConfigureExternalAuthenticationProperties("Discord", It.IsAny<string>(), null))
            .Callback<string?, string?, string?>((_, redirectUrl, _) => captured = new AuthenticationProperties { RedirectUri = redirectUrl })
            .Returns(() => captured!);

        var result = AccountEndpointExtensions.HandlePerformExternalLogin("/dashboard", signInManager.Object, settings);

        result.Should().BeOfType<ChallengeHttpResult>();
        var challenge = (ChallengeHttpResult)result;
        challenge.AuthenticationSchemes.Should().ContainSingle().Which.Should().Be("Discord");
        challenge.Properties!.RedirectUri.Should().Be("/Account/ExternalLogin/Callback?returnUrl=%2Fdashboard");
    }

    #endregion

    #region HandleExternalLoginCallbackAsync

    [Fact]
    public async Task HandleExternalLoginCallbackAsync_Redirect_LocalRedirectsToOutcomeUrl()
    {
        var handler = new Mock<IExternalLoginHandler>();
        handler.Setup(h => h.HandleCallbackAsync(It.IsAny<HttpContext>(), null, "/dashboard", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLoginOutcome.Redirect("/dashboard"));

        var result = await AccountEndpointExtensions.HandleExternalLoginCallbackAsync(
            new DefaultHttpContext(), "/dashboard", null, handler.Object);

        result.Should().BeOfType<RedirectHttpResult>()
            .Which.Url.Should().Be("/dashboard");
    }

    [Fact]
    public async Task HandleExternalLoginCallbackAsync_Lockout_RedirectsToLockoutPage()
    {
        var handler = new Mock<IExternalLoginHandler>();
        handler.Setup(h => h.HandleCallbackAsync(It.IsAny<HttpContext>(), null, "/", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLoginOutcome.Lockout());

        var result = await AccountEndpointExtensions.HandleExternalLoginCallbackAsync(
            new DefaultHttpContext(), null, null, handler.Object);

        result.Should().BeOfType<RedirectHttpResult>()
            .Which.Url.Should().Be("/Account/Lockout");
    }

    [Fact]
    public async Task HandleExternalLoginCallbackAsync_Error_RedirectsToLoginWithAuthErrorAndReturnUrl()
    {
        var handler = new Mock<IExternalLoginHandler>();
        handler.Setup(h => h.HandleCallbackAsync(It.IsAny<HttpContext>(), null, "/dashboard", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLoginOutcome.Error("discord_error"));

        var result = await AccountEndpointExtensions.HandleExternalLoginCallbackAsync(
            new DefaultHttpContext(), "/dashboard", null, handler.Object);

        result.Should().BeOfType<RedirectHttpResult>()
            .Which.Url.Should().Be("/Account/Login?authError=discord_error&returnUrl=%2Fdashboard");
    }

    #endregion
}
