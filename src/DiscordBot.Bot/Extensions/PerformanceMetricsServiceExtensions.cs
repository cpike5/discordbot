using DiscordBot.Bot.Services;
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

        // Core metrics services (singleton - maintain in-memory state)
        services.AddSingleton<IConnectionStateService, ConnectionStateService>();
        services.AddSingleton<ILatencyHistoryService, LatencyHistoryService>();
        services.AddSingleton<IApiRequestTracker, ApiRequestTracker>();
        services.AddSingleton<IDatabaseMetricsCollector, DatabaseMetricsCollector>();
        services.AddSingleton<IBackgroundServiceHealthRegistry, BackgroundServiceHealthRegistry>();

        // Instrumented cache wrapper (singleton)
        services.AddSingleton<IInstrumentedCache, InstrumentedMemoryCache>();

        // Memory diagnostics service (singleton - aggregates IMemoryReportable services)
        services.AddSingleton<IMemoryDiagnosticsService, MemoryDiagnosticsService>();

        // Command performance aggregator as background service
        services.AddHostedService<CommandPerformanceAggregator>();

        // Register the aggregator interface separately so it can be injected
        services.AddSingleton<ICommandPerformanceAggregator>(sp =>
            sp.GetServices<IHostedService>()
              .OfType<CommandPerformanceAggregator>()
              .First());

        // Performance alert services
        services.AddScoped<IPerformanceAlertRepository, PerformanceAlertRepository>();
        services.AddScoped<IPerformanceAlertService, PerformanceAlertService>();

        // Alert monitoring background service
        services.AddHostedService<AlertMonitoringService>();

        // Register the metrics provider interface separately so it can be injected
        // Points to the same AlertMonitoringService instance
        services.AddSingleton<IMetricsProvider>(sp =>
            sp.GetServices<IHostedService>()
              .OfType<AlertMonitoringService>()
              .First());

        // Historical metrics collection service
        services.AddScoped<IMetricSnapshotRepository, MetricSnapshotRepository>();
        services.AddHostedService<MetricsCollectionService>();

        return services;
    }
}
