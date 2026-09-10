using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Periodically re-broadcasts the bot's current status (connection state, latency, guild
/// count, uptime) to all dashboard clients over SignalR. <see cref="IBotStatusBroadcaster.BroadcastStatusAsync"/>
/// is otherwise only driven by Discord gateway connect/disconnect events (see
/// <see cref="BotHostedService"/>) and by <c>BotService</c>'s restart flow, so without this
/// service the dashboard status banner and the Admin/Settings Bot Control panel would only
/// refresh latency/uptime on those events instead of ticking. This service calls that same
/// method - reusing its exact <c>BotStatusUpdateDto</c> builder - rather than constructing
/// its own payload, so the periodic and event-driven broadcasts are always identical in shape.
/// </summary>
public class BotStatusBroadcastService : MonitoredBackgroundService
{
    /// <summary>
    /// How often to re-broadcast bot status. No existing options class has a natural slot for
    /// this: <c>PerformanceBroadcastOptions</c> is scoped to the Performance dashboard's own
    /// metric groups (health/commands/system), not overall bot connection status, so this is
    /// a constant rather than a configurable interval.
    /// </summary>
    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(30);

    private readonly IBotStatusBroadcaster _botStatusBroadcaster;

    /// <inheritdoc/>
    public override string ServiceName => "Bot Status Broadcast Service";

    /// <summary>
    /// Tracing service name used for activity/span naming.
    /// </summary>
    private const string TracingServiceName = "bot_status_broadcast_service";

    /// <summary>
    /// Initializes a new instance of the <see cref="BotStatusBroadcastService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency resolution.</param>
    /// <param name="botStatusBroadcaster">The broadcaster whose <c>BroadcastStatusAsync</c> builds and sends the status payload.</param>
    /// <param name="logger">The logger instance.</param>
    public BotStatusBroadcastService(
        IServiceProvider serviceProvider,
        IBotStatusBroadcaster botStatusBroadcaster,
        ILogger<BotStatusBroadcastService> logger)
        : base(serviceProvider, logger)
    {
        _botStatusBroadcaster = botStatusBroadcaster;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Bot status broadcast service started. Interval: {IntervalSeconds}s",
            BroadcastInterval.TotalSeconds);

        using var timer = new PeriodicTimer(BroadcastInterval);
        var cycle = 0;

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                cycle++;
                var correlationId = Guid.NewGuid().ToString("N")[..16];

                using var activity = BotActivitySource.StartBackgroundServiceActivity(
                    TracingServiceName,
                    cycle,
                    correlationId);

                UpdateHeartbeat();

                try
                {
                    await BroadcastBotStatusAsync();
                    BotActivitySource.SetSuccess(activity);
                    ClearError();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    BotActivitySource.RecordException(activity, ex);
                    _logger.LogError(ex, "Error broadcasting bot status");
                    RecordError(ex);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Bot status broadcast service stopping");
        }
    }

    /// <summary>
    /// Re-broadcasts the current bot status via <see cref="IBotStatusBroadcaster.BroadcastStatusAsync"/>
    /// - the same method the connect/disconnect path uses - so the payload is built and sent
    /// identically. Internal (rather than private) so tests can invoke a single broadcast
    /// cycle directly instead of driving the real 30-second timer.
    /// </summary>
    internal Task BroadcastBotStatusAsync() => _botStatusBroadcaster.BroadcastStatusAsync();
}
