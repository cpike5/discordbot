using DiscordBot.Bot.Blazor.Common;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;

namespace DiscordBot.Tests.Blazor.Common;

/// <summary>
/// Unit tests for <see cref="Debouncer"/>: only the last of a burst of calls runs, and the
/// earlier in-flight wait/action is cancelled. Polls off the xUnit context
/// (<see cref="LogTestHelper.WaitUntilAsync"/>) per
/// docs/lessons-learned/flaky-tests-thread-pool-starvation.md rather than sleeping a fixed delay.
/// </summary>
public class DebouncerTests : IDisposable
{
    private readonly Debouncer _debouncer = new();

    public void Dispose() => _debouncer.Dispose();

    [Fact]
    public async Task Debounce_SingleCall_RunsTheAction()
    {
        // Arrange
        var ran = false;

        // Act
        _debouncer.Debounce(TimeSpan.FromMilliseconds(10), _ => { ran = true; return Task.CompletedTask; });

        // Assert
        var completed = await LogTestHelper.WaitUntilAsync(() => ran, TimeSpan.FromSeconds(5));
        completed.Should().BeTrue();
    }

    [Fact]
    public async Task Debounce_BurstOfCalls_OnlyTheLastRuns()
    {
        // Arrange
        var runCount = 0;
        var lastValueRun = -1;

        // Act - a rapid burst, each superseding the last before its delay elapses
        for (var i = 0; i < 5; i++)
        {
            var value = i;
            _debouncer.Debounce(TimeSpan.FromMilliseconds(50), _ =>
            {
                Interlocked.Increment(ref runCount);
                lastValueRun = value;
                return Task.CompletedTask;
            });
        }

        // Assert
        var completed = await LogTestHelper.WaitUntilAsync(() => runCount > 0, TimeSpan.FromSeconds(5));
        completed.Should().BeTrue();

        // Give any (incorrectly) superseded call a chance to also fire before asserting the count.
        await Task.Delay(100);

        runCount.Should().Be(1, "only the last debounced call should run");
        lastValueRun.Should().Be(4);
    }

    [Fact]
    public async Task Debounce_SupersedingCallBeforeDelayElapses_CancelsTheEarlierWait()
    {
        // Arrange
        var earlyRan = false;
        var lateRan = false;

        // Act
        _debouncer.Debounce(TimeSpan.FromSeconds(30), _ => { earlyRan = true; return Task.CompletedTask; });
        _debouncer.Debounce(TimeSpan.FromMilliseconds(10), _ => { lateRan = true; return Task.CompletedTask; });

        // Assert
        var completed = await LogTestHelper.WaitUntilAsync(() => lateRan, TimeSpan.FromSeconds(5));
        completed.Should().BeTrue();
        earlyRan.Should().BeFalse("the first call's 30s wait should have been cancelled by the second call");
    }

    [Fact]
    public async Task Debounce_ActionAlreadyRunning_IsCancelledByASupersedingCall()
    {
        // Arrange
        using var startedGate = new SemaphoreSlim(0, 1);
        var firstActionCancelled = false;

        // Act - the first action starts running and awaits cancellation instead of completing
        _debouncer.Debounce(TimeSpan.FromMilliseconds(5), async ct =>
        {
            startedGate.Release();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                firstActionCancelled = true;
                throw;
            }
        });

        var started = await startedGate.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        started.Should().BeTrue("the first action should have started running");

        var secondRan = false;
        _debouncer.Debounce(TimeSpan.FromMilliseconds(5), _ => { secondRan = true; return Task.CompletedTask; });

        // Assert
        var completed = await LogTestHelper.WaitUntilAsync(() => secondRan, TimeSpan.FromSeconds(5));
        completed.Should().BeTrue();

        var cancelled = await LogTestHelper.WaitUntilAsync(() => firstActionCancelled, TimeSpan.FromSeconds(5));
        cancelled.Should().BeTrue("the in-flight first action should be cancelled once superseded");
    }

    [Fact]
    public void Debounce_AfterDispose_IsANoOp()
    {
        // Arrange
        _debouncer.Dispose();
        var ran = false;

        // Act
        var act = () => _debouncer.Debounce(TimeSpan.FromMilliseconds(1), _ => { ran = true; return Task.CompletedTask; });

        // Assert
        act.Should().NotThrow();
    }
}
