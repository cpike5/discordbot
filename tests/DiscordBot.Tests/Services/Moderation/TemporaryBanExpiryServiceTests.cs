using System.Net;
using Discord;
using Discord.Net;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Services.Moderation;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services.Moderation;

/// <summary>
/// Tests for <see cref="TemporaryBanExpiryService"/>, which lifts temporary bans once they expire.
/// </summary>
public class TemporaryBanExpiryServiceTests
{
    private const ulong GuildId = 100UL;
    private const ulong TargetId = 300UL;
    private const ulong BotId = 999UL;

    private readonly Mock<IModerationService> _moderationService = new();
    private readonly Mock<IGuild> _guild = new();
    private readonly Mock<IServiceScopeFactory> _scopeFactory = new();
    private IGuild? _guildLookupResult;

    public TemporaryBanExpiryServiceTests()
    {
        var provider = new Mock<IServiceProvider>();
        provider.Setup(p => p.GetService(typeof(IModerationService))).Returns(_moderationService.Object);
        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(provider.Object);
        _scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        _guild.SetupGet(g => g.Id).Returns(GuildId);
        _guildLookupResult = _guild.Object;

        _moderationService
            .Setup(s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ModerationCaseCreateDto dto, CancellationToken _) => new ModerationCaseDto
            {
                Id = Guid.NewGuid(),
                CaseNumber = 2,
                GuildId = dto.GuildId,
                TargetUserId = dto.TargetUserId,
                ModeratorUserId = dto.ModeratorUserId,
                Type = dto.Type,
                Reason = dto.Reason
            });
    }

    private TemporaryBanExpiryService CreateService() => new(
        Mock.Of<IServiceProvider>(),
        _scopeFactory.Object,
        Options.Create(new ModerationOptions()),
        id => id == GuildId ? _guildLookupResult : null,
        () => BotId,
        Mock.Of<ILogger<TemporaryBanExpiryService>>());

    private void ReturnExpired(params ModerationCase[] cases) =>
        _moderationService
            .Setup(s => s.GetExpiredTemporaryActionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(cases);

    private static ModerationCase ExpiredCase(CaseType type, long caseNumber = 1) => new()
    {
        Id = Guid.NewGuid(),
        CaseNumber = caseNumber,
        GuildId = GuildId,
        TargetUserId = TargetId,
        ModeratorUserId = 200UL,
        Type = type,
        CreatedAt = DateTime.UtcNow.AddMinutes(-2),
        Duration = TimeSpan.FromMinutes(1),
        ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
    };

    private void VerifyUnbanCaseCreated(Times times) =>
        _moderationService.Verify(
            s => s.CreateCaseAsync(
                It.Is<ModerationCaseCreateDto>(d =>
                    d.Type == CaseType.Unban &&
                    d.GuildId == GuildId &&
                    d.TargetUserId == TargetId &&
                    d.ModeratorUserId == BotId &&
                    d.Reason == "Temporary ban expired (case #1)"),
                It.IsAny<CancellationToken>()),
            times);

    [Fact]
    public async Task ProcessExpiredBansAsync_ExpiredBan_RemovesBanAndRecordsUnbanCase()
    {
        ReturnExpired(ExpiredCase(CaseType.Ban));
        _guild.Setup(g => g.RemoveBanAsync(TargetId, It.IsAny<RequestOptions>())).Returns(Task.CompletedTask);

        var closed = await CreateService().ProcessExpiredBansAsync(CancellationToken.None);

        closed.Should().Be(1);
        _guild.Verify(g => g.RemoveBanAsync(
            TargetId,
            It.Is<RequestOptions>(o => o.AuditLogReason == "Temporary ban expired (case #1)")), Times.Once);
        VerifyUnbanCaseCreated(Times.Once());
    }

    [Fact]
    public async Task ProcessExpiredBansAsync_BanAlreadyLiftedInDiscord_StillClosesCase()
    {
        ReturnExpired(ExpiredCase(CaseType.Ban));
        _guild.Setup(g => g.RemoveBanAsync(TargetId, It.IsAny<RequestOptions>()))
            .ThrowsAsync(new HttpException(HttpStatusCode.NotFound, null!, DiscordErrorCode.UnknownBan));

        var closed = await CreateService().ProcessExpiredBansAsync(CancellationToken.None);

        closed.Should().Be(1);
        VerifyUnbanCaseCreated(Times.Once());
    }

    [Fact]
    public async Task ProcessExpiredBansAsync_UnbanFailsOtherwise_LeavesCaseOpenForRetry()
    {
        ReturnExpired(ExpiredCase(CaseType.Ban));
        _guild.Setup(g => g.RemoveBanAsync(TargetId, It.IsAny<RequestOptions>()))
            .ThrowsAsync(new HttpException(HttpStatusCode.Forbidden, null!, DiscordErrorCode.MissingPermissions));

        var closed = await CreateService().ProcessExpiredBansAsync(CancellationToken.None);

        closed.Should().Be(0);
        VerifyUnbanCaseCreated(Times.Never());
    }

    [Fact]
    public async Task ProcessExpiredBansAsync_GuildUnavailable_SkipsCaseForRetry()
    {
        ReturnExpired(ExpiredCase(CaseType.Ban));
        _guildLookupResult = null;

        var closed = await CreateService().ProcessExpiredBansAsync(CancellationToken.None);

        closed.Should().Be(0);
        _moderationService.Verify(
            s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessExpiredBansAsync_ExpiredMute_IsIgnored()
    {
        // Mutes are Discord timeouts that lift themselves; the service must never unban for one.
        ReturnExpired(ExpiredCase(CaseType.Mute));

        var closed = await CreateService().ProcessExpiredBansAsync(CancellationToken.None);

        closed.Should().Be(0);
        _guild.Verify(g => g.RemoveBanAsync(It.IsAny<ulong>(), It.IsAny<RequestOptions>()), Times.Never);
        _moderationService.Verify(
            s => s.CreateCaseAsync(It.IsAny<ModerationCaseCreateDto>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessExpiredBansAsync_OneBanFails_OthersAreStillLifted()
    {
        var failing = ExpiredCase(CaseType.Ban, caseNumber: 1);
        failing.TargetUserId = 301UL;
        ReturnExpired(failing, ExpiredCase(CaseType.Ban, caseNumber: 1));
        _guild.Setup(g => g.RemoveBanAsync(301UL, It.IsAny<RequestOptions>()))
            .ThrowsAsync(new TimeoutException());
        _guild.Setup(g => g.RemoveBanAsync(TargetId, It.IsAny<RequestOptions>())).Returns(Task.CompletedTask);

        var closed = await CreateService().ProcessExpiredBansAsync(CancellationToken.None);

        closed.Should().Be(1);
        VerifyUnbanCaseCreated(Times.Once());
    }

    [Fact]
    public void AddModerationServices_RegistersTemporaryBanExpiryServiceAsHostedService()
    {
        var services = new ServiceCollection();
        services.AddModerationServices(new ConfigurationBuilder().Build());

        services.Should().Contain(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(TemporaryBanExpiryService));
    }
}
