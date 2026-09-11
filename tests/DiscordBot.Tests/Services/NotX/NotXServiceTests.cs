using Discord.WebSocket;
using DiscordBot.Bot.Services.NotX;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services.NotX;

/// <summary>
/// Unit tests for <see cref="NotXService"/>, focused on the gating rules that decide
/// whether a tweet preview is posted at all. DiscordSocketClient is a concrete Discord.Net
/// type, so an unconnected instance is used for construction — the tests here all return
/// before the client is touched, matching the pattern used for other Discord-dependent
/// services in this project.
/// </summary>
public class NotXServiceTests : IDisposable
{
    private readonly DiscordSocketClient _client;
    private readonly Mock<INotXGuildSettingsRepository> _mockRepository;
    private readonly Mock<IFxTwitterClient> _mockFxTwitterClient;
    private readonly Mock<ISettingsService> _mockSettingsService;

    private const ulong GuildId = 111111111111111111UL;
    private const ulong ChannelId = 222222222222222222UL;
    private const ulong MessageId = 333333333333333333UL;
    private const string TweetUrl = "https://x.com/someone/status/1234567890";

    public NotXServiceTests()
    {
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = Discord.GatewayIntents.None
        });
        _mockRepository = new Mock<INotXGuildSettingsRepository>();
        _mockFxTwitterClient = new Mock<IFxTwitterClient>();
        _mockSettingsService = new Mock<ISettingsService>();

        // Default: the portal's Features toggle is on, so only the case under test gates.
        _mockSettingsService
            .Setup(s => s.GetSettingValueAsync<bool?>(
                NotXService.GlobalEnabledSettingKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    public void Dispose() => _client.Dispose();

    private NotXService CreateService(bool enabled)
    {
        return new NotXService(
            _mockRepository.Object,
            _mockFxTwitterClient.Object,
            _client,
            Options.Create(new NotXOptions { Enabled = enabled }),
            _mockSettingsService.Object,
            Mock.Of<ILogger<NotXService>>());
    }

    [Fact]
    public async Task ProcessTweetAsync_WhenDisabledInConfiguration_ShouldReturnFalse()
    {
        var service = CreateService(enabled: false);

        var result = await service.ProcessTweetAsync(GuildId, ChannelId, MessageId, TweetUrl);

        result.Should().BeFalse("the configuration kill switch turns the feature off entirely");
    }

    [Fact]
    public async Task ProcessTweetAsync_WhenDisabledInConfiguration_ShouldNotTouchSettingsOrFxTwitter()
    {
        var service = CreateService(enabled: false);

        await service.ProcessTweetAsync(GuildId, ChannelId, MessageId, TweetUrl);

        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "a disabled feature should not query per-guild settings");
        _mockFxTwitterClient.Verify(
            c => c.FetchTweetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "a disabled feature should make no outbound fxtwitter calls");
    }

    [Fact]
    public async Task ProcessTweetAsync_WhenDisabledInConfiguration_ShouldOutrankIgnoreSettingsGate()
    {
        // The "Fetch Tweet" context menu passes ignoreSettingsGate: true to bypass the
        // per-guild toggles. The global kill switch must still win, so the command cannot
        // post previews while the feature is off (its module is also deregistered).
        var service = CreateService(enabled: false);

        var result = await service.ProcessTweetAsync(
            GuildId, ChannelId, MessageId, TweetUrl, ignoreSettingsGate: true);

        result.Should().BeFalse();
        _mockFxTwitterClient.Verify(
            c => c.FetchTweetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessTweetAsync_WhenGlobalSettingIsDisabled_ShouldReturnFalseWithoutFetching()
    {
        // The portal's Features tab writes this setting; it takes effect with no restart.
        _mockSettingsService
            .Setup(s => s.GetSettingValueAsync<bool?>(
                NotXService.GlobalEnabledSettingKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService(enabled: true);

        var result = await service.ProcessTweetAsync(GuildId, ChannelId, MessageId, TweetUrl);

        result.Should().BeFalse();
        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockFxTwitterClient.Verify(
            c => c.FetchTweetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessTweetAsync_WhenGlobalSettingIsDisabled_ShouldOutrankIgnoreSettingsGate()
    {
        _mockSettingsService
            .Setup(s => s.GetSettingValueAsync<bool?>(
                NotXService.GlobalEnabledSettingKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService(enabled: true);

        var result = await service.ProcessTweetAsync(
            GuildId, ChannelId, MessageId, TweetUrl, ignoreSettingsGate: true);

        result.Should().BeFalse("the manual context-menu fetch must not bypass a global off switch");
    }

    [Fact]
    public async Task ProcessTweetAsync_WhenGlobalSettingRowIsAbsent_ShouldTreatFeatureAsEnabled()
    {
        // A null setting value means "no row saved yet" and must default to enabled, matching
        // how Features:AudioEnabled and Features:RatWatchEnabled are read elsewhere.
        _mockSettingsService
            .Setup(s => s.GetSettingValueAsync<bool?>(
                NotXService.GlobalEnabledSettingKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((bool?)null);
        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotXGuildSettings { GuildId = GuildId, IsEnabled = false });

        var service = CreateService(enabled: true);

        await service.ProcessTweetAsync(GuildId, ChannelId, MessageId, TweetUrl);

        // Reaching the per-guild lookup proves the absent row did not gate the request.
        _mockRepository.Verify(
            r => r.GetByGuildIdAsync(GuildId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessTweetAsync_WhenDisabledInConfiguration_ShouldNotEvenReadTheGlobalSetting()
    {
        // The config switch is the outermost gate, so it short-circuits before the settings
        // lookup — a disabled deployment does no settings work on the message hot path.
        var service = CreateService(enabled: false);

        await service.ProcessTweetAsync(GuildId, ChannelId, MessageId, TweetUrl);

        _mockSettingsService.Verify(
            s => s.GetSettingValueAsync<bool?>(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessTweetAsync_WhenEnabledInConfiguration_ShouldStillHonourPerGuildToggle()
    {
        // Regression guard: adding the global switch must not bypass the per-guild gate.
        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NotXGuildSettings { GuildId = GuildId, IsEnabled = false });

        var service = CreateService(enabled: true);

        var result = await service.ProcessTweetAsync(GuildId, ChannelId, MessageId, TweetUrl);

        result.Should().BeFalse("the guild has not enabled not-X");
        _mockRepository.Verify(r => r.GetByGuildIdAsync(GuildId, It.IsAny<CancellationToken>()), Times.Once);
        _mockFxTwitterClient.Verify(
            c => c.FetchTweetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessTweetAsync_WhenEnabledAndNoGuildSettingsExist_ShouldReturnFalse()
    {
        _mockRepository
            .Setup(r => r.GetByGuildIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NotXGuildSettings?)null);

        var service = CreateService(enabled: true);

        var result = await service.ProcessTweetAsync(GuildId, ChannelId, MessageId, TweetUrl);

        result.Should().BeFalse("not-X is opt-in per guild");
    }
}
