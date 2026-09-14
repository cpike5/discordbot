using DiscordBot.Bot.Services.Account;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Services.Account;

/// <summary>
/// Unit tests for <see cref="DiscordLinkService"/>. Ports the Unlink-related behaviours of the
/// deleted <c>LinkDiscordModelTests</c> (docs/plans/blazor-port-plan.md Phase 4 cluster 4c) -
/// the GET-load and user-not-found behaviours those tests also covered now live on the page
/// itself (<c>LinkDiscordTests</c>, bUnit), since <see cref="IDiscordLinkService"/> takes an
/// already-loaded <see cref="ApplicationUser"/> rather than looking one up.
/// </summary>
public class DiscordLinkServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<IDiscordTokenService> _mockTokenService = new();
    private readonly Mock<IDiscordUserInfoService> _mockUserInfoService = new();
    private readonly Mock<IUserDiscordGuildService> _mockUserDiscordGuildService = new();
    private readonly Mock<IVerificationService> _mockVerificationService = new();
    private readonly DiscordLinkService _service;

    public DiscordLinkServiceTests()
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

        _service = new DiscordLinkService(
            _mockUserManager.Object,
            _mockTokenService.Object,
            _mockUserInfoService.Object,
            _mockUserDiscordGuildService.Object,
            _mockVerificationService.Object,
            new Mock<ILogger<DiscordLinkService>>().Object);
    }

    private static ApplicationUser LinkedUser() => new()
    {
        Id = "user-123",
        DiscordUserId = 123456789012345678,
        DiscordUsername = "testuser#1234",
        DiscordAvatarUrl = "https://cdn.discordapp.com/avatars/123/abc.png"
    };

    [Fact]
    public async Task UnlinkAsync_ClearsDiscordFieldsAndDeletesTokens()
    {
        var user = LinkedUser();

        _mockUserManager.Setup(um => um.GetLoginsAsync(user))
            .ReturnsAsync(new List<UserLoginInfo> { new("Discord", "123456789012345678", "Discord") });
        _mockUserManager.Setup(um => um.RemoveLoginAsync(user, "Discord", "123456789012345678"))
            .ReturnsAsync(IdentityResult.Success);
        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Success);
        _mockTokenService.Setup(ts => ts.DeleteTokensAsync(user.Id, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var outcome = await _service.UnlinkAsync(user);

        outcome.Succeeded.Should().BeTrue();
        outcome.StatusKey.Should().Be("unlink-success");
        user.DiscordUserId.Should().BeNull();
        user.DiscordUsername.Should().BeNull();
        user.DiscordAvatarUrl.Should().BeNull();

        _mockTokenService.Verify(ts => ts.DeleteTokensAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockUserInfoService.Verify(uis => uis.InvalidateCache(user.Id), Times.Once);
        _mockUserManager.Verify(um => um.RemoveLoginAsync(user, "Discord", "123456789012345678"), Times.Once);
        _mockUserManager.Verify(um => um.UpdateAsync(It.Is<ApplicationUser>(u => u.DiscordUserId == null)), Times.Once);
    }

    [Fact]
    public async Task UnlinkAsync_ReturnsNotLinked_WhenNoDiscordAccountLinked()
    {
        var user = new ApplicationUser { Id = "user-123" };

        var outcome = await _service.UnlinkAsync(user);

        outcome.Succeeded.Should().BeFalse();
        outcome.StatusKey.Should().Be("not-linked");

        _mockTokenService.Verify(ts => ts.DeleteTokensAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUserManager.Verify(um => um.UpdateAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    [Fact]
    public async Task UnlinkAsync_HandlesUpdateFailure()
    {
        var user = LinkedUser();

        _mockUserManager.Setup(um => um.GetLoginsAsync(user)).ReturnsAsync(new List<UserLoginInfo>());
        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));
        _mockTokenService.Setup(ts => ts.DeleteTokensAsync(user.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var outcome = await _service.UnlinkAsync(user);

        outcome.Succeeded.Should().BeFalse();
        outcome.StatusKey.Should().Be("unlink-failed");
    }

    [Fact]
    public async Task UnlinkAsync_HandlesException()
    {
        var user = new ApplicationUser { Id = "user-123", DiscordUserId = 123456789012345678 };

        _mockTokenService.Setup(ts => ts.DeleteTokensAsync(user.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Token service error"));

        var outcome = await _service.UnlinkAsync(user);

        outcome.Succeeded.Should().BeFalse();
        outcome.StatusKey.Should().Be("unlink-error");
    }

    [Fact]
    public async Task UnlinkAsync_HandlesRemoveLoginFailure_Gracefully()
    {
        var user = LinkedUser();

        _mockUserManager.Setup(um => um.GetLoginsAsync(user))
            .ReturnsAsync(new List<UserLoginInfo> { new("Discord", "123456789012345678", "Discord") });
        _mockUserManager.Setup(um => um.RemoveLoginAsync(user, "Discord", "123456789012345678"))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Remove login failed" }));
        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        _mockTokenService.Setup(ts => ts.DeleteTokensAsync(user.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var outcome = await _service.UnlinkAsync(user);

        outcome.Succeeded.Should().BeTrue("unlinking should still succeed even if external login removal fails");
        outcome.StatusKey.Should().Be("unlink-success");
    }

    [Fact]
    public async Task UnlinkAsync_HandlesNoExternalLogin()
    {
        var user = LinkedUser();

        _mockUserManager.Setup(um => um.GetLoginsAsync(user)).ReturnsAsync(new List<UserLoginInfo>());
        _mockUserManager.Setup(um => um.UpdateAsync(It.IsAny<ApplicationUser>())).ReturnsAsync(IdentityResult.Success);
        _mockTokenService.Setup(ts => ts.DeleteTokensAsync(user.Id, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var outcome = await _service.UnlinkAsync(user);

        outcome.Succeeded.Should().BeTrue();
        _mockUserManager.Verify(
            um => um.RemoveLoginAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RefreshDiscordDataAsync_ReturnsNotLinked_WhenNoDiscordAccountLinked()
    {
        var user = new ApplicationUser { Id = "user-123" };

        var outcome = await _service.RefreshDiscordDataAsync(user);

        outcome.Succeeded.Should().BeFalse();
        outcome.StatusKey.Should().Be("not-linked");
        _mockUserDiscordGuildService.Verify(s => s.RefreshUserGuildsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RefreshDiscordDataAsync_Succeeds_AndInvalidatesCache()
    {
        var user = LinkedUser();

        var outcome = await _service.RefreshDiscordDataAsync(user);

        outcome.Succeeded.Should().BeTrue();
        outcome.StatusKey.Should().Be("refresh-success");
        _mockUserDiscordGuildService.Verify(s => s.RefreshUserGuildsAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        _mockUserInfoService.Verify(s => s.InvalidateCache(user.Id), Times.Once);
    }

    [Fact]
    public async Task RefreshDiscordDataAsync_HandlesException()
    {
        var user = LinkedUser();
        _mockUserDiscordGuildService.Setup(s => s.RefreshUserGuildsAsync(user.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var outcome = await _service.RefreshDiscordDataAsync(user);

        outcome.Succeeded.Should().BeFalse();
        outcome.StatusKey.Should().Be("refresh-error");
    }

    [Fact]
    public async Task InitiateBotVerificationAsync_Succeeds()
    {
        var user = new ApplicationUser { Id = "user-123" };
        _mockVerificationService.Setup(s => s.InitiateVerificationAsync(user.Id, "1.2.3.4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(VerificationInitiationResult.Success(Guid.NewGuid()));

        var outcome = await _service.InitiateBotVerificationAsync(user, "1.2.3.4");

        outcome.Succeeded.Should().BeTrue();
        outcome.StatusKey.Should().Be("verify-init-success");
    }

    [Fact]
    public async Task InitiateBotVerificationAsync_ReturnsServiceErrorMessage_OnFailure()
    {
        var user = new ApplicationUser { Id = "user-123" };
        _mockVerificationService.Setup(s => s.InitiateVerificationAsync(user.Id, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(VerificationInitiationResult.Failure(VerificationInitiationResult.PendingVerificationExists, "A verification is already pending."));

        var outcome = await _service.InitiateBotVerificationAsync(user, null);

        outcome.Succeeded.Should().BeFalse();
        outcome.StatusKey.Should().Be("verify-init-failed");
        outcome.Detail.Should().Be("A verification is already pending.");
    }

    [Fact]
    public async Task VerifyCodeAsync_ReturnsEmpty_WhenCodeBlank()
    {
        var user = new ApplicationUser { Id = "user-123" };

        var outcome = await _service.VerifyCodeAsync(user, "   ");

        outcome.Succeeded.Should().BeFalse();
        outcome.StatusKey.Should().Be("verify-code-empty");
        _mockVerificationService.Verify(s => s.ValidateCodeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task VerifyCodeAsync_StripsFormattingAndUppercases_BeforeValidating()
    {
        var user = new ApplicationUser { Id = "user-123" };
        _mockVerificationService.Setup(s => s.ValidateCodeAsync(user.Id, "ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CodeValidationResult.Success(999UL, "linked-user"));

        var outcome = await _service.VerifyCodeAsync(user, "abc-123");

        outcome.Succeeded.Should().BeTrue();
        outcome.StatusKey.Should().Be("verify-code-success");
        outcome.Detail.Should().Be("linked-user");
        _mockVerificationService.Verify(s => s.ValidateCodeAsync(user.Id, "ABC123", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task VerifyCodeAsync_ReturnsServiceErrorMessage_OnFailure()
    {
        var user = new ApplicationUser { Id = "user-123" };
        _mockVerificationService.Setup(s => s.ValidateCodeAsync(user.Id, "BADCOD", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CodeValidationResult.Failure(CodeValidationResult.CodeExpired, "This code has expired."));

        var outcome = await _service.VerifyCodeAsync(user, "BADCOD");

        outcome.Succeeded.Should().BeFalse();
        outcome.StatusKey.Should().Be("verify-code-failed");
        outcome.Detail.Should().Be("This code has expired.");
    }

    [Fact]
    public async Task CancelVerificationAsync_Succeeds()
    {
        var user = new ApplicationUser { Id = "user-123" };

        var outcome = await _service.CancelVerificationAsync(user);

        outcome.Succeeded.Should().BeTrue();
        outcome.StatusKey.Should().Be("cancel-success");
        _mockVerificationService.Verify(s => s.CancelPendingVerificationAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelVerificationAsync_HandlesException()
    {
        var user = new ApplicationUser { Id = "user-123" };
        _mockVerificationService.Setup(s => s.CancelPendingVerificationAsync(user.Id, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var outcome = await _service.CancelVerificationAsync(user);

        outcome.Succeeded.Should().BeFalse();
        outcome.StatusKey.Should().Be("cancel-error");
    }
}
