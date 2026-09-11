using DiscordBot.Bot.Hubs;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Realtime;
using DiscordBot.Bot.Services.Realtime.Events;
using DiscordBot.Core.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AudioNotifier"/>'s dual-publish to <see cref="IDashboardEventBus"/>
/// alongside its existing guild-audio-group SignalR sends.
/// </summary>
public class AudioNotifierTests
{
    private const ulong GuildId = 111222333;

    private readonly Mock<IHubContext<DashboardHub>> _mockHubContext;
    private readonly IDashboardEventBus _eventBus;
    private readonly AudioNotifier _notifier;

    public AudioNotifierTests()
    {
        _mockHubContext = new Mock<IHubContext<DashboardHub>>();
        _eventBus = new DashboardEventBus(new Mock<ILogger<DashboardEventBus>>().Object);

        var mockClients = new Mock<IHubClients>();
        var mockGroupProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockGroupProxy.Object);
        _mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        _notifier = new AudioNotifier(_mockHubContext.Object, _eventBus, new Mock<ILogger<AudioNotifier>>().Object);
    }

    [Fact]
    public async Task NotifyAudioConnectedAsync_ShouldDualPublishGuildScopedEvent()
    {
        AudioConnectedEvent? received = null;
        using var s = _eventBus.Subscribe<AudioConnectedEvent>(GuildId, (evt, _) => { received = evt; return Task.CompletedTask; });

        await _notifier.NotifyAudioConnectedAsync(GuildId, 987, "General", 3);

        received.Should().NotBeNull();
        received!.GuildId.Should().Be(GuildId);
        received.Data.ChannelName.Should().Be("General");
    }

    [Fact]
    public async Task NotifyAudioDisconnectedAsync_ShouldDualPublishGuildScopedEvent()
    {
        AudioDisconnectedEvent? received = null;
        using var s = _eventBus.Subscribe<AudioDisconnectedEvent>(GuildId, (evt, _) => { received = evt; return Task.CompletedTask; });

        await _notifier.NotifyAudioDisconnectedAsync(GuildId, "Idle timeout");

        received.Should().NotBeNull();
        received!.Data.Reason.Should().Be("Idle timeout");
    }

    [Fact]
    public async Task NotifyPlaybackStartedAsync_ShouldDualPublishGuildScopedEvent()
    {
        PlaybackStartedEvent? received = null;
        using var s = _eventBus.Subscribe<PlaybackStartedEvent>(GuildId, (evt, _) => { received = evt; return Task.CompletedTask; });

        await _notifier.NotifyPlaybackStartedAsync(GuildId, Guid.NewGuid(), "Airhorn", 2.5);

        received.Should().NotBeNull();
        received!.Data.Name.Should().Be("Airhorn");
    }

    [Fact]
    public async Task NotifyPlaybackProgressAsync_ShouldDualPublishGuildScopedEvent()
    {
        var soundId = Guid.NewGuid();
        PlaybackProgressEvent? received = null;
        using var s = _eventBus.Subscribe<PlaybackProgressEvent>(GuildId, (evt, _) => { received = evt; return Task.CompletedTask; });

        await _notifier.NotifyPlaybackProgressAsync(GuildId, soundId, 1.2, 2.5);

        received.Should().NotBeNull();
        received!.Data.SoundId.Should().Be(soundId);
    }

    [Fact]
    public async Task NotifyPlaybackFinishedAsync_ShouldDualPublishGuildScopedEvent()
    {
        PlaybackFinishedEvent? received = null;
        using var s = _eventBus.Subscribe<PlaybackFinishedEvent>(GuildId, (evt, _) => { received = evt; return Task.CompletedTask; });

        await _notifier.NotifyPlaybackFinishedAsync(GuildId, Guid.NewGuid(), wasCancelled: false);

        received.Should().NotBeNull();
        received!.Data.WasCancelled.Should().BeFalse();
    }

    [Fact]
    public async Task NotifyQueueUpdatedAsync_ShouldDualPublishGuildScopedEvent()
    {
        var queue = new QueueUpdatedDto();
        QueueUpdatedEvent? received = null;
        using var s = _eventBus.Subscribe<QueueUpdatedEvent>(GuildId, (evt, _) => { received = evt; return Task.CompletedTask; });

        await _notifier.NotifyQueueUpdatedAsync(GuildId, queue);

        received.Should().NotBeNull();
        received!.Queue.Should().BeSameAs(queue);
    }

    [Fact]
    public async Task NotifyVoiceChannelMemberCountUpdatedAsync_ShouldDualPublishGuildScopedEvent()
    {
        VoiceChannelMemberCountUpdatedEvent? received = null;
        using var s = _eventBus.Subscribe<VoiceChannelMemberCountUpdatedEvent>(GuildId, (evt, _) => { received = evt; return Task.CompletedTask; });

        await _notifier.NotifyVoiceChannelMemberCountUpdatedAsync(GuildId, 987, "General", 5);

        received.Should().NotBeNull();
        received!.Data.MemberCount.Should().Be(5);
    }

    [Fact]
    public async Task NotifySoundUploadedAsync_ShouldDualPublishGuildScopedEvent()
    {
        SoundUploadedEvent? received = null;
        using var s = _eventBus.Subscribe<SoundUploadedEvent>(GuildId, (evt, _) => { received = evt; return Task.CompletedTask; });

        await _notifier.NotifySoundUploadedAsync(GuildId, Guid.NewGuid(), "Airhorn", 0);

        received.Should().NotBeNull();
        received!.Data.Name.Should().Be("Airhorn");
    }

    [Fact]
    public async Task NotifySoundDeletedAsync_ShouldDualPublishGuildScopedEvent()
    {
        var soundId = Guid.NewGuid();
        SoundDeletedEvent? received = null;
        using var s = _eventBus.Subscribe<SoundDeletedEvent>(GuildId, (evt, _) => { received = evt; return Task.CompletedTask; });

        await _notifier.NotifySoundDeletedAsync(GuildId, soundId);

        received.Should().NotBeNull();
        received!.Data.SoundId.Should().Be(soundId);
    }

    [Fact]
    public async Task NotifySoundDeletedAsync_WithGuildFilterForDifferentGuild_ShouldNotDeliver()
    {
        const ulong otherGuildId = 999;
        var received = false;
        using var s = _eventBus.Subscribe<SoundDeletedEvent>(otherGuildId, (_, _) => { received = true; return Task.CompletedTask; });

        await _notifier.NotifySoundDeletedAsync(GuildId, Guid.NewGuid());

        received.Should().BeFalse("the subscription is scoped to a different guild");
    }
}
