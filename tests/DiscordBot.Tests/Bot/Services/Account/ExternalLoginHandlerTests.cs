using System.Net;
using System.Net.Http;
using System.Security.Claims;
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
using Moq.Protected;

namespace DiscordBot.Tests.Bot.Services.Account;

/// <summary>
/// Unit tests for <see cref="ExternalLoginHandler"/> - ports the behavioural coverage
/// <c>Pages/Account/ExternalLogin.cshtml.cs</c> never had its own test class for
/// (docs/plans/blazor-port-plan.md Phase 4 cluster 4c): the remote-error/missing-info
/// short-circuits, all three account-linking branches (existing user by Discord id, existing user
/// by email, brand-new user), the missing-email-claim failure, and the lockout branch. Mocks
/// <c>SignInManager</c>/<c>UserManager</c> the way <c>LoginModelTests</c> did.
/// </summary>
public class ExternalLoginHandlerTests
{
    private const string ReturnUrl = "/dashboard";

    private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IDiscordTokenService> _mockTokenService = new();
    private readonly Mock<IUserDiscordGuildService> _mockGuildService = new();
    private readonly Mock<IAuditLogService> _mockAuditLogService = new();
    private readonly ExternalLoginHandler _handler;

    public ExternalLoginHandlerTests()
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

        var mockBuilder = new Mock<IAuditLogBuilder>();
        mockBuilder.Setup(x => x.ForCategory(It.IsAny<AuditLogCategory>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithAction(It.IsAny<AuditLogAction>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.ByUser(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.OnTarget(It.IsAny<string>(), It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.FromIpAddress(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithDetails(It.IsAny<object>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.Enqueue());
        _mockAuditLogService.Setup(x => x.CreateBuilder()).Returns(mockBuilder.Object);

        var httpClientFactory = CreateHttpClientFactory("[]");

        _handler = new ExternalLoginHandler(
            _mockSignInManager.Object,
            _mockUserManager.Object,
            _mockTokenService.Object,
            _mockGuildService.Object,
            httpClientFactory,
            _mockAuditLogService.Object,
            new Mock<ILogger<ExternalLoginHandler>>().Object);
    }

    private static IHttpClientFactory CreateHttpClientFactory(string jsonResponseBody)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponseBody)
            });

        var client = new HttpClient(handler.Object) { BaseAddress = new Uri("https://discord.com/api/v10/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient("Discord")).Returns(client);
        return factory.Object;
    }

    /// <summary>Builds an <see cref="ExternalLoginInfo"/> plus a matching <see cref="HttpContext"/> whose external-auth-scheme tokens (see <c>IdentityConstants.ExternalScheme</c>) resolve as given.</summary>
    private static (ExternalLoginInfo Info, HttpContext HttpContext) CreateExternalLogin(
        string? email = "discord-user@example.test",
        string discordId = "123456789012345678",
        string discordUsername = "discorduser",
        string? accessToken = "access-token",
        string? refreshToken = "refresh-token",
        string? expiresAt = "2099-01-01T00:00:00Z")
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, discordId), new(ClaimTypes.Name, discordUsername) };
        if (email != null)
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Discord"));
        var info = new ExternalLoginInfo(principal, "Discord", discordId, "Discord");

        var authProperties = new AuthenticationProperties();
        var tokens = new List<AuthenticationToken>();
        if (accessToken != null) tokens.Add(new AuthenticationToken { Name = "access_token", Value = accessToken });
        if (refreshToken != null) tokens.Add(new AuthenticationToken { Name = "refresh_token", Value = refreshToken });
        if (expiresAt != null) tokens.Add(new AuthenticationToken { Name = "expires_at", Value = expiresAt });
        authProperties.StoreTokens(tokens);

        var ticket = new AuthenticationTicket(principal, authProperties, IdentityConstants.ExternalScheme);

        var mockAuthService = new Mock<IAuthenticationService>();
        mockAuthService.Setup(a => a.AuthenticateAsync(It.IsAny<HttpContext>(), IdentityConstants.ExternalScheme))
            .ReturnsAsync(AuthenticateResult.Success(ticket));

        var services = new Mock<IServiceProvider>();
        services.Setup(sp => sp.GetService(typeof(IAuthenticationService))).Returns(mockAuthService.Object);

        var httpContext = new DefaultHttpContext { RequestServices = services.Object };

        return (info, httpContext);
    }

