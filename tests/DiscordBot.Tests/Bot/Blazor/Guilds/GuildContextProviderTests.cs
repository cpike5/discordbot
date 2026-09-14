using System.Security.Claims;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Blazor.Guilds;

/// <summary>
/// Unit tests for <see cref="GuildContextProvider"/>: the guild-load/authorize/compute pipeline
/// that replaces the ~27 independent per-page loaders described in
/// <c>Pages/Guilds/GuildPageModelBase.cs</c>. See "GuildContext" in
/// <c>docs/architecture/patterns.md</c>.
/// </summary>
public class GuildContextProviderTests
{
    private const ulong GuildId = 123456789012345678UL;

    private readonly Mock<IGuildService> _mockGuildService;
    private readonly Mock<IGuildMembershipService> _mockGuildMembershipService;
    private readonly Mock<IGuildAudioSettingsService> _mockAudioSettingsService;
    private readonly Mock<IRatWatchService> _mockRatWatchService;
    private readonly Mock<IAuthorizationService> _mockAuthorizationService;
    private readonly GuildContextProvider _provider;

    public GuildContextProviderTests()
    {
        _mockGuildService = new Mock<IGuildService>();
        _mockGuildMembershipService = new Mock<IGuildMembershipService>();
        _mockAudioSettingsService = new Mock<IGuildAudioSettingsService>();
        _mockRatWatchService = new Mock<IRatWatchService>();
        _mockAuthorizationService = new Mock<IAuthorizationService>();

        _provider = new GuildContextProvider(
            _mockGuildService.Object,
            _mockGuildMembershipService.Object,
            _mockAudioSettingsService.Object,
            _mockRatWatchService.Object,
            _mockAuthorizationService.Object,
            new Mock<ILogger<GuildContextProvider>>().Object);
    }

    private static GuildDto Guild() => new() { Id = GuildId, Name = "Test Guild", IsActive = true };

