using System.Security.Claims;
using DiscordBot.Bot.Pages.Account;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Account;

/// <summary>
/// Unit tests for LinkDiscordModel Razor Page.
/// </summary>
public class LinkDiscordModelTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
    private readonly Mock<IDiscordTokenService> _mockTokenService;
    private readonly Mock<IDiscordUserInfoService> _mockUserInfoService;
    private readonly Mock<IGuildMembershipService> _mockGuildMembershipService;
    private readonly Mock<IVerificationService> _mockVerificationService;
    private readonly DiscordOAuthSettings _oauthSettings;
    private readonly Mock<ILogger<LinkDiscordModel>> _mockLogger;
    private readonly LinkDiscordModel _pageModel;
    private readonly Mock<IServiceProvider> _mockServiceProvider;

    public LinkDiscordModelTests()
    {
        // Setup UserManager mock
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

        // Setup SignInManager mock
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

        // Setup service mocks
        _mockTokenService = new Mock<IDiscordTokenService>();
        _mockUserInfoService = new Mock<IDiscordUserInfoService>();
        _mockGuildMembershipService = new Mock<IGuildMembershipService>();
        _mockVerificationService = new Mock<IVerificationService>();

        // Setup verification service default: no pending verification
        _mockVerificationService.Setup(vs => vs.GetPendingVerificationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VerificationCode?)null);

        // Setup OAuth settings (configured by default)
        _oauthSettings = new DiscordOAuthSettings { IsConfigured = true };

        // Setup logger mock
        _mockLogger = new Mock<ILogger<LinkDiscordModel>>();

        // Setup service provider mock
        _mockServiceProvider = new Mock<IServiceProvider>();

        // Create page model instance
        _pageModel = new LinkDiscordModel(
            _mockUserManager.Object,
            _mockSignInManager.Object,
            _mockTokenService.Object,
            _mockUserInfoService.Object,
            _mockGuildMembershipService.Object,
            _mockVerificationService.Object,
            _oauthSettings,
            _mockLogger.Object);

        // Setup HttpContext and PageContext
        var httpContext = new DefaultHttpContext
        {
            RequestServices = _mockServiceProvider.Object
        };

        _pageModel.PageContext = new PageContext
        {
            HttpContext = httpContext
        };

        // Setup URL helper
        var urlHelper = new Mock<IUrlHelper>();
        _pageModel.Url = urlHelper.Object;

        // Setup TempData
        _pageModel.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public async Task OnGetAsync_ShowsLinkedStatus_WhenDiscordIsLinked()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-123",
            DiscordUserId = 123456789012345678,
            DiscordUsername = "testuser#1234",
            DiscordAvatarUrl = "https://cdn.discordapp.com/avatars/123/abc.png"
        };

        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _pageModel.PageContext.HttpContext.User = principal;

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync(user);

        _mockTokenService.Setup(ts => ts.HasValidTokenAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _pageModel.OnGetAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _pageModel.IsDiscordLinked.Should().BeTrue();
        _pageModel.DiscordUserId.Should().Be(user.DiscordUserId);
        _pageModel.DiscordUsername.Should().Be(user.DiscordUsername);
        _pageModel.DiscordAvatarUrl.Should().Be(user.DiscordAvatarUrl);
        _pageModel.HasValidToken.Should().BeFalse();
        _pageModel.UserGuilds.Should().BeEmpty();
    }

    [Fact]
    public async Task OnGetAsync_ShowsUnlinkedStatus_WhenDiscordNotLinked()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-123",
            DiscordUserId = null,
            DiscordUsername = null,
            DiscordAvatarUrl = null
        };

        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _pageModel.PageContext.HttpContext.User = principal;

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync(user);

        // Act
        var result = await _pageModel.OnGetAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _pageModel.IsDiscordLinked.Should().BeFalse();
        _pageModel.DiscordUserId.Should().BeNull();
        _pageModel.DiscordUsername.Should().BeNull();
        _pageModel.DiscordAvatarUrl.Should().BeNull();
        _pageModel.HasValidToken.Should().BeFalse();
        _pageModel.UserGuilds.Should().BeEmpty();
    }

    [Fact]
    public async Task OnGetAsync_LoadsGuilds_WhenValidTokenExists()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-123",
            DiscordUserId = 123456789012345678,
            DiscordUsername = "testuser#1234"
        };

        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _pageModel.PageContext.HttpContext.User = principal;

        var guilds = new List<DiscordGuildDto>
        {
            new DiscordGuildDto
            {
                Id = 987654321098765432,
                Name = "Test Guild",
                Owner = true,
                Permissions = 0x8
            },
            new DiscordGuildDto
            {
                Id = 111111111111111111,
                Name = "Another Guild",
                Owner = false,
                Permissions = 0x8
            }
        };

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync(user);

        _mockTokenService.Setup(ts => ts.HasValidTokenAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockGuildMembershipService.Setup(gms => gms.GetAdministeredGuildsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        // Act
        var result = await _pageModel.OnGetAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _pageModel.IsDiscordLinked.Should().BeTrue();
        _pageModel.HasValidToken.Should().BeTrue();
        _pageModel.UserGuilds.Should().HaveCount(2);
        _pageModel.UserGuilds.Should().Contain(g => g.Name == "Test Guild");
        _pageModel.UserGuilds.Should().Contain(g => g.Name == "Another Guild");
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenUserNotFound()
    {
        // Arrange
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, "nonexistent-user") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _pageModel.PageContext.HttpContext.User = principal;

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _pageModel.OnGetAsync();

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().Be("User not found.");
    }

    [Fact]
    public async Task OnGetAsync_HandlesTokenCheckError_Gracefully()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-123",
            DiscordUserId = 123456789012345678,
            DiscordUsername = "testuser#1234"
        };

        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _pageModel.PageContext.HttpContext.User = principal;

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync(user);

        _mockTokenService.Setup(ts => ts.HasValidTokenAsync(user.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Token service error"));

        // Act
        var result = await _pageModel.OnGetAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _pageModel.IsDiscordLinked.Should().BeTrue();
        _pageModel.HasValidToken.Should().BeFalse("should default to false on error");
        _pageModel.UserGuilds.Should().BeEmpty();
    }

    // Note: OnPostLinkAsync with OAuth configured cannot be easily tested with unit tests
    // because it uses URL.Page extension method and SignInManager.ConfigureExternalAuthenticationProperties
    // which both have optional parameters that can't be mocked with Moq expression trees.
    // This functionality should be tested via integration tests instead.

    [Fact]
    public void OnPostLinkAsync_RedirectsWithError_WhenOAuthNotConfigured()
    {
        // Arrange
        var oauthSettings = new DiscordOAuthSettings { IsConfigured = false };
        var pageModel = new LinkDiscordModel(
            _mockUserManager.Object,
            _mockSignInManager.Object,
            _mockTokenService.Object,
            _mockUserInfoService.Object,
            _mockGuildMembershipService.Object,
            _mockVerificationService.Object,
            oauthSettings,
            _mockLogger.Object);

        // Setup HttpContext and PageContext
        var httpContext = new DefaultHttpContext { RequestServices = _mockServiceProvider.Object };
        pageModel.PageContext = new PageContext { HttpContext = httpContext };

        // Setup URL helper - no need to setup extension methods
        var urlHelper = new Mock<IUrlHelper>();
        pageModel.Url = urlHelper.Object;

        // Setup TempData
        pageModel.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        // Act
        var result = pageModel.OnPostLinkAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        pageModel.StatusMessage.Should().Contain("Discord OAuth is not configured");
        pageModel.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task OnPostUnlinkAsync_ClearsDiscordFieldsAndDeletesTokens()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-123",
            DiscordUserId = 123456789012345678,
            DiscordUsername = "testuser#1234",
            DiscordAvatarUrl = "https://cdn.discordapp.com/avatars/123/abc.png"
        };

        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _pageModel.PageContext.HttpContext.User = principal;

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync(user);

        _mockUserManager.Setup(um => um.GetLoginsAsync(user))
            .ReturnsAsync(new List<UserLoginInfo>
            {
                new UserLoginInfo("Discord", "123456789012345678", "Discord")
            });

        _mockUserManager.Setup(um => um.RemoveLoginAsync(user, "Discord", "123456789012345678"))
            .ReturnsAsync(IdentityResult.Success);

        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockTokenService.Setup(ts => ts.DeleteTokensAsync(user.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUserInfoService.Setup(uis => uis.InvalidateCache(user.Id));

        // Act
        var result = await _pageModel.OnPostUnlinkAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        user.DiscordUserId.Should().BeNull();
        user.DiscordUsername.Should().BeNull();
        user.DiscordAvatarUrl.Should().BeNull();

        _mockTokenService.Verify(
            ts => ts.DeleteTokensAsync(user.Id, It.IsAny<CancellationToken>()),
            Times.Once,
            "tokens should be deleted");

        _mockUserInfoService.Verify(
            uis => uis.InvalidateCache(user.Id),
            Times.Once,
            "cache should be invalidated");

        _mockUserManager.Verify(
            um => um.RemoveLoginAsync(user, "Discord", "123456789012345678"),
            Times.Once,
            "external login should be removed");

        _mockUserManager.Verify(
            um => um.UpdateAsync(It.Is<ApplicationUser>(u => u.DiscordUserId == null)),
            Times.Once,
            "user should be updated with cleared Discord fields");

        _pageModel.StatusMessage.Should().Contain("Discord account unlinked successfully");
        _pageModel.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task OnPostUnlinkAsync_HandlesUserNotFound_Gracefully()
    {
        // Arrange
        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, "nonexistent-user") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _pageModel.PageContext.HttpContext.User = principal;

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _pageModel.OnPostUnlinkAsync();

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>()
            .Which.Value.Should().Be("User not found.");
    }

    [Fact]
    public async Task OnPostUnlinkAsync_ReturnsError_WhenNoDiscordAccountLinked()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-123",
            DiscordUserId = null, // No Discord linked
            DiscordUsername = null,
            DiscordAvatarUrl = null
        };

        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _pageModel.PageContext.HttpContext.User = principal;

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync(user);

        // Act
        var result = await _pageModel.OnPostUnlinkAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        _pageModel.StatusMessage.Should().Contain("No Discord account is currently linked");
        _pageModel.IsSuccess.Should().BeFalse();

        // Verify no unlink operations were performed
        _mockTokenService.Verify(
            ts => ts.DeleteTokensAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockUserManager.Verify(
            um => um.UpdateAsync(It.IsAny<ApplicationUser>()),
            Times.Never);
    }

    [Fact]
    public async Task OnPostUnlinkAsync_HandlesUpdateFailure()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-123",
            DiscordUserId = 123456789012345678,
            DiscordUsername = "testuser#1234"
        };

        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _pageModel.PageContext.HttpContext.User = principal;

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync(user);

        _mockUserManager.Setup(um => um.GetLoginsAsync(user))
            .ReturnsAsync(new List<UserLoginInfo>());

        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));

        _mockTokenService.Setup(ts => ts.DeleteTokensAsync(user.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUserInfoService.Setup(uis => uis.InvalidateCache(user.Id));

        // Act
        var result = await _pageModel.OnPostUnlinkAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        _pageModel.StatusMessage.Should().Contain("Failed to unlink Discord account");
        _pageModel.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task OnPostUnlinkAsync_HandlesException()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-123",
            DiscordUserId = 123456789012345678
        };

        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _pageModel.PageContext.HttpContext.User = principal;

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync(user);

        _mockTokenService.Setup(ts => ts.DeleteTokensAsync(user.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Token service error"));

        // Act
        var result = await _pageModel.OnPostUnlinkAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        _pageModel.StatusMessage.Should().Contain("An error occurred while unlinking Discord account");
        _pageModel.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task OnPostUnlinkAsync_HandlesRemoveLoginFailure_Gracefully()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-123",
            DiscordUserId = 123456789012345678,
            DiscordUsername = "testuser#1234"
        };

        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _pageModel.PageContext.HttpContext.User = principal;

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync(user);

        _mockUserManager.Setup(um => um.GetLoginsAsync(user))
            .ReturnsAsync(new List<UserLoginInfo>
            {
                new UserLoginInfo("Discord", "123456789012345678", "Discord")
            });

        _mockUserManager.Setup(um => um.RemoveLoginAsync(user, "Discord", "123456789012345678"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Remove login failed" }));

        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockTokenService.Setup(ts => ts.DeleteTokensAsync(user.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUserInfoService.Setup(uis => uis.InvalidateCache(user.Id));

        // Act
        var result = await _pageModel.OnPostUnlinkAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        // Should still succeed overall even if external login removal fails
        _pageModel.StatusMessage.Should().Contain("Discord account unlinked successfully");
        _pageModel.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void IsDiscordOAuthConfigured_ReturnsSettingsValue()
    {
        // Assert
        _pageModel.IsDiscordOAuthConfigured.Should().BeTrue("OAuth settings are configured in test setup");
    }

    [Fact]
    public async Task OnGetAsync_DoesNotLoadGuilds_WhenTokenCheckFails()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-123",
            DiscordUserId = 123456789012345678,
            DiscordUsername = "testuser#1234"
        };

        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _pageModel.PageContext.HttpContext.User = principal;

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync(user);

        _mockTokenService.Setup(ts => ts.HasValidTokenAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _pageModel.OnGetAsync();

        // Assert
        result.Should().BeOfType<PageResult>();
        _pageModel.HasValidToken.Should().BeFalse();
        _pageModel.UserGuilds.Should().BeEmpty();

        // Guild membership service should not be called
        _mockGuildMembershipService.Verify(
            gms => gms.GetAdministeredGuildsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "guilds should not be fetched when token is invalid");
    }

    [Fact]
    public async Task OnPostUnlinkAsync_HandlesNoExternalLogin()
    {
        // Arrange
        var user = new ApplicationUser
        {
            Id = "user-123",
            DiscordUserId = 123456789012345678,
            DiscordUsername = "testuser#1234"
        };

        var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, user.Id) };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _pageModel.PageContext.HttpContext.User = principal;

        _mockUserManager.Setup(um => um.GetUserAsync(principal))
            .ReturnsAsync(user);

        _mockUserManager.Setup(um => um.GetLoginsAsync(user))
            .ReturnsAsync(new List<UserLoginInfo>()); // No external logins

        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockTokenService.Setup(ts => ts.DeleteTokensAsync(user.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _mockUserInfoService.Setup(uis => uis.InvalidateCache(user.Id));

        // Act
        var result = await _pageModel.OnPostUnlinkAsync();

        // Assert
        result.Should().BeOfType<RedirectToPageResult>();
        _pageModel.StatusMessage.Should().Contain("Discord account unlinked successfully");
        _pageModel.IsSuccess.Should().BeTrue();

        // Verify RemoveLoginAsync was not called
        _mockUserManager.Verify(
            um => um.RemoveLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never,
            "should not attempt to remove login when none exists");
    }
}
