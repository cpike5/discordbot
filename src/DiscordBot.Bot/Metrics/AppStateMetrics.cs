using System.Diagnostics;
using System.Diagnostics.Metrics;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Metrics;

/// <summary>
/// Observable metrics over in-process state that services already track for the portal's
/// performance pages: work queue depths, interaction state, voice connections, cache hit/miss
/// counts, the query performance interceptor's totals, CPU, and dashboard subscribers.
/// </summary>
/// <remarks>
/// Every value is read lazily on collection, and every dependency is resolved optionally, so a
/// service that is not registered in some host configuration simply produces no series.
/// </remarks>
public sealed class AppStateMetrics : IDisposable
{
    public const string MeterName = "DiscordBot.State";

    private readonly Meter _meter;
    private readonly IServiceProvider _services;

    public AppStateMetrics(IMeterFactory meterFactory, IServiceProvider services)
    {
        _services = services;
        _meter = meterFactory.Create(MeterName);

        _meter.CreateObservableGauge(
            name: "discordbot.queue.depth",
            observeValues: ObserveQueueDepths,
            unit: "{items}",
            description: "Items waiting in in-process work queues");

        _meter.CreateObservableGauge(
            name: "discordbot.interaction_state.active",
            observeValue: () => _services.GetService<IInteractionStateService>()?.ActiveStateCount ?? 0,
            unit: "{states}",
            description: "Component interaction states held in memory");

        _meter.CreateObservableGauge(
            name: "discordbot.voice.connections",
            observeValue: () => (_services.GetService<IAudioService>() as AudioService)?.GetActiveConnections().Count ?? 0,
            unit: "{connections}",
            description: "Voice channels the bot is connected to");

        _meter.CreateObservableCounter(
            name: "discordbot.cache.requests",
            observeValues: ObserveCacheRequests,
            unit: "{requests}",
            description: "Instrumented cache lookups, by key prefix and result (hit, miss)");

        _meter.CreateObservableGauge(
            name: "discordbot.cache.entries",
            observeValues: ObserveCacheEntries,
            unit: "{entries}",
            description: "Instrumented cache entries, by key prefix");

        _meter.CreateObservableCounter(
            name: "discordbot.db.queries",
            observeValue: () => _services.GetService<IDatabaseMetricsCollector>()?.GetMetrics().TotalQueries ?? 0,
            unit: "{queries}",
            description: "Database commands seen by the query performance interceptor");

        _meter.CreateObservableCounter(
            name: "discordbot.db.slow_queries",
            observeValue: () => (long)(_services.GetService<IDatabaseMetricsCollector>()?.GetMetrics().SlowQueryCount ?? 0),
            unit: "{queries}",
            description: "Database commands slower than Database:SlowQueryThresholdMs");

        _meter.CreateObservableGauge(
            name: "discordbot.process.cpu.usage",
            observeValue: () => _services.GetService<ICpuHistoryService>()?.GetCurrentCpu() ?? 0,
            unit: "%",
            description: "Process CPU usage as sampled by CpuSamplingService (0-100, all cores)");

        _meter.CreateObservableGauge(
            name: "discordbot.process.memory.working_set",
            observeValue: () => Environment.WorkingSet,
            unit: "By",
            description: "Process working set (resident memory)");

        var processStarted = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        _meter.CreateObservableGauge(
            name: "discordbot.process.uptime",
            observeValue: () => (DateTime.UtcNow - processStarted).TotalSeconds,
            unit: "s",
            description: "Seconds since the process started");

        _meter.CreateObservableGauge(
            name: "discordbot.signalr.subscribers",
            observeValues: ObserveSubscribers,
            unit: "{clients}",
            description: "Dashboard hub clients subscribed to live performance groups");
    }

    private IEnumerable<Measurement<int>> ObserveQueueDepths()
    {
        if (_services.GetService<IAuditLogQueue>() is { } auditLogQueue)
        {
            yield return new Measurement<int>(auditLogQueue.Count, new KeyValuePair<string, object?>("queue", "audit_log"));
        }

        if (_services.GetService<IMemberSyncQueue>() is { } memberSyncQueue)
        {
            yield return new Measurement<int>(memberSyncQueue.Count, new KeyValuePair<string, object?>("queue", "member_sync"));
        }
    }

    private IEnumerable<Measurement<long>> ObserveCacheRequests()
    {
        var cache = _services.GetService<IInstrumentedCache>();
        if (cache is null)
        {
            yield break;
        }

        foreach (var stats in cache.GetStatistics())
        {
            yield return new Measurement<long>(stats.Hits, new TagList { { "prefix", stats.KeyPrefix }, { "result", "hit" } });
            yield return new Measurement<long>(stats.Misses, new TagList { { "prefix", stats.KeyPrefix }, { "result", "miss" } });
        }
    }

    private IEnumerable<Measurement<int>> ObserveCacheEntries()
    {
        var cache = _services.GetService<IInstrumentedCache>();
        if (cache is null)
        {
            yield break;
        }

        foreach (var stats in cache.GetStatistics())
        {
            yield return new Measurement<int>(stats.Size, new KeyValuePair<string, object?>("prefix", stats.KeyPrefix));
        }
    }

    private IEnumerable<Measurement<int>> ObserveSubscribers()
    {
        if (_services.GetService<IPerformanceSubscriptionTracker>() is { } tracker)
        {
            yield return new Measurement<int>(tracker.PerformanceGroupClientCount, new KeyValuePair<string, object?>("group", "performance"));
            yield return new Measurement<int>(tracker.SystemHealthGroupClientCount, new KeyValuePair<string, object?>("group", "system_health"));
        }
    }

    public void Dispose() => _meter.Dispose();
}
