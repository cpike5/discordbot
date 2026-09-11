using DiscordBot.Bot.Services.Realtime;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.Realtime;

/// <summary>
/// Unit tests for <see cref="DashboardEventBus"/>: subscribe/publish delivery, unsubscribe,
/// subscriber fault isolation, and guild-scoped filtering.
/// </summary>
public class DashboardEventBusTests
{
    private readonly Mock<ILogger<DashboardEventBus>> _mockLogger;
    private readonly DashboardEventBus _bus;

    public DashboardEventBusTests()
    {
        _mockLogger = new Mock<ILogger<DashboardEventBus>>();
        _bus = new DashboardEventBus(_mockLogger.Object);
    }

    private sealed record TestEvent(int Value) : IDashboardEvent
    {
        public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    }

    private sealed record TestGuildEvent : GuildScopedEvent
    {
        public required int Value { get; init; }
    }

    private sealed record TestUserEvent : UserScopedEvent
    {
        public required int Value { get; init; }
    }

    [Fact]
    public async Task PublishAsync_WithSubscriber_DeliversTheEvent()
    {
        // Arrange
        TestEvent? received = null;
        using var subscription = _bus.Subscribe<TestEvent>((evt, _) =>
        {
            received = evt;
            return Task.CompletedTask;
        });

        // Act
        await _bus.PublishAsync(new TestEvent(42));

        // Assert
        received.Should().NotBeNull();
        received!.Value.Should().Be(42);
    }

