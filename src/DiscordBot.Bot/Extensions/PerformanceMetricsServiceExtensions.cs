using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Commands;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering performance metrics services.
/// </summary>
public static class PerformanceMetricsServiceExtensions
{
    /// <summary>
    /// Adds performance metrics collection and monitoring services to the service collection.
    /// Registers singleton services for latency tracking, connection state, API monitoring,
    /// database metrics, cache instrumentation, and background service health.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPerformanceMetrics(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<PerformanceMetricsOptions>(
            configuration.GetSection(PerformanceMetricsOptions.SectionName));
        services.Configure<PerformanceAlertOptions>(
            configuration.GetSection(PerformanceAlertOptions.SectionName));
        services.Configure<HistoricalMetricsOptions>(
            configuration.GetSection(HistoricalMetricsOptions.SectionName));
        services.Configure<PerformanceBroadcastOptions>(
            configuration.GetSection(PerformanceBroadcastOptions.SectionName));

        // Core metrics services (singleton - maintain in-memory state)
        // Register concrete types first, then add interface mappings
        services.AddSingleton<ConnectionStateService>();
        services.AddSingleton<IConnectionStateService>(sp => sp.GetRequiredService<ConnectionStateService>());

        services.AddSingleton<LatencyHistoryService>();
        services.AddSingleton<ILatencyHistoryService>(sp => sp.GetRequiredService<LatencyHistoryService>());
        services.AddSingleton<IMemoryReportable>(sp => sp.GetRequiredService<LatencyHistoryService>());

        services.AddSingleton<CpuHistoryService>();
        services.AddSingleton<ICpuHistoryService>(sp => sp.GetRequiredService<CpuHistoryService>());
        services.AddSingleton<IMemoryReportable>(sp => sp.GetRequiredService<CpuHistoryService>());

        services.AddSingleton<ApiRequestTracker>();
        services.AddSingleton<IApiRequestTracker>(sp => sp.GetRequiredService<ApiRequestTracker>());
        services.AddSingleton<IMemoryReportable>(sp => sp.GetRequiredService<ApiRequestTracker>());

        services.AddSingleton<IDatabaseMetricsCollector, DatabaseMetricsCollector>();
        services.AddSingleton<IBackgroundServiceHealthRegistry, BackgroundServiceHealthRegistry>();

        // Instrumented cache wrapper (singleton) - also implements IMemoryReportable
        services.AddSingleton<InstrumentedMemoryCache>();
        services.AddSingleton<IInstrumentedCache>(sp => sp.GetRequiredService<InstrumentedMemoryCache>());
        services.AddSingleton<IMemoryReportable>(sp => sp.GetRequiredService<InstrumentedMemoryCache>());

        // Memory diagnostics service (singleton - aggregates IMemoryReportable services)
        services.AddSingleton<IMemoryDiagnosticsService, MemoryDiagnosticsService>();

        // Command performance aggregator - register as singleton first, then as hosted service
        // This avoids the circular dependency deadlock caused by GetServices<IHostedService>()
        services.AddSingleton<CommandPerformanceAggregator>();
        services.AddSingleton<ICommandPerformanceAggregator>(sp => sp.GetRequiredService<CommandPerformanceAggregator>());
        services.AddHostedService<CommandPerformanceAggregator>(sp => sp.GetRequiredService<CommandPerformanceAggregator>());

        // Performance alert services
        services.AddScoped<IPerformanceAlertRepository, PerformanceAlertRepository>();
        services.AddScoped<IPerformanceAlertService, PerformanceAlertService>();

        // Performance notifier for SignalR broadcasting (singleton)
        services.AddSingleton<IPerformanceNotifier, PerformanceNotifier>();

        // Alert monitoring background service - register as singleton first, then as hosted service
        // This avoids the circular dependency deadlock caused by GetServices<IHostedService>()
        services.AddSingleton<AlertMonitoringService>();
        services.AddSingleton<IMetricsProvider>(sp => sp.GetRequiredService<AlertMonitoringService>());
        services.AddHostedService<AlertMonitoringService>(sp => sp.GetRequiredService<AlertMonitoringService>());

        // Historical metrics collection service
        services.AddScoped<IMetricSnapshotRepository, MetricSnapshotRepository>();
        services.AddHostedService<MetricsCollectionService>();

        // Performance subscription tracker (singleton - tracks SignalR group memberships)
        services.AddSingleton<IPerformanceSubscriptionTracker, PerformanceSubscriptionTracker>();

        // Performance metrics broadcast service
        services.AddHostedService<PerformanceMetricsBroadcastService>();

        // CPU sampling background service
        services.AddHostedService<CpuSamplingService>();

        return services;
    }
}
