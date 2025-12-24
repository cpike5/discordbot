using DiscordBot.Bot.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for configuring OpenTelemetry metrics collection.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Adds OpenTelemetry metrics with Prometheus exporter to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOpenTelemetryMetrics(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "discordbot";
        var serviceVersion = configuration["OpenTelemetry:ServiceVersion"]
            ?? typeof(OpenTelemetryExtensions).Assembly
                .GetName().Version?.ToString() ?? "1.0.0";

        // Register custom metrics classes as singletons
        services.AddSingleton<BotMetrics>();
        services.AddSingleton<ApiMetrics>();

        // Configure OpenTelemetry
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: serviceVersion,
                    serviceInstanceId: Environment.MachineName))
            .WithMetrics(metrics =>
            {
                // Add custom meters
                metrics.AddMeter(BotMetrics.MeterName);
                metrics.AddMeter(ApiMetrics.MeterName);

                // Add ASP.NET Core instrumentation
                metrics.AddAspNetCoreInstrumentation();

                // Add HTTP client instrumentation
                metrics.AddHttpClientInstrumentation();

                // Add runtime instrumentation (GC, ThreadPool, etc.)
                metrics.AddRuntimeInstrumentation();

                // Configure histogram bucket boundaries for command duration
                metrics.AddView(
                    instrumentName: "discordbot.command.duration",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = new double[]
                        {
                            5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000
                        }
                    });

                // Configure histogram bucket boundaries for API request duration
                metrics.AddView(
                    instrumentName: "discordbot.api.request.duration",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = new double[]
                        {
                            1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500
                        }
                    });

                // Configure histogram bucket boundaries for component duration
                metrics.AddView(
                    instrumentName: "discordbot.component.duration",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = new double[]
                        {
                            5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000
                        }
                    });

                // Add Prometheus exporter
                metrics.AddPrometheusExporter();
            });

        return services;
    }

    /// <summary>
    /// Maps the Prometheus metrics scraping endpoint.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UsePrometheusMetrics(this IApplicationBuilder app)
    {
        // Map the /metrics endpoint for Prometheus scraping
        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        return app;
    }
}
