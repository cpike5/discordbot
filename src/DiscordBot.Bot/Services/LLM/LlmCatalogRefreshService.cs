using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services.LLM;

/// <summary>
/// Periodically refreshes the local LLM model catalog from OpenRouter. Interval is
/// <c>Llm:CatalogRefreshHours</c> (default 24); a value of 0 disables the refresh entirely - the
/// service exits immediately without registering a heartbeat loop. Only registered when
/// <c>OpenRouter:ApiKey</c> is configured (see <c>AssistantServiceExtensions.AddAssistant</c>).
/// </summary>
public class LlmCatalogRefreshService : MonitoredBackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<LlmOptions> _options;

    public override string ServiceName => "LLM Catalog Refresh Service";

    public LlmCatalogRefreshService(
        IServiceProvider serviceProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<LlmOptions> options,
        ILogger<LlmCatalogRefreshService> logger)
        : base(serviceProvider, logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
    }

    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        var refreshHours = _options.Value.CatalogRefreshHours;
        if (refreshHours <= 0)
        {
            _logger.LogInformation("LLM catalog refresh is disabled (Llm:CatalogRefreshHours = 0)");
            SetStatus("Disabled");
            return;
        }

        var interval = TimeSpan.FromHours(refreshHours);

        // Initial delay to let the app start up, mirroring the other scheduled background services.
        var initialDelay = TimeSpan.FromMinutes(_options.Value.CatalogRefreshInitialDelayMinutes);
        if (initialDelay > TimeSpan.Zero)
        {
            await Task.Delay(initialDelay, stoppingToken);
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var catalogService = scope.ServiceProvider.GetRequiredService<ILlmModelCatalogService>();

                var lastRefresh = await catalogService.GetLastRefreshAsync(stoppingToken);
                if (lastRefresh is { } last && DateTime.UtcNow - last < interval)
                {
                    _logger.LogDebug(
                        "Skipping scheduled LLM catalog refresh; last refresh at {LastRefresh:o} is within the {IntervalHours}h interval",
                        last, refreshHours);
                    UpdateHeartbeat();
                    ClearError();
                }
                else
                {
                    SetStatus("Refreshing");

                    var result = await catalogService.RefreshAsync(userId: null, stoppingToken);

                    _logger.LogInformation(
                        "Scheduled LLM catalog refresh complete: {Added} added, {Updated} updated, {Removed} marked unavailable",
                        result.Added, result.Updated, result.Removed);

                    UpdateHeartbeat();
                    ClearError();
                    SetStatus("Running");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Caught here (rather than left to MonitoredBackgroundService's own fatal-error
                // handling) so a single failed refresh - a bad response, a transient OpenRouter
                // outage - doesn't kill the loop; the next scheduled attempt still runs.
                _logger.LogError(ex, "Scheduled LLM catalog refresh failed");
                RecordError(ex);
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}
