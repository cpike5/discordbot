using DiscordBot.Bot.Services.Account;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Services.Account;

/// <summary>
/// Unit tests for <see cref="PasswordSignInService"/> - ports every case
/// <c>LoginModelTests</c> covered for <c>LoginModel.OnPostAsync</c> (docs/plans/blazor-port-plan.md
/// Phase 4 cluster 4c), minus the dead 2FA-redirect case (there is no <c>LoginWith2fa</c> page),
/// replaced by <see cref="RequiresTwoFactor_IsTreatedAsAFailedAttempt"/>.
/// </summary>
public class PasswordSignInServiceTests
{
    private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly PasswordSignInService _service;

    public PasswordSignInServiceTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
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
            _mockUserManager.Object,
            contextAccessor.Object,
            claimsFactory.Object,
            null!,
            new Mock<ILogger<SignInManager<ApplicationUser>>>().Object,
            new Mock<IAuthenticationSchemeProvider>().Object,
            null!);

        _mockAuditLogService = new Mock<IAuditLogService>();
        var mockBuilder = new Mock<IAuditLogBuilder>();
        mockBuilder.Setup(x => x.ForCategory(It.IsAny<AuditLogCategory>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithAction(It.IsAny<AuditLogAction>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.ByUser(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.BySystem()).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.OnTarget(It.IsAny<string>(), It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.FromIpAddress(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithDetails(It.IsAny<object>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.Enqueue());
        _mockAuditLogService.Setup(x => x.CreateBuilder()).Returns(mockBuilder.Object);

        _service = new PasswordSignInService(
            _mockSignInManager.Object,
            _mockUserManager.Object,
            _mockAuditLogService.Object,
            new Mock<ILogger<PasswordSignInService>>().Object);
    }

    [Fact]
    public async Task SignInAsync_WithInactiveUser_ReturnsInactive_AndNeverAttemptsSignIn()
    {
        const string email = "inactive@example.com";
        var inactiveUser = new ApplicationUser { Email = email, UserName = email, IsActive = false };
        _mockUserManager.Setup(um => um.FindByEmailAsync(email)).ReturnsAsync(inactiveUser);

        var result = await _service.SignInAsync(email, "Password123!", false, "/dashboard", "127.0.0.1");

        result.Should().BeOfType<PasswordSignInOutcome.Inactive>();
        _mockSignInManager.Verify(
            sm => sm.PasswordSignInAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task SignInAsync_WithValidCredentials_UpdatesLastLoginAndReturnsSuccessWithReturnUrl()
    {
        const string email = "user@example.com";
        const string password = "Password123!";
        const string returnUrl = "/dashboard";
        var user = new ApplicationUser { Email = email, UserName = email, IsActive = true, LastLoginAt = null };

        _mockUserManager.Setup(um => um.FindByEmailAsync(email)).ReturnsAsync(user);
        _mockSignInManager.Setup(sm => sm.PasswordSignInAsync(email, password, true, true))
            .ReturnsAsync(SignInResult.Success);
        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);

        var result = await _service.SignInAsync(email, password, true, returnUrl, "127.0.0.1");

        result.Should().BeOfType<PasswordSignInOutcome.Success>()
            .Which.ReturnUrl.Should().Be(returnUrl);
        user.LastLoginAt.Should().NotBeNull().And.BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        _mockUserManager.Verify(um => um.UpdateAsync(It.Is<ApplicationUser>(u => u.LastLoginAt != null)), Times.Once);
    }

    [Fact]
    public async Task SignInAsync_WithInvalidCredentials_ReturnsFailedWithGenericMessage()
    {
        const string email = "user@example.com";
        const string password = "WrongPassword";
        var user = new ApplicationUser { Email = email, UserName = email, IsActive = true };

        _mockUserManager.Setup(um => um.FindByEmailAsync(email)).ReturnsAsync(user);
        _mockSignInManager.Setup(sm => sm.PasswordSignInAsync(email, password, false, true))
            .ReturnsAsync(SignInResult.Failed);

        var result = await _service.SignInAsync(email, password, false, "/", "127.0.0.1");

        result.Should().BeOfType<PasswordSignInOutcome.Failed>()
            .Which.Message.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task SignInAsync_WithLockedOutUser_ReturnsLockedOut()
    {
        const string email = "locked@example.com";
        const string password = "Password123!";
        var user = new ApplicationUser { Email = email, UserName = email, IsActive = true };

        _mockUserManager.Setup(um => um.FindByEmailAsync(email)).ReturnsAsync(user);
        _mockSignInManager.Setup(sm => sm.PasswordSignInAsync(email, password, false, true))
            .ReturnsAsync(SignInResult.LockedOut);

        var result = await _service.SignInAsync(email, password, false, "/", "127.0.0.1");

        result.Should().BeOfType<PasswordSignInOutcome.LockedOut>();
    }

    [Fact]
    public async Task SignInAsync_RequiresTwoFactor_IsTreatedAsAFailedAttempt()
    {
        // No 2FA flow exists in this application - the legacy page's RedirectToPage("./LoginWith2fa")
        // targeted a page that was never implemented. Confirms the removed branch doesn't crash and
        // instead surfaces the same generic message as any other failed sign-in.
        const string email = "2fa@example.com";
        const string password = "Password123!";
        var user = new ApplicationUser { Email = email, UserName = email, IsActive = true };

        _mockUserManager.Setup(um => um.FindByEmailAsync(email)).ReturnsAsync(user);
        _mockSignInManager.Setup(sm => sm.PasswordSignInAsync(email, password, true, true))
            .ReturnsAsync(SignInResult.TwoFactorRequired);

        var result = await _service.SignInAsync(email, password, true, "/dashboard", "127.0.0.1");

        result.Should().BeOfType<PasswordSignInOutcome.Failed>()
            .Which.Message.Should().Contain("Invalid email or password");
    }

    [Fact]
    public async Task SignInAsync_PassesLockoutOnFailureAsTrue()
    {
        const string email = "user@example.com";
        const string password = "Password123!";
        var user = new ApplicationUser { Email = email, UserName = email, IsActive = true };

        _mockUserManager.Setup(um => um.FindByEmailAsync(email)).ReturnsAsync(user);
        _mockSignInManager.Setup(sm => sm.PasswordSignInAsync(email, password, false, true))
            .ReturnsAsync(SignInResult.Failed);

        await _service.SignInAsync(email, password, false, "/", "127.0.0.1");

        _mockSignInManager.Verify(sm => sm.PasswordSignInAsync(email, password, false, true), Times.Once);
    }
}
