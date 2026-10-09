using Discord.WebSocket;
using DiscordBot.Bot.Services.Moderation;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services.Moderation;

/// <summary>
/// Unit tests for <see cref="ModLogNotifier"/>. The socket client here is never logged in, so
/// <c>GetGuild</c> returns null: these tests cover everything up to the point Discord is needed,
/// which is where the configuration decides whether anything is posted at all.
/// </summary>
public class ModLogNotifierTests
{
    private const ulong GuildId = 100UL;

    private readonly Mock<IGuildModerationConfigService> _configService = new();
    private readonly Mock<ILogger<ModLogNotifier>> _logger = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ModLogNotifier _notifier;

    public ModLogNotifierTests()
    {
        _notifier = new ModLogNotifier(
            new DiscordSocketClient(),
            _configService.Object,
            _cache,
            Options.Create(new ApplicationOptions { BaseUrl = "https://bot.example" }),
            _logger.Object);
    }

    private static ModerationCaseDto Case() => new()
    {
        Id = Guid.NewGuid(),
        CaseNumber = 1,
        GuildId = GuildId,
        TargetUserId = 300,
        ModeratorUserId = 200,
        Type = CaseType.Warn,
        CreatedAt = DateTime.UtcNow
    };

    private void ConfigReturns(ulong? channelId, ModLogEventKinds events) =>
        _configService
            .Setup(s => s.GetConfigAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildModerationConfigDto { GuildId = GuildId, ModLogChannelId = channelId, ModLogEvents = events });

    [Fact]
    public async Task CaseCreated_NoChannelConfigured_PostsNothing()
    {
        ConfigReturns(null, ModLogEventKinds.All);

        var result = await _notifier.CaseCreatedAsync(Case());

        result.Should().BeNull();
        _logger.Invocations.Should().NotContain(i => (LogLevel)i.Arguments[0] >= LogLevel.Warning);
    }

    [Fact]
    public async Task CaseCreated_CasesKindDisabled_PostsNothing()
    {
        ConfigReturns(555, ModLogEventKinds.FlaggedEvents | ModLogEventKinds.AutoActions);

        var result = await _notifier.CaseCreatedAsync(Case());

        result.Should().BeNull();
        _logger.Invocations.Should().NotContain(i => (LogLevel)i.Arguments[0] >= LogLevel.Warning);
    }

    [Fact]
    public async Task CaseCreated_GuildNotAvailable_PostsNothingAndDoesNotWarn()
    {
        // The bot being offline is not a configuration problem the admin can fix.
        ConfigReturns(555, ModLogEventKinds.All);

        var result = await _notifier.CaseCreatedAsync(Case());

        result.Should().BeNull();
        _logger.Invocations.Should().NotContain(i => (LogLevel)i.Arguments[0] >= LogLevel.Warning);
    }

    [Fact]
    public async Task CaseCreated_ConfigLookupThrows_IsSwallowedAndLogged()
    {
        _configService
            .Setup(s => s.GetConfigAsync(GuildId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"));

        var act = () => _notifier.CaseCreatedAsync(Case());

        (await act.Should().NotThrowAsync()).Which.Should().BeNull();
        _logger.Invocations.Should().Contain(i => (LogLevel)i.Arguments[0] == LogLevel.Error);
    }

    private static FlaggedEventDto Flagged() => new()
    {
        Id = Guid.NewGuid(),
        GuildId = GuildId,
        UserId = 300,
        RuleType = RuleType.Spam,
        Severity = Severity.High,
        CreatedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task FlaggedEvent_KindDisabled_PostsNothing()
    {
        ConfigReturns(555, ModLogEventKinds.Cases | ModLogEventKinds.AutoActions);

        var result = await _notifier.FlaggedEventAsync(Flagged(), ModLogFlaggedContext.None);

        result.Should().BeNull();
        _logger.Invocations.Should().NotContain(i => (LogLevel)i.Arguments[0] >= LogLevel.Warning);
    }

    [Fact]
    public async Task AutoAction_KindDisabled_PostsNothing()
    {
        ConfigReturns(555, ModLogEventKinds.Cases | ModLogEventKinds.FlaggedEvents);

        var result = await _notifier.AutoActionAsync(Flagged(), AutoAction.Delete, true, ModLogFlaggedContext.None);

        result.Should().BeNull();
        _logger.Invocations.Should().NotContain(i => (LogLevel)i.Arguments[0] >= LogLevel.Warning);
    }

    [Fact]
    public async Task FlaggedEvent_NoChannelConfigured_PostsNothing_EvenThoughAChannelMayBeNamedModLog()
    {
        // The name heuristic is gone: only the configured channel counts.
        ConfigReturns(null, ModLogEventKinds.All);

        var result = await _notifier.FlaggedEventAsync(Flagged(), ModLogFlaggedContext.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AutoAction_ConfigLookupThrows_IsSwallowedAndLogged()
    {
        _configService
            .Setup(s => s.GetConfigAsync(GuildId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database down"));

        var act = () => _notifier.AutoActionAsync(Flagged(), AutoAction.Kick, true, ModLogFlaggedContext.None);

        (await act.Should().NotThrowAsync()).Which.Should().BeNull();
        _logger.Invocations.Should().Contain(i => (LogLevel)i.Arguments[0] == LogLevel.Error);
    }
}
