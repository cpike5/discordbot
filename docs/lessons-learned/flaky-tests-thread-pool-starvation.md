# Lessons Learned: Flaky background-service tests under a full parallel run

**Date:** 2026-09-10
**Related:** [#636 - Fix flaky MetricsCollectionService error handling tests](https://github.com/cpike5/discordbot/issues/636),
`docs/lessons-learned/issue-615-metrics-collection-service-tests.md`

---

## Summary

CI on `main` failed intermittently with one background-service test red per run, always one
that passes in isolation:

- `CpuSamplingServiceTests.ExecuteMonitoredAsync_RecordsCpuValueInValidRange` (30-40 s, then
  "at least one sample should be recorded")
- `CpuSamplingServiceTests.ExecuteMonitoredAsync_UpdatesHeartbeatOnSuccess`
- `MessageLogCleanupServiceTests.ExecuteAsync_WhenDisabled_ExitsImmediately`,
  `PerformCleanup_LogsCleanupStart`, `ExecuteAsync_LogsRetentionConfiguration`,
  `PerformCleanup_HandlesExceptionGracefully` (logger mock: "No invocations performed")

The services under test already ran on `FakeTimeProvider` and the tests already polled for a
condition instead of sleeping, so the usual "real delay too short" explanation did not apply.
Instrumenting the CPU test inside a full run showed two different things going wrong, and the
fix has two parts.

## Mechanism 1: the thread pool was starved, so the service never started

`MonitoredBackgroundService.ExecuteAsync` starts the service loop with `Task.Run`. The
thread-safety tests for `LruConcurrentDictionary` and `BoundedTimestampQueue` did this:

```csharp
var barrier = new Barrier(threadCount);      // 10 or 20 participants
Parallel.For(0, threadCount, i =>
{
    barrier.SignalAndWait();                 // blocks a pool thread until all N are here
    ...
});
```

`Parallel.For` runs on pool threads, and the barrier cannot release until all N bodies are
running at once. On a 4-core runner the pool starts with 4 workers and injects roughly one more
per half second, so the 20-participant barrier pinned **every** pool thread for 8-10 seconds
(the CI log shows that test at `[10 s]` and the 10-participant one at `[6 s]`). Any `Task.Run`
queued in that window waited it out, which is why the `MessageLogCleanupService` tests saw a
logger with no invocations at all: the service loop had not started.

`Task.Run` bodies that loop on `Thread.Sleep(1)` for 500 ms, and
`Parallel.For(0, 500, i => { ...; Thread.Sleep(1); })`, are the same problem in miniature.

**Fix:** `TestHelpers/ConcurrencyTestHelper.cs` runs those bodies on dedicated `Thread`s
(released together by an internal barrier) and joins them, so the tests keep their "N threads
hit the collection at once" semantics without touching the pool. `TestHelpers/ThreadPoolTestSetup.cs`
is a `[ModuleInitializer]` that raises the pool's minimum worker count to 32 for the test
process, as a backstop so one new blocking test cannot starve the suite again.

## Mechanism 2: the test's own polling loop was starved by xUnit's scheduler

With mechanism 1 fixed, the CPU test still took 7-26 s in a full run. Instrumentation showed
the service logging "CPU sampling started" within 15 ms and recording its sample as soon as the
fake clock was advanced, while the test's `AdvanceUntilAsync` loop got only **four** turns in
26 seconds.

xUnit v2 runs test code on a small pool of worker threads (`maxParallelThreads`, one per core)
behind a synchronization context. Every test collection is queued on it up front and multiplexed
over those workers, and every `await` inside a test method resumes by posting to the back of
that same FIFO queue. A continuation posted early in the run therefore waits behind every
collection that has not started yet, each of which holds a worker for its whole synchronous
segment. The SQLite-backed repository test classes are the killer: `Microsoft.Data.Sqlite`
completes its "async" calls synchronously, so a whole repository class (dozens of tests, each
creating a database) runs as one multi-second segment without ever yielding. Measured locally
on 4 cores, a single `await Task.Delay(20)` in a test took anywhere from 5 s to 27 s to resume.

`AdvanceUntilAsync` and `LogTestHelper.WaitUntilAsync`/`WaitForLogAsync` all measured their
deadline in wall-clock time across those resumptions. A 30 s ceiling that only affords three or
four polls is a coin flip; the same latency is why unrelated tiny async tests in the same CI
log report durations of 10-30 s.

**Fix:** the polling helpers await with `ConfigureAwait(false)`, so their loops resume on the
thread pool (healthy after part 1) instead of the xUnit context. The cadence of the poll, and
hence the deadline, no longer depends on what the rest of the suite is doing. The calling test
still resumes through xUnit once when the helper returns, which only inflates its reported
duration.

## How it was diagnosed

1. The failing test ran for 30-40 s: the polling ceiling plus the time `StopAsync` took.
2. Trivially fast tests around it in the same CI log reported ~1 s durations.
3. A stopwatch log written from inside the test in a full local run separated "service did
   not start" (part 1) from "service started in 11 ms, loop checked 4 times in 26 s" (part 2).

Reading the durations of the *passing* tests was the shortcut: the 10 s and 6 s barrier tests
were in every failing log.

## Key lessons

1. **A test that waits for a `Task.Run` is only as reliable as the thread pool.** Never block a
   pool thread on other pool threads (`Barrier`, `CountdownEvent`, `Task.Wait` inside
   `Parallel.For` or `Task.Run`). Use dedicated threads for that shape of test.
2. **A wall-clock deadline across plain `await`s in an xUnit test is not a deadline.** Anything
   that polls with a timeout must resume off the xUnit context (`ConfigureAwait(false)`) or it
   will be starved by whichever test classes happen to be running.
3. **Instrument before theorising.** Both mechanisms produce "background service test timed out
   in a full run, passes alone"; only a timestamp log from inside the test told them apart.
4. **The skipped `MetricsCollectionServiceTests` in #636** describe the same symptom and were
   very likely the same two mechanisms. They still use fixed real delays, so they were left
   skipped; un-skipping them is a separate change.
