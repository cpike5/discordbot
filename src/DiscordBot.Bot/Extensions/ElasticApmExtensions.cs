using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using Elastic.Apm;
using Elastic.Apm.NetCoreAll;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for configuring Elastic APM integration with priority-based sampling.
/// </summary>
public static class ElasticApmExtensions
{
    /// <summary>
    /// Adds Elastic APM with custom transaction filtering based on PrioritySampler logic.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddElasticApmWithPrioritySampling(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure sampling options (reuse existing configuration from OpenTelemetry)
        services.Configure<SamplingOptions>(
            configuration.GetSection($"OpenTelemetry:Tracing:{SamplingOptions.SectionName}"));

        // Register transaction filter as singleton
        services.AddSingleton<ElasticApmTransactionFilter>();

        // Register background service that registers the transaction filter with APM agent after startup
        services.AddHostedService<ElasticApmFilterRegistrationService>();

        return services;
    }
}

/// <summary>
/// Background service that registers the transaction filter with the Elastic APM agent.
/// </summary>
/// <remarks>
/// This service registers the filter after the APM agent has been initialized.
/// The filter implements priority-based sampling consistent with the OpenTelemetry PrioritySampler.
/// </remarks>
public class ElasticApmFilterRegistrationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ElasticApmFilterRegistrationService> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ElasticApmFilterRegistrationService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving dependencies.</param>
    /// <param name="logger">The logger for this service.</param>
    /// <param name="configuration">The application configuration.</param>
    public ElasticApmFilterRegistrationService(
        IServiceProvider serviceProvider,
        ILogger<ElasticApmFilterRegistrationService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Executes the background service, registering the APM transaction filter.
    /// </summary>
    /// <param name="stoppingToken">The cancellation token.</param>
    /// <returns>A completed task once the filter is registered.</returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Check if APM is enabled via configuration
            var apmEnabled = _configuration.GetValue<bool>("ElasticApm:Enabled", true);
            var apmRecording = _configuration.GetValue<bool>("ElasticApm:Recording", true);

            if (!apmEnabled)
            {
                _logger.LogInformation("Elastic APM is disabled via configuration, skipping filter registration");
                return Task.CompletedTask;
            }

            if (!apmRecording)
            {
                _logger.LogInformation("Elastic APM recording is disabled, skipping filter registration");
                return Task.CompletedTask;
            }

            var filter = _serviceProvider.GetRequiredService<ElasticApmTransactionFilter>();
            Agent.AddFilter(filter.Filter);

            var serviceName = _configuration.GetValue<string>("ElasticApm:ServiceName") ?? "discordbot";
            var serverUrl = _configuration.GetValue<string>("ElasticApm:ServerUrl") ?? "not configured";

            _logger.LogInformation(
                "Elastic APM transaction filter registered successfully for service '{ServiceName}' " +
                "with APM server at '{ServerUrl}'",
                serviceName, serverUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register Elastic APM transaction filter. APM tracing may not work correctly");
        }

        return Task.CompletedTask;
    }
}
