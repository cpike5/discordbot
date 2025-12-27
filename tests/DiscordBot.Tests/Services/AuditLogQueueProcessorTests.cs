using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AuditLogQueueProcessor"/>.
/// Tests verify that the background service correctly processes audit log entries from the queue,
/// batches them efficiently, and handles shutdown gracefully.
/// </summary>
[Trait("Category", "Unit")]
public class AuditLogQueueProcessorTests
{
    private readonly Mock<IServiceScopeFactory> _scopeFactoryMock;
    private readonly Mock<IServiceScope> _serviceScopeMock;
    private readonly Mock<IServiceProvider> _serviceProviderMock;
    private readonly Mock<IAuditLogQueue> _queueMock;
    private readonly Mock<IAuditLogRepository> _repositoryMock;
    private readonly Mock<ILogger<AuditLogQueueProcessor>> _loggerMock;

    public AuditLogQueueProcessorTests()
    {
        _scopeFactoryMock = new Mock<IServiceScopeFactory>();
        _serviceScopeMock = new Mock<IServiceScope>();
        _serviceProviderMock = new Mock<IServiceProvider>();
        _queueMock = new Mock<IAuditLogQueue>();
        _repositoryMock = new Mock<IAuditLogRepository>();
        _loggerMock = new Mock<ILogger<AuditLogQueueProcessor>>();

        // Setup the service scope chain
        _scopeFactoryMock.Setup(x => x.CreateScope()).Returns(_serviceScopeMock.Object);
        _serviceScopeMock.Setup(x => x.ServiceProvider).Returns(_serviceProviderMock.Object);
        _serviceProviderMock.Setup(x => x.GetService(typeof(IAuditLogRepository)))
            .Returns(_repositoryMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        // Arrange & Act
        var act = () => new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_StartsWithoutThrowing()
    {
        // Arrange
        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Assert - Should not throw
        await executeTask;
        executeTask.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_LogsStartupMessage()
    {
        // Arrange
        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("starting") &&
                    v.ToString()!.Contains("Batch size: 10") &&
                    v.ToString()!.Contains("Timeout: 1000ms")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log startup message with batch configuration");
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesItemsFromQueue()
    {
        // Arrange
        var dto = CreateTestDto();
        var dequeueCalled = false;
        var callCount = 0;

        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                callCount++;
                if (callCount == 1)
                {
                    dequeueCalled = true;
                    return dto;
                }

                // Wait to simulate timeout condition
                await Task.Delay(2000, token);
                throw new OperationCanceledException();
            });

        _repositoryMock.Setup(x => x.BulkInsertAsync(It.IsAny<IEnumerable<AuditLog>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(1500); // Give it time to process
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        dequeueCalled.Should().BeTrue("should dequeue items from the queue");
        _repositoryMock.Verify(
            x => x.BulkInsertAsync(It.IsAny<IEnumerable<AuditLog>>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "should insert items into repository");
    }

    [Fact]
    public async Task ExecuteAsync_RespectsCancellationToken()
    {
        // Arrange
        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        cts.Cancel(); // Cancel immediately
        await service.StopAsync(CancellationToken.None);

        // Should complete without hanging
        var completedInTime = await Task.WhenAny(executeTask, Task.Delay(1000)) == executeTask;

        // Assert
        completedInTime.Should().BeTrue("service should stop promptly when cancellation is requested");
    }

    [Fact]
    public async Task ExecuteAsync_HandlesOperationCanceledException_Gracefully()
    {
        // Arrange
        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(token);
                }
                // Wait indefinitely - will be cancelled
                await Task.Delay(Timeout.Infinite, token);
                throw new OperationCanceledException(token);
            });

        _queueMock.Setup(x => x.Count).Returns(0);

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("shutting down") &&
                    v.ToString()!.Contains("remaining items")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log shutdown message when OperationCanceledException occurs");
    }

    [Fact]
    public async Task ExecuteAsync_LogsStoppedMessage()
    {
        // Arrange
        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(token);
                }
                // Wait indefinitely - will be cancelled
                await Task.Delay(Timeout.Infinite, token);
                throw new OperationCanceledException(token);
            });

