using DiscordBot.Bot.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Services;

/// <summary>
/// Unit tests for <see cref="BackgroundTaskRunner"/>.
/// Verifies work execution, error logging, and state passing.
/// </summary>
public class BackgroundTaskRunnerTests
{
    private readonly Mock<ILogger<BackgroundTaskRunner>> _mockLogger;
    private readonly BackgroundTaskRunner _runner;

    public BackgroundTaskRunnerTests()
    {
        _mockLogger = new Mock<ILogger<BackgroundTaskRunner>>();
        _runner = new BackgroundTaskRunner(_mockLogger.Object);
    }

    [Fact]
    public async Task Run_ExecutesWork()
    {
        // Arrange
        var tcs = new TaskCompletionSource<bool>();

        // Act
        _runner.Run(_ =>
        {
            tcs.SetResult(true);
            return Task.CompletedTask;
        }, "TestOperation");

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert
        completed.Should().Be(tcs.Task, "the work should have completed within the timeout");
        (await tcs.Task).Should().BeTrue();
    }

    [Fact]
    public async Task Run_LogsErrorOnException()
    {
        // Arrange
        var tcs = new TaskCompletionSource<bool>();
        var expectedException = new InvalidOperationException("Test failure");

        // Act
        _runner.Run(_ =>
        {
            tcs.SetResult(true);
            throw expectedException;
        }, "FailingOperation");

        // Wait for the task to complete (including exception handling)
        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        // Give the logging a moment to happen after the exception
        await Task.Delay(100);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("FailingOperation")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_DoesNotPropagateExceptions()
    {
        // Arrange
        var tcs = new TaskCompletionSource<bool>();

        // Act - this should not throw
        _runner.Run(_ =>
        {
            tcs.SetResult(true);
            throw new InvalidOperationException("Should not propagate");
        }, "NonPropagating");

        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert - if we got here without an exception, the test passes
        (await tcs.Task).Should().BeTrue();
    }

    [Fact]
    public async Task Run_WithCancelledException_LogsDebugInsteadOfError()
    {
        // Arrange
        var tcs = new TaskCompletionSource<bool>();

        // Act
        _runner.Run(_ =>
        {
            tcs.SetResult(true);
            throw new OperationCanceledException("Cancelled");
        }, "CancelledOperation");

        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await Task.Delay(100);

        // Assert - should log Debug, not Error
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("CancelledOperation")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    [Fact]
    public async Task RunWithState_PassesStateCorrectly()
    {
        // Arrange
        var tcs = new TaskCompletionSource<int>();
        var expectedState = 42;

        // Act
        _runner.Run<int>((state, _) =>
        {
            tcs.SetResult(state);
            return Task.CompletedTask;
        }, expectedState, "StatefulOperation");

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert
        completed.Should().Be(tcs.Task, "the work should have completed within the timeout");
        (await tcs.Task).Should().Be(expectedState);
    }

    [Fact]
    public async Task RunWithState_LogsErrorOnException()
    {
        // Arrange
        var tcs = new TaskCompletionSource<bool>();
        var expectedException = new InvalidOperationException("Stateful failure");

        // Act
        _runner.Run<string>((state, _) =>
        {
            tcs.SetResult(true);
            throw expectedException;
        }, "test-state", "StatefulFailingOperation");

        await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        await Task.Delay(100);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("StatefulFailingOperation")),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RunWithState_ComplexState_PassedCorrectly()
    {
        // Arrange
        var tcs = new TaskCompletionSource<(ulong GuildId, string Name)>();
        var expectedState = (GuildId: 123456789UL, Name: "TestGuild");

        // Act
        _runner.Run<(ulong GuildId, string Name)>((state, _) =>
        {
            tcs.SetResult(state);
            return Task.CompletedTask;
        }, expectedState, "ComplexStateOperation");

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert
        completed.Should().Be(tcs.Task);
        var result = await tcs.Task;
        result.GuildId.Should().Be(123456789UL);
        result.Name.Should().Be("TestGuild");
    }
}
