using System.Security.Claims;
using DiscordBot.Bot.Pages.Account;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Account;

/// <summary>
/// Unit tests for <see cref="LogoutModel"/>. Regression coverage for the Phase 3 Blazor-port
/// review finding: <c>OnPostAsync</c> used to call <c>RedirectToPage("/Landing")</c> when no
/// <c>returnUrl</c> was supplied, which throws <c>InvalidOperationException</c> at execution time
/// now that <c>/Landing</c> is a deleted Razor Page (it's <c>Blazor/Pages/Landing.razor</c>,
/// routed at <c>/landing</c>, today) - every sign-out without a <c>returnUrl</c> (both
/// <c>MainNavbar.razor</c>'s and the legacy <c>_Navbar.cshtml</c>'s logout forms post without
/// one) 500'd. <c>OnPostAsync</c> now uses <c>LocalRedirect("/landing")</c> instead.
/// </summary>
public class LogoutModelTests
{
    private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
    private readonly Mock<ILogger<LogoutModel>> _mockLogger;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly LogoutModel _logoutModel;

    public LogoutModelTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        var mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object,
            null!,
            new Mock<IPasswordHasher<ApplicationUser>>().Object,
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new Mock<ILookupNormalizer>().Object,
            new Mock<IdentityErrorDescriber>().Object,
            new Mock<IServiceProvider>().Object,
            new Mock<ILogger<UserManager<ApplicationUser>>>().Object);

        var contextAccessor = new Mock<IHttpContextAccessor>();
        var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
            mockUserManager.Object,
            contextAccessor.Object,
            claimsFactory.Object,
            null!,
            new Mock<ILogger<SignInManager<ApplicationUser>>>().Object,
            new Mock<IAuthenticationSchemeProvider>().Object,
            null!);
        _mockSignInManager.Setup(s => s.SignOutAsync()).Returns(Task.CompletedTask);

        _mockLogger = new Mock<ILogger<LogoutModel>>();

        _mockAuditLogService = new Mock<IAuditLogService>();
        var mockBuilder = new Mock<IAuditLogBuilder>();
        mockBuilder.Setup(x => x.ForCategory(It.IsAny<AuditLogCategory>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithAction(It.IsAny<AuditLogAction>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.ByUser(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.OnTarget(It.IsAny<string>(), It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.FromIpAddress(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithDetails(It.IsAny<object>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.Enqueue());
        _mockAuditLogService.Setup(x => x.CreateBuilder()).Returns(mockBuilder.Object);

        _logoutModel = new LogoutModel(
            _mockSignInManager.Object,
            _mockLogger.Object,
            _mockAuditLogService.Object);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "testuser"),
            new(ClaimTypes.NameIdentifier, "user-1")
        };
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
        };
        _logoutModel.PageContext = new PageContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task OnPostAsync_WithoutReturnUrl_LocalRedirectsToLandingPage()
    {
        var result = await _logoutModel.OnPostAsync();

        result.Should().BeOfType<LocalRedirectResult>()
            .Which.Url.Should().Be("/landing");
        _mockSignInManager.Verify(s => s.SignOutAsync(), Times.Once);
    }

    [Fact]
    public async Task OnPostAsync_WithReturnUrl_LocalRedirectsToReturnUrl()
    {
        var result = await _logoutModel.OnPostAsync("/Guilds");

        result.Should().BeOfType<LocalRedirectResult>()
            .Which.Url.Should().Be("/Guilds");
        _mockSignInManager.Verify(s => s.SignOutAsync(), Times.Once);
    }
}
