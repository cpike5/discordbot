using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.TestHelpers;

/// <summary>
/// Helper for asserting on <see cref="Mock{ILogger}"/> invocations without racing a background
/// service's own scheduling. Background services (see <c>MonitoredBackgroundService</c>) run their
/// loop via <c>Task.Run</c> on the thread pool, so a fixed <c>Task.Delay(N)</c> followed immediately
/// by a log assertion is inherently flaky under CPU load: the thread pool may not schedule the
/// service's continuation before the fixed delay elapses. Polling for the expected invocation (with
/// a generous upper bound purely to fail fast on a genuine bug, never to assert timing) removes that
/// race while still failing promptly when the log truly never happens.
/// </summary>
internal static class LogTestHelper
{
    /// <summary>
    /// Polls until a matching log invocation is observed on <paramref name="loggerMock"/> or the
    /// timeout elapses. Does not assert - callers should still assert afterwards (e.g. via
    /// <c>Verify</c>) so failure messages remain descriptive.
    /// </summary>
    public static async Task<bool> WaitForLogAsync<T>(
        Mock<ILogger<T>> loggerMock,
        LogLevel level,
        Func<string, bool> messagePredicate,
        TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));

        while (true)
        {
            // Mock.Invocations is a live collection the mocked service can still be appending to
            // on another thread; enumerating it here can race that append and throw
            // InvalidOperationException ("Collection was modified"). Treat that as "not found yet"
            // and retry on the next poll rather than failing the whole wait.
            bool found;
            try
            {
                found = loggerMock.Invocations.Any(inv =>
                    inv.Method.Name == nameof(ILogger.Log) &&
                    inv.Arguments.Count >= 3 &&
                    inv.Arguments[0] is LogLevel invokedLevel &&
                    invokedLevel == level &&
                    messagePredicate(inv.Arguments[2]?.ToString() ?? string.Empty));
            }
            catch (InvalidOperationException)
            {
                found = false;
            }

            if (found)
            {
                return true;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(10);
        }
    }

    /// <summary>
    /// Polls until <paramref name="condition"/> returns true or the timeout elapses.
    /// Use for non-log conditions (mock call counts, captured state) that race a background
    /// service's own scheduling in the same way log assertions do.
    /// </summary>
    public static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));

        while (true)
        {
            // condition() commonly enumerates a Mock's Invocations (call counts, captured args) that
            // the mocked service may still be appending to concurrently; guard the same way as
            // WaitForLogAsync so a transient InvalidOperationException just delays the next poll
            // instead of failing the wait outright.
            bool result;
            try
            {
                result = condition();
            }
            catch (InvalidOperationException)
            {
                result = false;
            }

            if (result)
            {
                return true;
            }

            if (DateTime.UtcNow >= deadline)
            {
                return false;
            }

            await Task.Delay(10);
        }
    }
}
