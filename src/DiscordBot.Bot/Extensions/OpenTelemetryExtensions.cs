using System.Reflection;
using DiscordBot.Bot.Health;
using DiscordBot.Bot.Metrics;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using DiscordBot.Agents;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for configuring OpenTelemetry metrics and tracing collection.
/// </summary>
public static class OpenTelemetryExtensions
{
    /// <summary>
    /// Built-in .NET meters worth exporting beyond what the instrumentation packages add:
    /// SignalR connections, Kestrel connections and the Npgsql connection pool.
    /// </summary>
    private static readonly string[] FrameworkMeters =
    {
        "Microsoft.AspNetCore.Hosting",
        "Microsoft.AspNetCore.Server.Kestrel",
        "Microsoft.AspNetCore.Http.Connections",
        "Microsoft.AspNetCore.Routing",
        "Microsoft.AspNetCore.Diagnostics",
        "Microsoft.AspNetCore.RateLimiting",
        "System.Net.Http",
        "System.Net.NameResolution",
        "Npgsql"
    };

    /// <summary>
    /// Adds OpenTelemetry metrics with the Prometheus scrape endpoint and, when an OTLP endpoint is
    /// configured, an OTLP push exporter.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOpenTelemetryMetrics(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var (otlp, otlpEndpoint) = ResolveOtlp(configuration);

        // Register custom metrics classes as singletons
        services.AddSingleton<BotMetrics>();
        services.AddSingleton<ApiMetrics>();
        services.AddSingleton<BusinessMetrics>();
        services.AddSingleton<SloMetrics>();
        services.AddSingleton<VoxMetrics>();
        services.AddSingleton<DiscordGatewayMetrics>();
        services.AddSingleton<LlmMetrics>();
        services.AddSingleton<BackgroundServiceMetrics>();
        services.AddSingleton<FeatureMetrics>();
        services.AddSingleton<AppStateMetrics>();

        services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                // Add custom meters
                metrics.AddMeter(BotMetrics.MeterName);
                metrics.AddMeter(ApiMetrics.MeterName);
                metrics.AddMeter(BusinessMetrics.MeterName);
                metrics.AddMeter(SloMetrics.MeterName);
                metrics.AddMeter(VoxMetrics.MeterName);
                metrics.AddMeter(DiscordGatewayMetrics.MeterName);
                metrics.AddMeter(LlmMetrics.MeterName);
                metrics.AddMeter(BackgroundServiceMetrics.MeterName);
                metrics.AddMeter(FeatureMetrics.MeterName);
                metrics.AddMeter(AppStateMetrics.MeterName);
                metrics.AddMeter(HealthCheckMetricsPublisher.MeterName);
                metrics.AddMeter(AgentsMetrics.MeterName);
                metrics.AddMeter(FrameworkMeters);

                // Observable gauges exist only once their class is constructed, and nothing else
                // resolves these two; build them with the meter provider.
                metrics.AddInstrumentation(sp => sp.GetRequiredService<AppStateMetrics>());
                metrics.AddInstrumentation(sp => sp.GetRequiredService<BackgroundServiceMetrics>());

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

