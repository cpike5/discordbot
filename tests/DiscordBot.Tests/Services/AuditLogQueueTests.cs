using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AuditLogQueue"/>.
/// Tests cover enqueue, dequeue, and thread-safety scenarios.
/// </summary>
[Trait("Category", "Unit")]
public class AuditLogQueueTests
{
    private readonly Mock<ILogger<AuditLogQueue>> _mockLogger;

    public AuditLogQueueTests()
    {
        _mockLogger = new Mock<ILogger<AuditLogQueue>>();
    }

    #region Enqueue Tests

    [Fact]
    public void Enqueue_AddsToChannel()
    {
        // Arrange
        var queue = new AuditLogQueue(_mockLogger.Object);
        var dto = CreateTestDto();

        // Act
        queue.Enqueue(dto);

        // Assert
        queue.Count.Should().Be(1, "one item should be in the queue after enqueue");
    }

    [Fact]
    public void Enqueue_MultipleItems_IncreasesCount()
    {
        // Arrange
        var queue = new AuditLogQueue(_mockLogger.Object);

        // Act
        queue.Enqueue(CreateTestDto());
        queue.Enqueue(CreateTestDto());
        queue.Enqueue(CreateTestDto());

        // Assert
        queue.Count.Should().Be(3, "count should reflect the number of enqueued items");
    }

    #endregion

    #region DequeueAsync Tests

    [Fact]
    public async Task DequeueAsync_ReturnsEnqueuedItem()
    {
        // Arrange
        var queue = new AuditLogQueue(_mockLogger.Object);
        var dto = CreateTestDto(category: AuditLogCategory.User, action: AuditLogAction.Created);
        queue.Enqueue(dto);

        // Act
        var result = await queue.DequeueAsync();

        // Assert
        result.Should().NotBeNull();
        result.Category.Should().Be(AuditLogCategory.User);
        result.Action.Should().Be(AuditLogAction.Created);
        result.ActorId.Should().Be(dto.ActorId);
    }

    [Fact]
    public async Task DequeueAsync_MaintainsFIFOOrder()
    {
        // Arrange
        var queue = new AuditLogQueue(_mockLogger.Object);
        var dto1 = CreateTestDto(action: AuditLogAction.Created, actorId: "actor1");
        var dto2 = CreateTestDto(action: AuditLogAction.Updated, actorId: "actor2");
        var dto3 = CreateTestDto(action: AuditLogAction.Deleted, actorId: "actor3");

        queue.Enqueue(dto1);
        queue.Enqueue(dto2);
        queue.Enqueue(dto3);

        // Act & Assert
        var result1 = await queue.DequeueAsync();
        result1.Action.Should().Be(AuditLogAction.Created);
        result1.ActorId.Should().Be("actor1");

        var result2 = await queue.DequeueAsync();
        result2.Action.Should().Be(AuditLogAction.Updated);
        result2.ActorId.Should().Be("actor2");

        var result3 = await queue.DequeueAsync();
        result3.Action.Should().Be(AuditLogAction.Deleted);
        result3.ActorId.Should().Be("actor3");
    }

    [Fact]
    public async Task DequeueAsync_DecreasesCount()
    {
        // Arrange
        var queue = new AuditLogQueue(_mockLogger.Object);
        queue.Enqueue(CreateTestDto());
        queue.Enqueue(CreateTestDto());
        queue.Enqueue(CreateTestDto());

        // Act
        await queue.DequeueAsync();

        // Assert
        queue.Count.Should().Be(2, "count should decrease after dequeue");
    }

    [Fact]
    public async Task DequeueAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        var queue = new AuditLogQueue(_mockLogger.Object);
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        Func<Task> act = async () => await queue.DequeueAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>("dequeue should respect cancellation token");
    }

    [Fact]
    public async Task DequeueAsync_WaitsForEnqueue_WhenQueueEmpty()
    {
        // Arrange
        var queue = new AuditLogQueue(_mockLogger.Object);
        var dto = CreateTestDto();
        AuditLogCreateDto? result = null;

        // Act
        var dequeueTask = Task.Run(async () =>
        {
            result = await queue.DequeueAsync();
        });

        // Give dequeue a moment to start waiting
        await Task.Delay(100);

        // Enqueue an item
        queue.Enqueue(dto);

        // Wait for dequeue to complete
        await dequeueTask;

        // Assert
        result.Should().NotBeNull();
        result!.ActorId.Should().Be(dto.ActorId);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task Queue_HandlesMultipleWritersSimultaneously()
    {
        // Arrange
        var queue = new AuditLogQueue(_mockLogger.Object);
        const int itemsPerThread = 100;
        const int threadCount = 5;

        // Act - Multiple threads enqueuing simultaneously
        var enqueueTasks = Enumerable.Range(0, threadCount)
            .Select(threadIndex => Task.Run(() =>
            {
                for (int i = 0; i < itemsPerThread; i++)
                {
                    queue.Enqueue(CreateTestDto(actorId: $"thread{threadIndex}-item{i}"));
                }
            }))
            .ToArray();

        await Task.WhenAll(enqueueTasks);

        // Assert
        queue.Count.Should().Be(itemsPerThread * threadCount, "all items from all threads should be enqueued");
    }

    [Fact]
    public async Task Queue_HandlesConcurrentReadsAndWrites()
    {
        // Arrange
        var queue = new AuditLogQueue(_mockLogger.Object);
        const int itemCount = 50;
        var dequeuedItems = new List<AuditLogCreateDto>();
        var dequeueLock = new object();

        // Act - Start dequeuing in background
        var dequeueTask = Task.Run(async () =>
        {
            for (int i = 0; i < itemCount; i++)
            {
                var item = await queue.DequeueAsync();
                lock (dequeueLock)
                {
                    dequeuedItems.Add(item);
                }
            }
        });

        // Enqueue items while dequeue is running
        for (int i = 0; i < itemCount; i++)
        {
            queue.Enqueue(CreateTestDto(actorId: $"item{i}"));
            await Task.Delay(5); // Small delay to interleave operations
        }

        await dequeueTask;

        // Assert
        dequeuedItems.Should().HaveCount(itemCount, "all items should be dequeued");
        queue.Count.Should().Be(0, "queue should be empty after all items are dequeued");
    }

    #endregion

    #region Bounded Capacity Tests

    [Fact]
    public void Enqueue_WithinCapacity_Succeeds()
    {
        // Arrange
        var queue = new AuditLogQueue(_mockLogger.Object);
        const int itemsToEnqueue = 1000; // Well below the 10,000 capacity

        // Act
        for (int i = 0; i < itemsToEnqueue; i++)
        {
            queue.Enqueue(CreateTestDto());
        }

        // Assert
        queue.Count.Should().Be(itemsToEnqueue, "all items should be successfully enqueued");
    }

    #endregion

    #region Helper Methods

    private static AuditLogCreateDto CreateTestDto(
        AuditLogCategory category = AuditLogCategory.Guild,
        AuditLogAction action = AuditLogAction.Updated,
        AuditLogActorType actorType = AuditLogActorType.User,
        string actorId = "testuser")
    {
        return new AuditLogCreateDto
        {
            Category = category,
            Action = action,
            ActorType = actorType,
            ActorId = actorId,
            TargetType = "Guild",
            TargetId = "123456789",
            GuildId = 123456789UL,
            Details = "{\"test\":\"data\"}",
            IpAddress = "127.0.0.1",
            CorrelationId = null
        };
    }

    #endregion
}