    [Fact]
    public async Task PublishAsync_WithNoSubscribers_CompletesWithoutError()
    {
        // Act
        var act = () => _bus.PublishAsync(new TestEvent(1));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_WithMultipleSubscribers_DeliversToAll()
    {
        // Arrange
        var receivedByFirst = 0;
        var receivedBySecond = 0;
        using var s1 = _bus.Subscribe<TestEvent>((evt, _) => { receivedByFirst = evt.Value; return Task.CompletedTask; });
        using var s2 = _bus.Subscribe<TestEvent>((evt, _) => { receivedBySecond = evt.Value; return Task.CompletedTask; });

        // Act
        await _bus.PublishAsync(new TestEvent(7));

        // Assert
        receivedByFirst.Should().Be(7);
        receivedBySecond.Should().Be(7);
    }

    [Fact]
    public async Task Dispose_StopsFurtherDelivery()
    {
        // Arrange
        var callCount = 0;
        var subscription = _bus.Subscribe<TestEvent>((_, _) => { callCount++; return Task.CompletedTask; });

        // Act
        subscription.Dispose();
        await _bus.PublishAsync(new TestEvent(1));

        // Assert
        callCount.Should().Be(0, "the subscription was disposed before publish");
    }

    [Fact]
    public async Task PublishAsync_WhenOneHandlerThrows_StillDeliversToOtherHandlers()
    {
        // Arrange
        var goodHandlerCalled = false;
        using var faulty = _bus.Subscribe<TestEvent>((_, _) => throw new InvalidOperationException("boom"));
        using var good = _bus.Subscribe<TestEvent>((_, _) => { goodHandlerCalled = true; return Task.CompletedTask; });

        // Act
        var act = () => _bus.PublishAsync(new TestEvent(1));

        // Assert
        await act.Should().NotThrowAsync("a throwing subscriber must never fault the publisher");
        goodHandlerCalled.Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_WhenHandlerTaskFaults_DoesNotFaultPublishAsync()
    {
        // Arrange
        using var faulty = _bus.Subscribe<TestEvent>((_, _) => Task.FromException(new InvalidOperationException("boom")));

        // Act
        var act = () => _bus.PublishAsync(new TestEvent(1));

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_LogsWarningWithEventTypeWhenHandlerThrows()
    {
        // Arrange
        using var faulty = _bus.Subscribe<TestEvent>((_, _) => throw new InvalidOperationException("boom"));

        // Act
        await _bus.PublishAsync(new TestEvent(1));

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(nameof(TestEvent))),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Subscribe_WithGuildFilter_OnlyReceivesMatchingGuild()
    {
        // Arrange
        const ulong targetGuild = 111;
        const ulong otherGuild = 222;
        var received = new List<TestGuildEvent>();
        using var subscription = _bus.Subscribe<TestGuildEvent>(targetGuild, (evt, _) =>
        {
            received.Add(evt);
            return Task.CompletedTask;
        });

        // Act
        await _bus.PublishAsync(new TestGuildEvent { GuildId = otherGuild, Value = 1 });
        await _bus.PublishAsync(new TestGuildEvent { GuildId = targetGuild, Value = 2 });

        // Assert
        received.Should().ContainSingle();
        received[0].GuildId.Should().Be(targetGuild);
        received[0].Value.Should().Be(2);
    }

    [Fact]
    public async Task PublishAsync_DisposedSubscriberDuringPublish_DoesNotAffectOtherSubscribers()
    {
        // Arrange - a handler that disposes its own subscription while running, plus a sibling
        // handler. Neither the disposal nor the concurrent publish should throw or skip the sibling.
        IDisposable? selfSubscription = null;
        var siblingCalled = false;

        selfSubscription = _bus.Subscribe<TestEvent>((_, _) =>
        {
            selfSubscription!.Dispose();
            return Task.CompletedTask;
        });
        using var sibling = _bus.Subscribe<TestEvent>((_, _) => { siblingCalled = true; return Task.CompletedTask; });

        // Act
        var act = () => _bus.PublishAsync(new TestEvent(1));

        // Assert
        await act.Should().NotThrowAsync();
        siblingCalled.Should().BeTrue();

        // A second publish should not reach the disposed handler.
        var secondRoundCount = 0;
        selfSubscription.Dispose(); // idempotent
        using var check = _bus.Subscribe<TestEvent>((_, _) => { secondRoundCount++; return Task.CompletedTask; });
        await _bus.PublishAsync(new TestEvent(2));
        secondRoundCount.Should().Be(1);
    }

    [Fact]
    public async Task Subscribe_WithUserFilter_OnlyReceivesMatchingUser()
    {
        // Arrange
        const string targetUser = "user-1";
        const string otherUser = "user-2";
        var received = new List<TestUserEvent>();
        using var subscription = _bus.Subscribe<TestUserEvent>(targetUser, (evt, _) =>
        {
            received.Add(evt);
            return Task.CompletedTask;
        });

        // Act
        await _bus.PublishAsync(new TestUserEvent { UserId = otherUser, Value = 1 });
        await _bus.PublishAsync(new TestUserEvent { UserId = targetUser, Value = 2 });

        // Assert
        received.Should().ContainSingle();
        received[0].UserId.Should().Be(targetUser);
        received[0].Value.Should().Be(2);
    }

    [Fact]
    public void HasSubscribers_WithNoSubscriber_ReturnsFalse()
    {
        // Assert
        _bus.HasSubscribers<TestEvent>().Should().BeFalse();
    }

    [Fact]
    public void HasSubscribers_WithSubscriber_ReturnsTrue()
    {
        // Arrange
        using var subscription = _bus.Subscribe<TestEvent>((_, _) => Task.CompletedTask);

        // Assert
        _bus.HasSubscribers<TestEvent>().Should().BeTrue();
    }

    [Fact]
    public void HasSubscribers_AfterUnsubscribe_ReturnsFalse()
    {
        // Arrange
        var subscription = _bus.Subscribe<TestEvent>((_, _) => Task.CompletedTask);
        subscription.Dispose();

        // Assert
        _bus.HasSubscribers<TestEvent>().Should().BeFalse();
    }

    [Fact]
    public void HasSubscribers_DoesNotCrossEventTypes()
    {
        // Arrange - a subscriber for a different event type must not count for TestEvent.
        using var subscription = _bus.Subscribe<TestGuildEvent>((_, _) => Task.CompletedTask);

        // Assert
        _bus.HasSubscribers<TestEvent>().Should().BeFalse();
    }
}