        _queueMock.Setup(x => x.Count).Returns(0);

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("stopped")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log stopped message when service exits");
    }

    #endregion

    #region Batch Processing Tests

    [Fact]
    public async Task ProcessBatch_CollectsUpTo10ItemsPerBatch()
    {
        // Arrange
        var dtos = Enumerable.Range(0, 15).Select(_ => CreateTestDto()).ToList();
        var dequeueIndex = 0;
        List<AuditLog>? capturedBatch = null;

        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                if (dequeueIndex >= dtos.Count)
                    throw new OperationCanceledException();
                return dtos[dequeueIndex++];
            });

        _repositoryMock.Setup(x => x.BulkInsertAsync(It.IsAny<IEnumerable<AuditLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<AuditLog>, CancellationToken>((batch, token) => capturedBatch = batch.ToList())
            .Returns(Task.CompletedTask);

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(200); // Give it time to process first batch
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        capturedBatch.Should().NotBeNull();
        capturedBatch!.Count.Should().BeLessThanOrEqualTo(10, "batch size should be at most 10 items");
    }

    [Fact]
    public async Task ProcessBatch_ProcessesAvailableItems_WhenBatchFills()
    {
        // Arrange
        var dtos = Enumerable.Range(0, 10).Select(_ => CreateTestDto()).ToList();
        var dequeueIndex = 0;

        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                if (dequeueIndex >= dtos.Count)
                {
                    await Task.Delay(2000, token);
                    throw new OperationCanceledException();
                }
                return dtos[dequeueIndex++];
            });

        _repositoryMock.Setup(x => x.BulkInsertAsync(It.IsAny<IEnumerable<AuditLog>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(500); // 10 items will fill batch immediately
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        _repositoryMock.Verify(
            x => x.BulkInsertAsync(It.Is<IEnumerable<AuditLog>>(list => list.Count() == 10), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "should process a full batch of 10 items");
    }

    [Fact]
    public async Task ProcessBatch_HandlesErrors_WithoutCrashing()
    {
        // Arrange
        var dto = CreateTestDto();

        _queueMock.SetupSequence(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto)
            .ReturnsAsync(dto)
            .ThrowsAsync(new OperationCanceledException());

        _repositoryMock.SetupSequence(x => x.BulkInsertAsync(It.IsAny<IEnumerable<AuditLog>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"))
            .Returns(Task.CompletedTask);

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(300); // Give it time to process and recover
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Should not throw - service handles errors gracefully
        await executeTask;
        executeTask.IsCompleted.Should().BeTrue();

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Error processing audit log batch")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log error when batch processing fails");
    }

    #endregion

    #region Shutdown Processing Tests

    [Fact]
    public async Task Shutdown_ProcessesRemainingItems()
    {
        // Arrange
        var dto = CreateTestDto();
        _queueMock.SetupSequence(x => x.Count)
            .Returns(5)
            .Returns(4)
            .Returns(3)
            .Returns(2)
            .Returns(1)
            .Returns(0);

        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                // Throw when the token is cancelled (simulating shutdown)
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(token);
                }
                // Otherwise wait indefinitely - will be cancelled
                await Task.Delay(Timeout.Infinite, token);
                throw new OperationCanceledException(token);
            });

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100); // Let it start
        cts.Cancel(); // Trigger shutdown
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Processing") &&
                    v.ToString()!.Contains("remaining")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should attempt to process remaining items during shutdown");
    }

    [Fact]
    public async Task Shutdown_LogsNoRemainingItems_WhenQueueEmpty()
    {
        // Arrange
        _queueMock.Setup(x => x.Count).Returns(0);
        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                if (token.IsCancellationRequested)
                {
                    throw new OperationCanceledException(token);
                }
                // Wait indefinitely - will be cancelled
                await Task.Delay(Timeout.Infinite, token);
                throw new OperationCanceledException(token);
            });

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("No remaining") &&
                    v.ToString()!.Contains("audit log entries")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log that no items remain when queue is empty during shutdown");
    }

    [Fact]
    public async Task Shutdown_LogsFinishedProcessing_WhenItemsProcessed()
    {
        // Arrange
        var dto = CreateTestDto();
        var callCount = 0;

        // Queue has items at shutdown
        _queueMock.SetupSequence(x => x.Count)
            .Returns(1)  // First check - has items
            .Returns(0); // After processing - empty

        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // Main loop - wait to be cancelled
                    await Task.Delay(Timeout.Infinite, token);
                    throw new OperationCanceledException(token);
                }
                // During shutdown - return the item
                return dto;
            });

        _repositoryMock.Setup(x => x.BulkInsertAsync(It.IsAny<IEnumerable<AuditLog>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(100);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Finished processing remaining")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log completion of remaining items processing");
    }

    #endregion

    #region WriteBatch Tests

    [Fact]
    public async Task WriteBatch_CreatesScopedServiceCorrectly()
    {
        // Arrange
        var dto = CreateTestDto();
        var callCount = 0;

        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return dto;
                }

                await Task.Delay(2000, token);
                throw new OperationCanceledException();
            });

        _repositoryMock.Setup(x => x.BulkInsertAsync(It.IsAny<IEnumerable<AuditLog>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(1500);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        _scopeFactoryMock.Verify(x => x.CreateScope(), Times.AtLeastOnce,
            "should create a scope for database operations");
        _serviceScopeMock.Verify(x => x.Dispose(), Times.AtLeastOnce,
            "should dispose scope after operations complete");
    }

    [Fact]
    public async Task WriteBatch_ConvertsDtosToEntitiesProperly()
    {
        // Arrange
        var dto = CreateTestDto(
            category: AuditLogCategory.User,
            action: AuditLogAction.Created,
            actorId: "actor123",
            targetId: "target456",
            guildId: 987654321UL);

        List<AuditLog>? capturedEntities = null;
        var callCount = 0;

        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return dto;
                }

                await Task.Delay(2000, token);
                throw new OperationCanceledException();
            });

        _repositoryMock.Setup(x => x.BulkInsertAsync(It.IsAny<IEnumerable<AuditLog>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<AuditLog>, CancellationToken>((entities, token) => capturedEntities = entities.ToList())
            .Returns(Task.CompletedTask);

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(1500);
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        capturedEntities.Should().NotBeNull();
        capturedEntities.Should().HaveCount(1);
        var entity = capturedEntities![0];

        entity.Category.Should().Be(AuditLogCategory.User);
        entity.Action.Should().Be(AuditLogAction.Created);
        entity.ActorId.Should().Be("actor123");
        entity.ActorType.Should().Be(dto.ActorType);
        entity.TargetType.Should().Be(dto.TargetType);
        entity.TargetId.Should().Be("target456");
        entity.GuildId.Should().Be(987654321UL);
        entity.Details.Should().Be(dto.Details);
        entity.IpAddress.Should().Be(dto.IpAddress);
        entity.CorrelationId.Should().Be(dto.CorrelationId);
        entity.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5),
            "timestamp should be set to current UTC time");
    }

    [Fact]
    public async Task WriteBatch_CallsBulkInsertAsync_OnRepository()
    {
        // Arrange
        var dtos = Enumerable.Range(0, 5).Select(_ => CreateTestDto()).ToList();
        var dequeueIndex = 0;

        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                if (dequeueIndex >= dtos.Count)
                {
                    // Wait to allow batch timeout
                    await Task.Delay(2000, token);
                    throw new OperationCanceledException();
                }
                return dtos[dequeueIndex++];
            });

        _repositoryMock.Setup(x => x.BulkInsertAsync(It.IsAny<IEnumerable<AuditLog>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(1500); // Wait for batch timeout and processing
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        _repositoryMock.Verify(
            x => x.BulkInsertAsync(
                It.Is<IEnumerable<AuditLog>>(list => list.Count() == 5),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "should call BulkInsertAsync with the converted entities");
    }

    [Fact]
    public async Task WriteBatch_LogsSuccess()
    {
        // Arrange
        var dtos = Enumerable.Range(0, 10).Select(_ => CreateTestDto()).ToList();
        var dequeueIndex = 0;

        // Provide 10 items to fill the batch immediately (batch size is 10)
        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                if (dequeueIndex >= dtos.Count)
                {
                    // After batch is full, wait to trigger cancellation
                    await Task.Delay(5000, token);
                    throw new OperationCanceledException();
                }
                return dtos[dequeueIndex++];
            });

        _repositoryMock.Setup(x => x.BulkInsertAsync(It.IsAny<IEnumerable<AuditLog>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(500); // Full batch (10 items) should process immediately
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Successfully wrote batch")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log success message after writing batch");
    }

    [Fact]
    public async Task WriteBatch_LogsFailure_WhenBulkInsertFails()
    {
        // Arrange
        var dto = CreateTestDto(category: AuditLogCategory.Message, action: AuditLogAction.Deleted);
        var callCount = 0;

        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return dto;
                }

                // Wait to simulate timeout condition
                await Task.Delay(2000, token);
                throw new OperationCanceledException();
            });

        _repositoryMock.Setup(x => x.BulkInsertAsync(It.IsAny<IEnumerable<AuditLog>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database connection failed"));

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(1500); // Give it time to process and handle error
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Failed to write batch") &&
                    v.ToString()!.Contains("Message.Deleted")),
                It.Is<Exception>(ex => ex.Message.Contains("Database connection failed")),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log error with batch details when BulkInsertAsync fails");
    }

    [Fact]
    public async Task WriteBatch_DoesNotRethrowException()
    {
        // Arrange
        var dto = CreateTestDto();
        var callCount = 0;

        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                callCount++;
                if (callCount <= 2)
                {
                    return dto;
                }
                // Wait to simulate timeout condition
                await Task.Delay(2000, token);
                throw new OperationCanceledException();
            });

        _repositoryMock.SetupSequence(x => x.BulkInsertAsync(It.IsAny<IEnumerable<AuditLog>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"))
            .Returns(Task.CompletedTask);

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(7000); // Give it time to process, fail, wait 5 seconds, and recover
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);

        // Should not throw - service handles exceptions gracefully
        await executeTask;

        // Assert
        executeTask.IsCompleted.Should().BeTrue("service should handle database errors gracefully");
        _repositoryMock.Verify(
            x => x.BulkInsertAsync(It.IsAny<IEnumerable<AuditLog>>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce,
            "should attempt to process items even after database error");
    }

    [Fact]
    public async Task WriteBatch_LogsDebugMessage_BeforeWriting()
    {
        // Arrange
        var dtos = Enumerable.Range(0, 3).Select(_ => CreateTestDto()).ToList();
        var dequeueIndex = 0;

        _queueMock.Setup(x => x.DequeueAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async token =>
            {
                if (dequeueIndex >= dtos.Count)
                {
                    // Wait to simulate timeout condition
                    await Task.Delay(2000, token);
                    throw new OperationCanceledException();
                }
                return dtos[dequeueIndex++];
            });

        _repositoryMock.Setup(x => x.BulkInsertAsync(It.IsAny<IEnumerable<AuditLog>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new AuditLogQueueProcessor(
            _scopeFactoryMock.Object,
            _queueMock.Object,
            _loggerMock.Object);

        using var cts = new CancellationTokenSource();

        // Act
        var executeTask = service.StartAsync(cts.Token);
        await Task.Delay(1500); // Wait for batch timeout and processing
        cts.Cancel();
        await service.StopAsync(CancellationToken.None);
        await executeTask;

        // Assert
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Writing batch") &&
                    v.ToString()!.Contains("3")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "should log debug message before writing batch");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test audit log DTO with default or specified values.
    /// </summary>
    private static AuditLogCreateDto CreateTestDto(
        AuditLogCategory category = AuditLogCategory.Guild,
        AuditLogAction action = AuditLogAction.Updated,
        AuditLogActorType actorType = AuditLogActorType.User,
        string actorId = "testuser123",
        string targetId = "target456",
        ulong guildId = 123456789UL)
    {
        return new AuditLogCreateDto
        {
            Category = category,
            Action = action,
            ActorType = actorType,
            ActorId = actorId,
            TargetType = "Guild",
            TargetId = targetId,
            GuildId = guildId,
            Details = "{\"test\":\"data\"}",
            IpAddress = "127.0.0.1",
            CorrelationId = null
        };
    }

    #endregion
}
