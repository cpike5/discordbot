using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ContentFilterService"/> — a security-critical service that screens
/// message content against per-guild blocklists, invite-link rules, and link whitelists.
/// </summary>
public class ContentFilterServiceTests
{
    private const ulong GuildId = 100;
    private const ulong UserId = 200;
    private const ulong ChannelId = 300;
    private const ulong MessageId = 400;

    private static ContentFilterService CreateService(ContentFilterConfigDto config)
    {
        var configService = new Mock<IGuildModerationConfigService>();
        configService
            .Setup(s => s.GetContentFilterConfigAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(config);

        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(sp => sp.GetService(typeof(IGuildModerationConfigService)))
            .Returns(configService.Object);

        var scope = new Mock<IServiceScope>();
        scope.Setup(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        return new ContentFilterService(
            scopeFactory.Object,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ContentFilterService>.Instance);
    }

    private static Task<DetectionResultDto?> AnalyzeAsync(ContentFilterService service, string content)
        => service.AnalyzeMessageAsync(GuildId, content, UserId, ChannelId, MessageId);

    [Fact]
    public async Task AnalyzeMessageAsync_WhenDisabled_ReturnsNull()
    {
        var service = CreateService(new ContentFilterConfigDto { Enabled = false, ProhibitedWords = { "badword" } });

        var result = await AnalyzeAsync(service, "this contains badword");

        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeMessageAsync_ProhibitedWord_IsDetectedAsHighSeverity()
    {
        var service = CreateService(new ContentFilterConfigDto
        {
            ProhibitedWords = { "forbidden" },
            AutoAction = AutoAction.Delete
        });

        var result = await AnalyzeAsync(service, "this is a forbidden message");

        result.Should().NotBeNull();
        result!.RuleType.Should().Be(RuleType.Content);
        result.Severity.Should().Be(Severity.High);
        result.ShouldAutoAction.Should().BeTrue();
        result.RecommendedAction.Should().Be(AutoAction.Delete);
    }

    [Fact]
    public async Task AnalyzeMessageAsync_ProhibitedWord_IsCaseInsensitive()
    {
        var service = CreateService(new ContentFilterConfigDto { ProhibitedWords = { "forbidden" } });

        var result = await AnalyzeAsync(service, "FORBIDDEN content here");

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeMessageAsync_ProhibitedWord_RespectsWordBoundaries()
    {
        // "class" must not match inside "classic" because the filter uses word boundaries.
        var service = CreateService(new ContentFilterConfigDto { ProhibitedWords = { "class" } });

        var result = await AnalyzeAsync(service, "this is a classic example");

        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeMessageAsync_WhenAutoActionNone_DoesNotRecommendAutoAction()
    {
        var service = CreateService(new ContentFilterConfigDto
        {
            ProhibitedWords = { "forbidden" },
            AutoAction = AutoAction.None
        });

        var result = await AnalyzeAsync(service, "forbidden");

        result.Should().NotBeNull();
        result!.ShouldAutoAction.Should().BeFalse();
        result.RecommendedAction.Should().Be(AutoAction.None);
    }

    [Fact]
    public async Task AnalyzeMessageAsync_InviteLink_IsDetectedWhenBlocked()
    {
        var service = CreateService(new ContentFilterConfigDto { BlockInviteLinks = true });

        var result = await AnalyzeAsync(service, "join here discord.gg/abc123");

        result.Should().NotBeNull();
        result!.Severity.Should().Be(Severity.Medium);
        result.Description.Should().Contain("invite");
    }

    [Fact]
    public async Task AnalyzeMessageAsync_InviteLink_IgnoredWhenNotBlocked()
    {
        var service = CreateService(new ContentFilterConfigDto { BlockInviteLinks = false });

        var result = await AnalyzeAsync(service, "join here discord.gg/abc123");

        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeMessageAsync_UnlistedLink_IsDetected()
    {
        var service = CreateService(new ContentFilterConfigDto
        {
            BlockUnlistedLinks = true,
            AllowedLinkDomains = { "trusted.com" }
        });

        var result = await AnalyzeAsync(service, "check out https://evil.com/path");

        result.Should().NotBeNull();
        result!.Severity.Should().Be(Severity.Medium);
    }

    [Fact]
    public async Task AnalyzeMessageAsync_AllowedLink_IsNotDetected()
    {
        var service = CreateService(new ContentFilterConfigDto
        {
            BlockUnlistedLinks = true,
            AllowedLinkDomains = { "trusted.com" }
        });

        var result = await AnalyzeAsync(service, "see https://trusted.com/page");

        result.Should().BeNull();
    }

    [Fact]
    public async Task AnalyzeMessageAsync_CleanMessage_ReturnsNull()
    {
        var service = CreateService(new ContentFilterConfigDto { ProhibitedWords = { "forbidden" } });

        var result = await AnalyzeAsync(service, "a perfectly nice message");

        result.Should().BeNull();
    }
}
