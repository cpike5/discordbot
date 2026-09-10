using System.Runtime.CompilerServices;

namespace DiscordBot.Tests.TestHelpers;

/// <summary>
/// Raises the thread pool's minimum worker count for the test process.
/// <para>
/// By default the pool starts with one worker per core (4 on a GitHub-hosted runner) and grows by
/// roughly one thread per half second once those are all busy. With ~3,800 tests running in
/// parallel, a handful of tests that block a pool thread (a sleep, a barrier, a synchronous wait)
/// are enough to leave queued work - such as the <c>Task.Run</c> that starts every
/// <c>MonitoredBackgroundService</c> loop - waiting for seconds, which is what made the
/// background-service tests time out on CI. The known blockers now run on dedicated threads
/// (<see cref="ConcurrencyTestHelper"/>); this floor is the backstop so a future one cannot
/// starve the rest of the suite again. Nothing in the suite asserts on thread-pool behaviour.
/// </para>
/// </summary>
internal static class ThreadPoolTestSetup
{
    private const int MinimumWorkerThreads = 32;

    [ModuleInitializer]
    internal static void RaiseMinimumWorkerThreads()
    {
        ThreadPool.GetMinThreads(out var workerThreads, out var completionPortThreads);
        if (workerThreads < MinimumWorkerThreads)
        {
            ThreadPool.SetMinThreads(MinimumWorkerThreads, completionPortThreads);
        }
    }
}