    [Fact]
    public async Task HandleCallbackAsync_WithRemoteError_ReturnsDiscordErrorWithoutLookingUpExternalInfo()
    {
        var httpContext = new DefaultHttpContext();

        var result = await _handler.HandleCallbackAsync(httpContext, remoteError: "access_denied", ReturnUrl);

        result.Should().BeOfType<ExternalLoginOutcome.Error>()
            .Which.AuthError.Should().Be("discord_error");
        _mockSignInManager.Verify(sm => sm.GetExternalLoginInfoAsync(It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task HandleCallbackAsync_WithNullExternalLoginInfo_ReturnsDiscordError()
    {
        var httpContext = new DefaultHttpContext();
        _mockSignInManager.Setup(sm => sm.GetExternalLoginInfoAsync(It.IsAny<string?>())).ReturnsAsync((ExternalLoginInfo?)null);

        var result = await _handler.HandleCallbackAsync(httpContext, remoteError: null, ReturnUrl);

        result.Should().BeOfType<ExternalLoginOutcome.Error>()
            .Which.AuthError.Should().Be("discord_error");
    }

    [Fact]
    public async Task HandleCallbackAsync_ExistingLinkedUser_SignsInAndRedirectsToReturnUrl()
    {
        var (info, httpContext) = CreateExternalLogin();
        _mockSignInManager.Setup(sm => sm.GetExternalLoginInfoAsync(It.IsAny<string?>())).ReturnsAsync(info);
        _mockSignInManager.Setup(sm => sm.ExternalLoginSignInAsync("Discord", info.ProviderKey, true, true))
            .ReturnsAsync(SignInResult.Success);

        var existingUser = new ApplicationUser { Id = "user-1", Email = "discord-user@example.test" };
        _mockUserManager.Setup(um => um.FindByLoginAsync("Discord", info.ProviderKey)).ReturnsAsync(existingUser);

        var result = await _handler.HandleCallbackAsync(httpContext, remoteError: null, ReturnUrl);

        result.Should().BeOfType<ExternalLoginOutcome.Redirect>()
            .Which.Url.Should().Be(ReturnUrl);
        _mockTokenService.Verify(t => t.StoreTokensAsync(
            existingUser.Id, 123456789012345678UL, "access-token", "refresh-token", It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _mockGuildService.Verify(g => g.InvalidateCache(existingUser.Id), Times.Once);
    }

    [Fact]
    public async Task HandleCallbackAsync_SignInLockedOut_ReturnsLockout()
    {
        var (info, httpContext) = CreateExternalLogin();
        _mockSignInManager.Setup(sm => sm.GetExternalLoginInfoAsync(It.IsAny<string?>())).ReturnsAsync(info);
        _mockSignInManager.Setup(sm => sm.ExternalLoginSignInAsync("Discord", info.ProviderKey, true, true))
            .ReturnsAsync(SignInResult.LockedOut);

        var result = await _handler.HandleCallbackAsync(httpContext, remoteError: null, ReturnUrl);

        result.Should().BeOfType<ExternalLoginOutcome.Lockout>();
    }

    [Fact]
    public async Task HandleCallbackAsync_NoEmailClaim_ReturnsDiscordError()
    {
        var (info, httpContext) = CreateExternalLogin(email: null);
        _mockSignInManager.Setup(sm => sm.GetExternalLoginInfoAsync(It.IsAny<string?>())).ReturnsAsync(info);
        _mockSignInManager.Setup(sm => sm.ExternalLoginSignInAsync("Discord", info.ProviderKey, true, true))
            .ReturnsAsync(SignInResult.Failed);

        var result = await _handler.HandleCallbackAsync(httpContext, remoteError: null, ReturnUrl);

        result.Should().BeOfType<ExternalLoginOutcome.Error>()
            .Which.AuthError.Should().Be("discord_error");
    }

    [Fact]
    public async Task HandleCallbackAsync_NoAccountYet_ExistingUserWithSameDiscordId_LinksAndSignsIn()
    {
        var (info, httpContext) = CreateExternalLogin(discordId: "999888777666555444");
        _mockSignInManager.Setup(sm => sm.GetExternalLoginInfoAsync(It.IsAny<string?>())).ReturnsAsync(info);
        _mockSignInManager.Setup(sm => sm.ExternalLoginSignInAsync("Discord", info.ProviderKey, true, true))
            .ReturnsAsync(SignInResult.Failed);

        var existingUser = new ApplicationUser
        {
            Id = "user-2",
            Email = "discord-user@example.test",
            DiscordUserId = 999888777666555444UL
        };
        _mockUserManager.Setup(um => um.Users).Returns(new[] { existingUser }.AsQueryable());
        _mockUserManager.Setup(um => um.AddLoginAsync(existingUser, info)).ReturnsAsync(IdentityResult.Success);
        // FindByLoginAsync is also called post-sign-in for token storage/audit - return the same user.
        _mockUserManager.Setup(um => um.FindByLoginAsync("Discord", info.ProviderKey)).ReturnsAsync(existingUser);

        var result = await _handler.HandleCallbackAsync(httpContext, remoteError: null, ReturnUrl);

        result.Should().BeOfType<ExternalLoginOutcome.Redirect>()
            .Which.Url.Should().Be(ReturnUrl);
        _mockSignInManager.Verify(sm => sm.SignInAsync(existingUser, true, null), Times.Once);
    }

    [Fact]
    public async Task HandleCallbackAsync_NoAccountYet_ExistingUserWithSameEmail_LinksAndSignsIn()
    {
        var (info, httpContext) = CreateExternalLogin();
        _mockSignInManager.Setup(sm => sm.GetExternalLoginInfoAsync(It.IsAny<string?>())).ReturnsAsync(info);
        _mockSignInManager.Setup(sm => sm.ExternalLoginSignInAsync("Discord", info.ProviderKey, true, true))
            .ReturnsAsync(SignInResult.Failed);

        var existingUser = new ApplicationUser { Id = "user-3", Email = "discord-user@example.test" };
        _mockUserManager.Setup(um => um.Users).Returns(Array.Empty<ApplicationUser>().AsQueryable());
        _mockUserManager.Setup(um => um.FindByEmailAsync("discord-user@example.test")).ReturnsAsync(existingUser);
        _mockUserManager.Setup(um => um.AddLoginAsync(existingUser, info)).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.FindByLoginAsync("Discord", info.ProviderKey)).ReturnsAsync(existingUser);

        var result = await _handler.HandleCallbackAsync(httpContext, remoteError: null, ReturnUrl);

        result.Should().BeOfType<ExternalLoginOutcome.Redirect>()
            .Which.Url.Should().Be(ReturnUrl);
        _mockSignInManager.Verify(sm => sm.SignInAsync(existingUser, true, null), Times.Once);
    }

    [Fact]
    public async Task HandleCallbackAsync_NoAccountYet_NoExistingUser_CreatesAccountAndSignsIn()
    {
        var (info, httpContext) = CreateExternalLogin();
        _mockSignInManager.Setup(sm => sm.GetExternalLoginInfoAsync(It.IsAny<string?>())).ReturnsAsync(info);
        _mockSignInManager.Setup(sm => sm.ExternalLoginSignInAsync("Discord", info.ProviderKey, true, true))
            .ReturnsAsync(SignInResult.Failed);

        _mockUserManager.Setup(um => um.Users).Returns(Array.Empty<ApplicationUser>().AsQueryable());
        _mockUserManager.Setup(um => um.FindByEmailAsync("discord-user@example.test")).ReturnsAsync((ApplicationUser?)null);
        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.AddLoginAsync(It.IsAny<ApplicationUser>(), info)).ReturnsAsync(IdentityResult.Success);
        ApplicationUser? createdUser = null;
        _mockUserManager.Setup(um => um.CreateAsync(It.IsAny<ApplicationUser>()))
            .Callback<ApplicationUser>(u => createdUser = u)
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.FindByLoginAsync("Discord", info.ProviderKey))
            .ReturnsAsync(() => createdUser);

        var result = await _handler.HandleCallbackAsync(httpContext, remoteError: null, ReturnUrl);

        result.Should().BeOfType<ExternalLoginOutcome.Redirect>()
            .Which.Url.Should().Be(ReturnUrl);
        createdUser.Should().NotBeNull();
        createdUser!.Email.Should().Be("discord-user@example.test");
        createdUser.DiscordUserId.Should().Be(123456789012345678UL);
        createdUser.IsActive.Should().BeTrue();
        _mockSignInManager.Verify(sm => sm.SignInAsync(createdUser, true, null), Times.Once);
    }

    [Fact]
    public async Task HandleCallbackAsync_TokenExtractionSkipped_WhenAccessTokenMissing_DoesNotCallStoreTokens()
    {
        var (info, httpContext) = CreateExternalLogin(accessToken: null);
        _mockSignInManager.Setup(sm => sm.GetExternalLoginInfoAsync(It.IsAny<string?>())).ReturnsAsync(info);
        _mockSignInManager.Setup(sm => sm.ExternalLoginSignInAsync("Discord", info.ProviderKey, true, true))
            .ReturnsAsync(SignInResult.Success);

        var existingUser = new ApplicationUser { Id = "user-4", Email = "discord-user@example.test" };
        _mockUserManager.Setup(um => um.FindByLoginAsync("Discord", info.ProviderKey)).ReturnsAsync(existingUser);

        var result = await _handler.HandleCallbackAsync(httpContext, remoteError: null, ReturnUrl);

        result.Should().BeOfType<ExternalLoginOutcome.Redirect>();
        _mockTokenService.Verify(
            t => t.StoreTokensAsync(It.IsAny<string>(), It.IsAny<ulong>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockGuildService.Verify(g => g.InvalidateCache(It.IsAny<string>()), Times.Never);
    }
}
