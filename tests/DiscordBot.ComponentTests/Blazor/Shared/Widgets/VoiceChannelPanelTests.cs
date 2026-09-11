using Bunit;
using Discord.Audio;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Realtime;
using DiscordBot.Bot.Services.Realtime.Events;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Shared.Widgets;

public class VoiceChannelPanelTests : BlazorComponentTestContext
{
    private const string GuildId = "123456789012345678";
    private const ulong ParsedGuildId = 123456789012345678UL;

    private readonly Mock<IDashboardAudioStatusService> _audioStatusService = new();
    private readonly Mock<IAudioService> _audioService = new();
    private readonly Mock<IPlaybackService> _playbackService = new();

    public VoiceChannelPanelTests()
    {
        _audioStatusService
            .Setup(s => s.GetCurrentAudioStatus(ParsedGuildId, null, null))
            .Returns(new AudioStatusDto { GuildId = ParsedGuildId, IsConnected = false });

        Services.AddSingleton(_audioStatusService.Object);
        Services.AddSingleton(_audioService.Object);
        Services.AddSingleton(_playbackService.Object);
    }

    [Fact]
    public void OnInitialized_LoadsCurrentAudioStatus_FromDashboardAudioStatusService()
    {
        var cut = Render<VoiceChannelPanel>(p => p.Add(x => x.GuildId, GuildId));

        cut.Find("#voice-channel-panel").GetAttribute("data-connected").Should().Be("false");
        _audioStatusService.Verify(s => s.GetCurrentAudioStatus(ParsedGuildId, null, null), Times.Once);
    }

    [Fact]
    public void SelectingChannel_CallsAudioServiceJoinChannelAsync_AndRaisesOnJoined()
    {
        _audioService
            .Setup(s => s.JoinChannelAsync(ParsedGuildId, 42UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IAudioClient>());

        string? joinedChannelId = null;
        var cut = Render<VoiceChannelPanel>(p => p
            .Add(x => x.GuildId, GuildId)
            .Add(x => x.AvailableChannels, new[] { new VoiceChannelInfo { Id = 42, Name = "General", MemberCount = 2 } })
            .Add(x => x.OnJoined, id => joinedChannelId = id));

        cut.Find("#channel-selector").Change("42");

        _audioService.Verify(s => s.JoinChannelAsync(ParsedGuildId, 42UL, It.IsAny<CancellationToken>()), Times.Once);
        joinedChannelId.Should().Be("42");
    }

    [Fact]
    public void LeaveButton_StopsPlayback_ThenLeavesChannel_AndRaisesOnLeft()
    {
        _audioStatusService
            .Setup(s => s.GetCurrentAudioStatus(ParsedGuildId, null, null))
            .Returns(new AudioStatusDto { GuildId = ParsedGuildId, IsConnected = true, ChannelId = 42, ChannelName = "General" });
        _audioService.Setup(s => s.LeaveChannelAsync(ParsedGuildId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var left = false;
        var cut = Render<VoiceChannelPanel>(p => p
            .Add(x => x.GuildId, GuildId)
            .Add(x => x.OnLeft, () => left = true));

        cut.Find("#leave-channel-btn").Click();

        _playbackService.Verify(s => s.StopAsync(ParsedGuildId, It.IsAny<CancellationToken>()), Times.Once);
        _audioService.Verify(s => s.LeaveChannelAsync(ParsedGuildId, It.IsAny<CancellationToken>()), Times.Once);
        left.Should().BeTrue();
    }

    [Fact]
    public void SkipButton_RemovesFromQueue_AndRaisesOnSkipped()
    {
        _playbackService.Setup(s => s.RemoveFromQueueAsync(ParsedGuildId, 1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        int? skippedPosition = null;
        var cut = Render<VoiceChannelPanel>(p => p
            .Add(x => x.GuildId, GuildId)
            .Add(x => x.Queue, new[] { new QueueItemInfo { Position = 1, Name = "airhorn.mp3", DurationSeconds = 2 } })
            .Add(x => x.OnSkipped, pos => skippedPosition = pos));

        cut.Find("button[title='Skip to next']").Click();

        skippedPosition.Should().Be(1);
    }

    [Fact]
    public async Task QueueUpdatedEvent_GuildScoped_ReplacesQueue()
    {
        var cut = Render<VoiceChannelPanel>(p => p.Add(x => x.GuildId, GuildId));
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        await bus.PublishAsync(new QueueUpdatedEvent
        {
            GuildId = ParsedGuildId,
            Queue = new QueueUpdatedDto
            {
                GuildId = ParsedGuildId,
                Queue = [new QueueItemDto { Position = 1, Name = "new-sound.mp3", DurationSeconds = 3 }]
            }
        });

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("new-sound.mp3"), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task QueueUpdatedEvent_ForDifferentGuild_IsIgnored()
    {
        var cut = Render<VoiceChannelPanel>(p => p.Add(x => x.GuildId, GuildId));
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        await bus.PublishAsync(new QueueUpdatedEvent
        {
            GuildId = ParsedGuildId + 1,
            Queue = new QueueUpdatedDto { GuildId = ParsedGuildId + 1, Queue = [new QueueItemDto { Position = 1, Name = "not-ours.mp3" }] }
        });

        await Task.Delay(TimeSpan.FromSeconds(1.2));
        cut.Markup.Should().NotContain("not-ours.mp3");
    }

    [Fact]
    public async Task AudioConnectedEvent_UpdatesConnectionState()
    {
        var cut = Render<VoiceChannelPanel>(p => p.Add(x => x.GuildId, GuildId));
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        await bus.PublishAsync(new AudioConnectedEvent
        {
            GuildId = ParsedGuildId,
            Data = new AudioConnectedDto { GuildId = ParsedGuildId, ChannelId = 42, ChannelName = "General", MemberCount = 3 }
        });

        cut.WaitForAssertion(
            () => cut.Find("#voice-channel-panel").GetAttribute("data-connected").Should().Be("true"),
            TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task Dispose_UnsubscribesFromBus()
    {
        var cut = Render<VoiceChannelPanel>(p => p.Add(x => x.GuildId, GuildId));
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        await DisposeComponentsAsync();
        var renderCountAfterDispose = cut.RenderCount;

        await bus.PublishAsync(new AudioConnectedEvent
        {
            GuildId = ParsedGuildId,
            Data = new AudioConnectedDto { GuildId = ParsedGuildId, ChannelId = 1, ChannelName = "x", MemberCount = 1 }
        });
        await Task.Delay(TimeSpan.FromSeconds(1.5));

        cut.RenderCount.Should().Be(renderCountAfterDispose);
    }

    [Fact]
    public void IsCompact_AppliesCompactClass()
    {
        var cut = Render<VoiceChannelPanel>(p => p.Add(x => x.GuildId, GuildId).Add(x => x.IsCompact, true));
        cut.Find("#voice-channel-panel").ClassList.Should().Contain("voice-panel-compact");
    }
}