    private static ClaimsPrincipal User(string? userId = "app-user-1", params string[] roles)
    {
        var claims = new List<Claim>();
        if (userId is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }

        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private void SetupAuthorized(bool succeeded = true) =>
        _mockAuthorizationService
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), GuildId, "GuildAccess"))
            .ReturnsAsync(succeeded ? AuthorizationResult.Success() : AuthorizationResult.Failed());

    private void SetupSettings(bool audioEnabled = false, bool ratWatchEnabled = false)
    {
        _mockAudioSettingsService
            .Setup(s => s.GetSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildAudioSettings { GuildId = GuildId, AudioEnabled = audioEnabled });
        _mockRatWatchService
            .Setup(s => s.GetGuildSettingsAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildRatWatchSettings { GuildId = GuildId, IsEnabled = ratWatchEnabled });
    }

    [Fact]
    public async Task GetAsync_ReturnsNotFound_WhenGuildDoesNotExist()
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GuildDto?)null);

        var result = await _provider.GetAsync(GuildId, User());

        result.Status.Should().Be(GuildContextStatus.NotFound);
        result.Context.Should().BeNull();
        _mockAuthorizationService.Verify(
            a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<string>()),
            Times.Never,
            "authorization should not run for a guild that doesn't exist");
    }

    [Fact]
    public async Task GetAsync_ReturnsForbidden_WhenAuthorizationServiceDenies()
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());
        SetupAuthorized(succeeded: false);

        var result = await _provider.GetAsync(GuildId, User());

        result.Status.Should().Be(GuildContextStatus.Forbidden);
        result.Context.Should().BeNull();
        _mockAudioSettingsService.Verify(
            s => s.GetSettingsAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "feature flags should not be loaded once authorization has already failed");
    }

    [Fact]
    public async Task GetAsync_CallsAuthorizeAsync_WithGuildIdAsResource_AndGuildAccessPolicy()
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());
        SetupAuthorized();
        SetupSettings();

        await _provider.GetAsync(GuildId, User());

        _mockAuthorizationService.Verify(
            a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), GuildId, "GuildAccess"),
            Times.Once);
    }

    [Theory]
    [InlineData("SuperAdmin")]
    [InlineData("Admin")]
    public async Task GetAsync_CanEdit_TrueForAppAdminRole_RegardlessOfGuildMembership(string role)
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());
        SetupAuthorized();
        SetupSettings();
        _mockGuildMembershipService
            .Setup(s => s.IsGuildAdminAsync("app-user-1", GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _provider.GetAsync(GuildId, User(roles: role));

        result.Status.Should().Be(GuildContextStatus.Ok);
        result.Context!.IsAppAdmin.Should().BeTrue();
        result.Context.IsGuildAdmin.Should().BeFalse();
        result.Context.CanEdit.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_CanEdit_TrueForGuildAdmin_WhoIsNotAnAppAdmin()
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());
        SetupAuthorized();
        SetupSettings();
        _mockGuildMembershipService
            .Setup(s => s.IsGuildAdminAsync("app-user-1", GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _provider.GetAsync(GuildId, User());

        result.Context!.IsAppAdmin.Should().BeFalse();
        result.Context.IsGuildAdmin.Should().BeTrue();
        result.Context.CanEdit.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_CanEdit_FalseForPlainMember()
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());
        SetupAuthorized();
        SetupSettings();
        _mockGuildMembershipService
            .Setup(s => s.IsGuildAdminAsync("app-user-1", GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _provider.GetAsync(GuildId, User());

        result.Context!.CanEdit.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task GetAsync_PopulatesFeatureFlags_FromSettingsServices(bool audioEnabled, bool ratWatchEnabled)
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());
        SetupAuthorized();
        SetupSettings(audioEnabled, ratWatchEnabled);
        _mockGuildMembershipService
            .Setup(s => s.IsGuildAdminAsync(It.IsAny<string>(), GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _provider.GetAsync(GuildId, User());

        result.Context!.AudioEnabled.Should().Be(audioEnabled);
        result.Context.RatWatchEnabled.Should().Be(ratWatchEnabled);
    }

    [Fact]
    public async Task GetAsync_PopulatesTabs_FromGuildNavigationConfig()
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());
        SetupAuthorized();
        SetupSettings();
        _mockGuildMembershipService
            .Setup(s => s.IsGuildAdminAsync(It.IsAny<string>(), GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _provider.GetAsync(GuildId, User());

        result.Context!.Tabs.Should().NotBeEmpty();
        result.Context.Tabs.Should().Contain(t => t.Id == "overview");
        result.Context.GuildIdString.Should().Be(GuildId.ToString());
    }

    [Fact]
    public async Task GetAsync_Memoises_OneGuildLookup_ForTwoCallsWithSameGuildId()
    {
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());
        SetupAuthorized();
        SetupSettings();
        _mockGuildMembershipService
            .Setup(s => s.IsGuildAdminAsync(It.IsAny<string>(), GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var user = User();
        var first = await _provider.GetAsync(GuildId, user);
        var second = await _provider.GetAsync(GuildId, user);

        first.Should().BeSameAs(second, "the second call should be served from the memoisation cache");
        _mockGuildService.Verify(
            s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()),
            Times.Once);
        _mockAuthorizationService.Verify(
            a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), GuildId, "GuildAccess"),
            Times.Once);
    }

    [Fact]
    public async Task GetAsync_DoesNotMemoiseAcrossDifferentGuildIds()
    {
        const ulong otherGuildId = 987654321098765432UL;
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guild());
        _mockGuildService
            .Setup(s => s.GetGuildByIdAsync(otherGuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = otherGuildId, Name = "Other Guild" });
        _mockAuthorizationService
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), "GuildAccess"))
            .ReturnsAsync(AuthorizationResult.Success());
        SetupSettings();
        _mockGuildMembershipService
            .Setup(s => s.IsGuildAdminAsync(It.IsAny<string>(), It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var user = User();
        await _provider.GetAsync(GuildId, user);
        await _provider.GetAsync(otherGuildId, user);

        _mockGuildService.Verify(s => s.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()), Times.Once);
        _mockGuildService.Verify(s => s.GetGuildByIdAsync(otherGuildId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
