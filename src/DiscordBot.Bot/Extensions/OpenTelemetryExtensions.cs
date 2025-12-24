using DiscordBot.Bot.Metrics;
using DiscordBot.Bot.Tracing;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for configuring OpenTelemetry metrics and tracing collection.
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
        services.AddSingleton<BusinessMetrics>();
        services.AddSingleton<SloMetrics>();

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
                metrics.AddMeter(BusinessMetrics.MeterName);
                metrics.AddMeter(SloMetrics.MeterName);

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
    /// Adds OpenTelemetry distributed tracing to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOpenTelemetryTracing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? "discordbot";
        var serviceVersion = configuration["OpenTelemetry:ServiceVersion"]
            ?? typeof(OpenTelemetryExtensions).Assembly
                .GetName().Version?.ToString() ?? "1.0.0";

        // Determine sampling ratio based on environment
        var isProduction = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
        var samplingRatio = configuration.GetValue<double?>("OpenTelemetry:Tracing:SamplingRatio")
            ?? (isProduction ? 0.1 : 1.0);

        var enableConsoleExporter = configuration.GetValue<bool>("OpenTelemetry:Tracing:EnableConsoleExporter");
        var otlpEndpoint = configuration["OpenTelemetry:Tracing:OtlpEndpoint"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: serviceVersion,
                    serviceInstanceId: Environment.MachineName))
            .WithTracing(tracing =>
            {
                // Add custom activity sources
                tracing.AddSource(BotActivitySource.SourceName);
                tracing.AddSource("DiscordBot.Infrastructure");

                // Add ASP.NET Core instrumentation for HTTP requests
                tracing.AddAspNetCoreInstrumentation(options =>
                {
                    // Filter out health checks, metrics, and static files
                    options.Filter = httpContext =>
                    {
                        var path = httpContext.Request.Path.Value ?? "";
                        return !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase)
                            && !path.StartsWith("/metrics", StringComparison.OrdinalIgnoreCase)
                            && !path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
                            && !path.Contains('.'); // Filter static files
                    };

                    // Enrich spans with additional request info
                    options.EnrichWithHttpRequest = (activity, request) =>
                    {
                        // Add correlation ID if present
                        if (request.HttpContext.Items.TryGetValue("CorrelationId", out var correlationId))
                        {
                            activity.SetTag("correlation.id", correlationId?.ToString());
                        }
                    };
                });

                // Add HTTP client instrumentation for outgoing calls (Discord API, etc.)
                tracing.AddHttpClientInstrumentation(options =>
                {
                    // Redact sensitive headers
                    options.FilterHttpRequestMessage = request =>
                    {
                        // Filter out internal health checks
                        return request.RequestUri?.Host != "localhost";
                    };
                });

                // Add Entity Framework Core instrumentation
                tracing.AddEntityFrameworkCoreInstrumentation(options =>
                {
                    // Only include SQL text in non-production for security
                    options.SetDbStatementForText = !isProduction;
                    options.SetDbStatementForStoredProcedure = !isProduction;
                });

                // Configure sampler
                if (samplingRatio < 1.0)
                {
                    tracing.SetSampler(new TraceIdRatioBasedSampler(samplingRatio));
                }

                // Add console exporter for development
                if (enableConsoleExporter)
                {
                    tracing.AddConsoleExporter();
                }

                // Add OTLP exporter if configured
                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = new Uri(otlpEndpoint);

                        // Use gRPC by default, can be configured via settings
                        var protocol = configuration["OpenTelemetry:Tracing:OtlpProtocol"];
                        if (protocol?.Equals("http", StringComparison.OrdinalIgnoreCase) == true)
                        {
                            options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
                        }
                    });
                }
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
