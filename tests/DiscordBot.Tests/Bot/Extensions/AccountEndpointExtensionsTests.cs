using System.Security.Claims;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Account;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
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
    public void HandlePerformExternalLogin_WhenOAuthNotConfigured_RedirectsToLoginWithDiscordUnconfigured()
    {
        var signInManager = CreateMockSignInManager();
        var settings = new DiscordOAuthSettings { IsConfigured = false };

        var result = AccountEndpointExtensions.HandlePerformExternalLogin("/dashboard", signInManager.Object, settings);

        result.Should().BeOfType<RedirectHttpResult>()
            .Which.Url.Should().Be("/Account/Login?authError=discord_unconfigured");
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

    #region Antiforgery metadata (MapAccountEndpoints)

    /// <summary>
    /// Builds the real endpoint route data <see cref="AccountEndpointExtensions.MapAccountEndpoints"/>
    /// produces (via <see cref="WebApplication"/>, which implements <see cref="IEndpointRouteBuilder"/>
    /// directly - no need to <c>Build()</c>/<c>Run()</c> it) and asserts the antiforgery metadata
    /// ASP.NET Core infers for each endpoint, rather than trusting the doc comment on
    /// <see cref="AccountEndpointExtensions"/> that used to cite the handler-level unit tests above
    /// for this - none of them ever construct an endpoint, so none could have proven it. A minimal
    /// API that binds any parameter with <c>[FromForm]</c> (both POST handlers do) is decorated
    /// with <see cref="IAntiforgeryMetadata"/> (<c>RequiresValidation == true</c>) automatically;
    /// the GET callback binds nothing from form data and carries none.
    /// </summary>
    [Fact]
    public void MapAccountEndpoints_PostEndpointsRequireAntiforgery_GetEndpointsDoNot()
    {
        var builder = WebApplication.CreateBuilder();

        // Only registered so RequestDelegateFactory's parameter-source inference recognizes each
        // type as a DI service rather than guessing it's a JSON request body (which then conflicts
        // with the handlers' own [FromForm] parameter and throws at endpoint-build time below) -
        // never resolved, so a throwing factory is fine.
        builder.Services.AddSingleton<SignInManager<ApplicationUser>>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<IAuditLogService>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<DiscordOAuthSettings>(_ => throw new NotSupportedException());
        builder.Services.AddSingleton<IExternalLoginHandler>(_ => throw new NotSupportedException());

        var app = builder.Build();

        app.MapAccountEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var logout = endpoints.Single(e => e.RoutePattern.RawText == AccountRoutes.Logout);
        var performExternalLogin = endpoints.Single(e => e.RoutePattern.RawText == AccountRoutes.PerformExternalLogin);
        var callback = endpoints.Single(e => e.RoutePattern.RawText == AccountRoutes.ExternalLoginCallback);

        logout.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation.Should().BeTrue(
            "POST /Account/Logout binds [FromForm] returnUrl and must be antiforgery-validated");
        performExternalLogin.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation.Should().BeTrue(
            "POST /Account/PerformExternalLogin binds [FromForm] returnUrl and must be antiforgery-validated");
        callback.Metadata.GetMetadata<IAntiforgeryMetadata>()?.RequiresValidation.Should().NotBe(true,
            "GET /Account/ExternalLogin/Callback binds no form data and must not require an antiforgery token");
    }

    #endregion
}
