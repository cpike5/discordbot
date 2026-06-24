using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="SpamDetectionService"/> — a security-critical service that detects
/// message floods, duplicate spam, and mass-mention abuse over a sliding window. Each test uses a
/// fresh in-memory cache so message counts are isolated.
/// </summary>
public class SpamDetectionServiceTests
{
    private const ulong GuildId = 100;
    private const ulong UserId = 200;
    private const ulong ChannelId = 300;

    private static readonly DateTime OldAccount = DateTime.UtcNow.AddYears(-1);
    private static readonly DateTime NewAccount = DateTime.UtcNow.AddDays(-1);

    private static SpamDetectionService CreateService(SpamDetectionConfigDto config)
    {
        var configService = new Mock<IGuildModerationConfigService>();
        configService
            .Setup(s => s.GetSpamConfigAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(sp => sp.GetService(typeof(IGuildModerationConfigService)))
            .Returns(configService.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return new SpamDetectionService(
            scopeFactory.Object,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<SpamDetectionService>.Instance,
            Options.Create(new AutoModerationOptions()));
    }

    /// <summary>
    /// Sends <paramref name="count"/> messages through the analyzer and returns the last result.
    /// Each message has distinct content (unless <paramref name="identicalContent"/> is set) to
    /// avoid inadvertently tripping duplicate detection during flood tests.
    /// </summary>
    private static async Task<DetectionResultDto?> SendMessagesAsync(
        SpamDetectionService service,
        int count,
        DateTime accountCreated,
        string? identicalContent = null,
        string contentPrefix = "message")
    {
        DetectionResultDto? last = null;
        for (var i = 0; i < count; i++)
        {
            var content = identicalContent ?? $"{contentPrefix} {i}";
            last = await service.AnalyzeMessageAsync(
                GuildId, UserId, ChannelId, content, messageId: (ulong)(1000 + i), accountCreated);
        }

        return last;
    }

    [Fact]
    public async Task AnalyzeMessageAsync_WhenDisabled_ReturnsNull()
    {
        var service = CreateService(new SpamDetectionConfigDto { Enabled = false });

        var result = await SendMessagesAsync(service, 20, OldAccount);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeMessageAsync_BelowThreshold_ReturnsNull()
    {
        var service = CreateService(new SpamDetectionConfigDto { MaxMessagesPerWindow = 5, WindowSeconds = 60 });

        var result = await SendMessagesAsync(service, 4, OldAccount);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeMessageAsync_AtThreshold_FlagsMediumFlood()
    {
        var service = CreateService(new SpamDetectionConfigDto
        {
            MaxMessagesPerWindow = 5,
            WindowSeconds = 60,
            AutoAction = AutoAction.Delete
        });

        var result = await SendMessagesAsync(service, 5, OldAccount);

        result.Should().NotBeNull();
        result!.RuleType.Should().Be(RuleType.Spam);
        result.Severity.Should().Be(Severity.Medium);
        result.ShouldAutoAction.Should().BeTrue();
        result.RecommendedAction.Should().Be(AutoAction.Delete);
    }

    [Fact]
    public async Task AnalyzeMessageAsync_AtOneAndHalfThreshold_FlagsHighFlood()
    {
        var service = CreateService(new SpamDetectionConfigDto { MaxMessagesPerWindow = 5, WindowSeconds = 60 });

        // 8 >= 5 * 1.5 (7.5) but < 5 * 2 (10) → High
        var result = await SendMessagesAsync(service, 8, OldAccount);

        result.Should().NotBeNull();
        result!.Severity.Should().Be(Severity.High);
    }

    [Fact]
    public async Task AnalyzeMessageAsync_AtDoubleThreshold_FlagsCriticalFlood()
    {
        var service = CreateService(new SpamDetectionConfigDto { MaxMessagesPerWindow = 5, WindowSeconds = 60 });

        var result = await SendMessagesAsync(service, 10, OldAccount);

        result.Should().NotBeNull();
        result!.Severity.Should().Be(Severity.Critical);
    }

    [Fact]
    public async Task AnalyzeMessageAsync_IdenticalMessages_FlagsDuplicateSpam()
    {
        // High flood threshold so the 3 identical messages do not trip flood detection first.
        var service = CreateService(new SpamDetectionConfigDto { MaxMessagesPerWindow = 50, WindowSeconds = 60 });

        var result = await SendMessagesAsync(service, 3, OldAccount, identicalContent: "buy now at spam.example");

        result.Should().NotBeNull();
        result!.Severity.Should().Be(Severity.Medium);
        result.Description.Should().Contain("Duplicate");
    }

    [Fact]
    public async Task AnalyzeMessageAsync_EveryoneMentionAbuse_FlagsHigh()
    {
        var service = CreateService(new SpamDetectionConfigDto { MaxMessagesPerWindow = 50, WindowSeconds = 60 });

        // Two distinct messages that both mention @everyone (count >= 2, not flood, not duplicate).
        var result = await SendMessagesAsync(service, 2, OldAccount, contentPrefix: "hey @everyone look");

        result.Should().NotBeNull();
        result!.Severity.Should().Be(Severity.High);
        result.Description.Should().Contain("@everyone");
    }

    [Fact]
    public async Task AnalyzeMessageAsync_NewAccount_HasStricterThreshold()
    {
        // Adjusted threshold for new accounts: floor(5 * 0.7) = 3, so 3 messages trips Medium.
        var service = CreateService(new SpamDetectionConfigDto { MaxMessagesPerWindow = 5, WindowSeconds = 60 });

        var result = await SendMessagesAsync(service, 3, NewAccount);

        result.Should().NotBeNull();
        result!.Severity.Should().Be(Severity.Medium);
    }

    [Fact]
    public async Task AnalyzeMessageAsync_OldAccount_NotFlaggedAtNewAccountThreshold()
    {
        // The same 3 messages from an established account stay under the unadjusted threshold of 5.
        var service = CreateService(new SpamDetectionConfigDto { MaxMessagesPerWindow = 5, WindowSeconds = 60 });

        var result = await SendMessagesAsync(service, 3, OldAccount);

        result.Should().BeNull();
    }
}
