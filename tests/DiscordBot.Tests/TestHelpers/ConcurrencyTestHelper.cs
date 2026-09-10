namespace DiscordBot.Tests.TestHelpers;

/// <summary>
/// Runs concurrency tests on dedicated threads instead of the shared .NET thread pool.
/// <para>
/// Tests that exercise thread safety used to do <c>Parallel.For</c> with a <see cref="Barrier"/>
/// (or <c>Task.Run</c> bodies that spin on <c>Thread.Sleep</c>). Those bodies block pool threads,
/// and a barrier with N participants cannot release until the pool has grown to N threads. On a
/// 4-core CI runner the pool starts at 4 workers and injects roughly one more per half second, so a
/// 20-participant barrier pinned every pool thread for ~8-10 seconds. During that window nothing
/// else could be scheduled on the pool - including the <c>Task.Run</c> that
/// <c>MonitoredBackgroundService</c> uses to start its loop - so unrelated background-service tests
/// running in parallel timed out waiting for a service that had never started.
/// </para>
/// <para>
/// Dedicated threads keep the same "N threads hit the collection at the same instant" semantics
/// without touching the pool, so these tests neither depend on nor starve it.
/// </para>
/// </summary>
internal static class ConcurrencyTestHelper
{
    /// <summary>
    /// Runs <paramref name="body"/> on <paramref name="threadCount"/> dedicated threads. All threads
    /// are released together once every one of them has started, so the bodies genuinely overlap.
    /// Blocks until every thread has finished, then rethrows any exception a body threw.
    /// </summary>
    /// <param name="threadCount">Number of threads to start.</param>
    /// <param name="body">Work to run on each thread; receives the thread's index (0-based).</param>
    public static void RunOnDedicatedThreads(int threadCount, Action<int> body)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(threadCount);
        ArgumentNullException.ThrowIfNull(body);

        using var startGate = new Barrier(threadCount);
        RunOnDedicatedThreads(Enumerable.Range(0, threadCount).Select<int, Action>(index => () =>
        {
            startGate.SignalAndWait();
            body(index);
        }).ToArray());
    }

    /// <summary>
    /// Runs each of <paramref name="bodies"/> on its own dedicated thread, blocks until all of them
    /// have finished, then rethrows any exception a body threw.
    /// </summary>
    public static void RunOnDedicatedThreads(params Action[] bodies)
    {
        ArgumentNullException.ThrowIfNull(bodies);

        var failures = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var threads = bodies.Select((body, index) => new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception ex)
            {
                failures.Enqueue(ex);
            }
        })
        {
            IsBackground = true,
            Name = $"{nameof(ConcurrencyTestHelper)}-{index}"
        }).ToList();

        foreach (var thread in threads)
        {
            thread.Start();
        }

        foreach (var thread in threads)
        {
            thread.Join();
        }

        if (!failures.IsEmpty)
        {
            throw new AggregateException("One or more test threads threw.", failures);
        }
    }
}