                // Configure histogram bucket boundaries for VOX command duration
                metrics.AddView(
                    instrumentName: "discordbot.vox.command.duration",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = new double[]
                        {
                            10, 25, 50, 100, 250, 500, 1000, 2000, 5000
                        }
                    });

                // Configure histogram bucket boundaries for VOX concatenation duration
                metrics.AddView(
                    instrumentName: "discordbot.vox.concatenation.duration",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = new double[]
                        {
                            5, 10, 25, 50, 100, 250, 500, 1000, 2000
                        }
                    });

                // Configure histogram bucket boundaries for VOX message word count
                metrics.AddView(
                    instrumentName: "discordbot.vox.message.words",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = new double[]
                        {
                            1, 2, 5, 10, 15, 20, 25, 30, 40, 50
                        }
                    });

                // Configure histogram bucket boundaries for VOX match percentage
                metrics.AddView(
                    instrumentName: "discordbot.vox.match.percentage",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = new double[]
                        {
                            0, 10, 20, 30, 40, 50, 60, 70, 80, 90, 100
                        }
                    });

                // Configure histogram bucket boundaries for VOX audio size
                metrics.AddView(
                    instrumentName: "discordbot.vox.audio.bytes",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = new double[]
                        {
                            1024, 10240, 102400, 512000, 1048576, 5242880, 10485760, 52428800
                        }
                    });

                // Millisecond buckets for the new duration histograms (LLM calls run to seconds).
                var slowCallBuckets = new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = new double[]
                    {
                        50, 100, 250, 500, 1000, 2500, 5000, 10000, 20000, 30000, 60000, 120000
                    }
                };
                metrics.AddView("discordbot.llm.request.duration", slowCallBuckets);
                metrics.AddView("discordbot.agent.completion.duration", slowCallBuckets);
                metrics.AddView(
                    instrumentName: "discordbot.agent.tool.duration",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = new double[] { 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000, 30000 }
                    });
                metrics.AddView(
                    instrumentName: "discordbot.audio.playback.duration",
                    new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = new double[] { 0.5, 1, 2, 5, 10, 20, 30, 60, 120, 300, 600 }
                    });

                // Npgsql's second-based histograms carry no bucket advice, so the SDK's default
                // (millisecond-scale) boundaries would put every query in the first bucket.
                var dbSecondsBuckets = new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = new double[]
                    {
                        0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10
                    }
                };
                metrics.AddView("db.client.commands.duration", dbSecondsBuckets);
                metrics.AddView("db.client.connections.create_time", dbSecondsBuckets);
                metrics.AddView("db.client.connections.wait_time", dbSecondsBuckets);
                metrics.AddView("db.client.connections.use_time", dbSecondsBuckets);

                // Prometheus scrape endpoint (/metrics), kept for existing scrapers
                metrics.AddPrometheusExporter();

                // OTLP push, the path the Grafana stack uses
                if (otlp.ExportMetrics && otlpEndpoint is not null)
                {
                    metrics.AddOtlpExporter((exporterOptions, readerOptions) =>
                    {
                        ConfigureOtlpExporter(exporterOptions, otlp, otlpEndpoint, "metrics");
                        readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds =
                            Math.Max(1, otlp.MetricExportIntervalSeconds) * 1000;
                        // Cumulative is what Prometheus expects from OTLP.
                        readerOptions.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative;
                    });
                }
            });

        // Push health check results into the Health meter every 30 seconds.
        services.AddSingleton<IHealthCheckPublisher, HealthCheckMetricsPublisher>();
        services.Configure<HealthCheckPublisherOptions>(options =>
        {
            options.Delay = TimeSpan.FromSeconds(10);
            options.Period = TimeSpan.FromSeconds(30);
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
        // Configure sampling options
        services.Configure<SamplingOptions>(
            configuration.GetSection($"OpenTelemetry:Tracing:{SamplingOptions.SectionName}"));

        // Determine if environment is production
        var isProduction = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";

        // Override default sampling rate based on environment if not explicitly configured
        services.PostConfigure<SamplingOptions>(options =>
        {
            // If DefaultRate is still at default 0.1, set to 1.0 for non-production
            if (!isProduction && Math.Abs(options.DefaultRate - 0.1) < 0.001)
            {
                options.DefaultRate = 1.0;
            }
        });

        var enableConsoleExporter = configuration.GetValue<bool>("OpenTelemetry:Tracing:EnableConsoleExporter");
        var (otlp, sharedEndpoint) = ResolveOtlp(configuration);

        // The shared OTLP endpoint wins; the older tracing-only setting is honoured when it is absent.
        var legacyEndpoint = configuration["OpenTelemetry:Tracing:OtlpEndpoint"];
        var otlpEndpoint = sharedEndpoint ?? (string.IsNullOrWhiteSpace(legacyEndpoint) ? null : legacyEndpoint);
        if (sharedEndpoint is null && otlpEndpoint is not null
            && configuration["OpenTelemetry:Tracing:OtlpProtocol"] is { Length: > 0 } legacyProtocol)
        {
            otlp.Protocol = legacyProtocol;
        }

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                // Add custom activity sources
                tracing.AddSource(BotActivitySource.SourceName);
                tracing.AddSource("DiscordBot.Infrastructure");
                tracing.AddSource(AgentsActivitySource.SourceName);
                tracing.AddSource("DiscordBot.Vox");

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

                // Configure priority-based sampler
                // The sampler is created via DI to access IOptions<SamplingOptions> and ILogger
                tracing.SetSampler(sp =>
                {
                    var samplingOptions = sp.GetRequiredService<IOptions<SamplingOptions>>();
                    var logger = sp.GetRequiredService<ILogger<PrioritySampler>>();
                    return new PrioritySampler(samplingOptions, logger);
                });

                // Add console exporter for development
                if (enableConsoleExporter)
                {
                    tracing.AddConsoleExporter();
                }

                // Add OTLP exporter if configured
                if (otlp.ExportTraces && otlpEndpoint is not null)
                {
                    tracing.AddOtlpExporter(options =>
                        ConfigureOtlpExporter(options, otlp, otlpEndpoint, "traces"));
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

    /// <summary>
    /// Adds all observability services: OpenTelemetry metrics, tracing, and Elastic APM.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind observability configuration options
        services.Configure<ObservabilityOptions>(
            configuration.GetSection(ObservabilityOptions.SectionName));

        // One resource for every signal, matching the attributes the Serilog OTLP sink sends with logs.
        var resourceAttributes = BuildResourceAttributes(configuration);
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddAttributes(resourceAttributes));

        services.AddOpenTelemetryMetrics(configuration);
        services.AddOpenTelemetryTracing(configuration);
        services.AddElasticApmWithPrioritySampling(configuration);

        return services;
    }

    /// <summary>
    /// Binds <see cref="OtlpOptions"/> and resolves the endpoint all signals export to:
    /// <c>OpenTelemetry:Otlp:Endpoint</c>, then the standard <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The bound options and the endpoint, or a null endpoint when OTLP is not configured.</returns>
    public static (OtlpOptions Options, string? Endpoint) ResolveOtlp(IConfiguration configuration)
    {
        var options = configuration.GetSection(OtlpOptions.SectionName).Get<OtlpOptions>() ?? new OtlpOptions();

        var endpoint = options.Endpoint;
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
        }

        return (options, string.IsNullOrWhiteSpace(endpoint) ? null : endpoint.Trim());
    }

    /// <summary>
    /// Builds the resource attributes shared by logs, traces and metrics.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>service.name, service.version, service.instance.id and deployment.environment.</returns>
    public static IReadOnlyDictionary<string, object> BuildResourceAttributes(IConfiguration configuration)
    {
        var environment = configuration[HostDefaults.EnvironmentKey]
            ?? configuration["ASPNETCORE_ENVIRONMENT"]
            ?? configuration["DOTNET_ENVIRONMENT"]
            ?? "Production";

        return new Dictionary<string, object>
        {
            ["service.name"] = configuration["OpenTelemetry:ServiceName"] ?? "discordbot",
            ["service.version"] = GetAssemblyVersion(),
            ["service.instance.id"] = Environment.MachineName,
            ["deployment.environment"] = environment.ToLowerInvariant()
        };
    }

    private static void ConfigureOtlpExporter(
        OtlpExporterOptions exporterOptions,
        OtlpOptions otlp,
        string endpoint,
        string signal)
    {
        exporterOptions.Protocol = otlp.UseHttpProtobuf ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;
        exporterOptions.Endpoint = otlp.GetSignalEndpoint(endpoint, signal);

        var headers = otlp.ParseHeaders();
        if (headers.Count > 0)
        {
            exporterOptions.Headers = string.Join(",", headers.Select(h => $"{h.Key}={h.Value}"));
        }
    }

    /// <summary>
    /// Gets the assembly version from the informational version attribute or falls back to assembly version.
    /// </summary>
    /// <returns>The service version string.</returns>
    private static string GetAssemblyVersion()
    {
        var assembly = typeof(OpenTelemetryExtensions).Assembly;
        return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
    }
}
