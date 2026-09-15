using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Pages.Account;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Account;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Account;

/// <summary>
/// Covers the static SSR port of Pages/Account/Login.cshtml + LoginModel
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4c). <see cref="IPasswordSignInService"/> is
/// mocked (the sign-in logic itself is <c>PasswordSignInServiceTests</c>' job); this class covers
/// the page's own responsibilities: the Discord button's visibility, the authError alert variants,
/// DataAnnotationsValidator's rendered messages, and turning a <see cref="PasswordSignInOutcome"/>
/// into either a NavigationManager redirect or a rendered error - the same submit-the-rendered-
/// EditForm technique <c>ProfileTests</c> uses for its own static SSR form.
/// </summary>
public class LoginTests : BlazorComponentTestContext
{
    private readonly Mock<IPasswordSignInService> _signInService = new();
    private readonly DefaultHttpContext _httpContext = new();
    private DiscordOAuthSettings _oauthSettings = new() { IsConfigured = true };

    public LoginTests()
    {
        Services.AddSingleton(_signInService.Object);
        // A factory (not a fixed instance) so a test can reassign the _oauthSettings field before
        // Render and have the page see the new value - DI resolves this lazily at Render time.
        Services.AddSingleton<DiscordOAuthSettings>(_ => _oauthSettings);
        Services.AddSingleton<AntiforgeryStateProvider, TestAntiforgeryStateProvider>();

        var versionService = new Mock<IVersionService>();
        versionService.Setup(v => v.GetVersion()).Returns("v1.2.3");
        Services.AddSingleton(versionService.Object);
    }

    private IRenderedComponent<Login> RenderLogin(string? returnUrl = null, string? authError = null, bool authenticated = false)
    {
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        var query = new Dictionary<string, object?>();
        if (returnUrl is not null) query["returnUrl"] = returnUrl;
        if (authError is not null) query["authError"] = authError;
        if (query.Count > 0)
        {
            navMan.NavigateTo(navMan.GetUriWithQueryParameters(query!));
        }

        if (authenticated)
        {
            _httpContext.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "testuser") }, "Test"));
        }

        return Render<Login>(parameters => parameters.AddCascadingValue(_httpContext));
    }

    [Fact]
    public void RendersBrandAndHeader()
    {
        var cut = RenderLogin();

        cut.Find("h2").TextContent.Should().Be("Welcome back");
        cut.Markup.Should().Contain("Bot Admin");
    }

    [Fact]
    public void DiscordConfigured_ShowsDiscordButtonAndDivider()
    {
        _oauthSettings = new DiscordOAuthSettings { IsConfigured = true };

        var cut = RenderLogin();

        cut.Markup.Should().Contain("Continue with Discord");
        cut.Markup.Should().Contain("Or use your email");
        cut.FindAll("form").Should().Contain(f => f.GetAttribute("action") == "/Account/PerformExternalLogin");
    }

    [Fact]
    public void DiscordNotConfigured_HidesDiscordButton()
    {
        _oauthSettings = new DiscordOAuthSettings { IsConfigured = false };

        var cut = RenderLogin();

        cut.Markup.Should().NotContain("Continue with Discord");
        cut.FindAll("form").Should().NotContain(f => f.GetAttribute("action") == "/Account/PerformExternalLogin");
    }

    [Fact]
    public void AlreadyAuthenticated_RedirectsToReturnUrl()
    {
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        RenderLogin(returnUrl: "/components", authenticated: true);

        navMan.History.First().Uri.Should().Be("/components");
    }

    [Fact]
    public void AuthErrorDiscordUnavailable_ShowsTitleAndStatusLink()
    {
        var cut = RenderLogin(authError: "discord_unavailable");

        cut.Markup.Should().Contain("Discord is currently unavailable");
        cut.FindAll("a").Should().Contain(a => a.GetAttribute("href") == "https://discordstatus.com");
    }

    [Fact]
    public void AuthErrorDiscordExpired_ShowsSessionExpiredCopy_NoStatusLink()
    {
        var cut = RenderLogin(authError: "discord_expired");

        cut.Markup.Should().Contain("Login session expired");
        cut.FindAll("a").Should().NotContain(a => a.GetAttribute("href") == "https://discordstatus.com");
    }

    [Fact]
    public void AuthErrorUnknown_ShowsGenericDiscordLoginFailedCopy()
    {
        var cut = RenderLogin(authError: "something_else");

        cut.Markup.Should().Contain("Discord login failed");
    }

    [Fact]
    public void NoAuthError_DoesNotRenderAuthErrorAlert()
    {
        var cut = RenderLogin();

        cut.Markup.Should().NotContain("login-alert-warning");
    }

    [Fact]
    public void SubmittingEmptyForm_ShowsRequiredValidationMessages()
    {
        var cut = RenderLogin();

        cut.Find("#login-form").Submit();

        cut.Markup.Should().Contain("Email is required.");
        cut.Markup.Should().Contain("Password is required.");
        _signInService.Verify(
            s => s.SignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void SubmittingValidCredentials_OnSuccess_NavigatesToReturnUrl()
    {
        _signInService.Setup(s => s.SignInAsync("user@example.test", "Password123!", true, "/components", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordSignInOutcome.Success("/components"));
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = RenderLogin(returnUrl: "/components");
        cut.Find("#email").Change("user@example.test");
        cut.Find("#password").Change("Password123!");
        cut.Find("#remember-me").Change(true);
        cut.Find("#login-form").Submit();

        navMan.History.First().Uri.Should().Be("/components");
    }

    [Fact]
    public void SubmittingValidCredentials_OnFailure_RendersErrorMessage_NoNavigation()
    {
        _signInService.Setup(s => s.SignInAsync("user@example.test", "wrong", false, "/", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordSignInOutcome.Failed("Invalid email or password. Please try again."));
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        var uriBefore = navMan.Uri;

        var cut = RenderLogin();
        cut.Find("#email").Change("user@example.test");
        cut.Find("#password").Change("wrong");
        cut.Find("#login-form").Submit();

        cut.Markup.Should().Contain("Sign-in failed").And.Contain("Invalid email or password");
        navMan.Uri.Should().Be(uriBefore);
    }

    [Fact]
    public void SubmittingValidCredentials_OnLockedOut_NavigatesToLockout()
    {
        _signInService.Setup(s => s.SignInAsync("locked@example.test", "Password123!", false, "/", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordSignInOutcome.LockedOut());
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();

        var cut = RenderLogin();
        cut.Find("#email").Change("locked@example.test");
        cut.Find("#password").Change("Password123!");
        cut.Find("#login-form").Submit();

        navMan.History.First().Uri.Should().Be("/Account/Lockout");
    }
}
