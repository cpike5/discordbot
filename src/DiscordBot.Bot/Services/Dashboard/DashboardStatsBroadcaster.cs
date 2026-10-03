using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services.Dashboard;

/// <summary>
/// Default <see cref="IDashboardStatsBroadcaster"/>. The first <see cref="NotifyChanged"/> starts a
/// short wait; calls that arrive during it join the same broadcast. The wait also gives the command
/// log (written fire-and-forget after a command) time to land before the count is read.
/// </summary>
public class DashboardStatsBroadcaster : IDashboardStatsBroadcaster
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDashboardUpdateService _updateService;
    private readonly ILogger<DashboardStatsBroadcaster> _logger;
    private readonly object _gate = new();
    private bool _pending;

    public DashboardStatsBroadcaster(
        IServiceScopeFactory scopeFactory,
        IDashboardUpdateService updateService,
        ILogger<DashboardStatsBroadcaster> logger)
    {
        _scopeFactory = scopeFactory;
        _updateService = updateService;
        _logger = logger;
    }

    /// <summary>How long a burst of changes is collected before one broadcast. Tests shorten it.</summary>
    public TimeSpan Debounce { get; set; } = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public void NotifyChanged()
    {
        lock (_gate)
        {
            if (_pending) return;
            _pending = true;
        }

        _ = Task.Run(BroadcastAfterDelayAsync);
    }

    private async Task BroadcastAfterDelayAsync()
    {
        try
        {
            await Task.Delay(Debounce).ConfigureAwait(false);

            // Reopen the gate before reading, so a change that lands mid-read schedules another pass.
            lock (_gate) { _pending = false; }

            using var scope = _scopeFactory.CreateScope();
            var provider = scope.ServiceProvider.GetRequiredService<IDashboardStatsProvider>();
            var stats = await provider.GetStatsAsync().ConfigureAwait(false);
            await _updateService.BroadcastStatsUpdateAsync(stats).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            lock (_gate) { _pending = false; }
            _logger.LogWarning(ex, "Failed to broadcast dashboard stats; dashboards refresh them on reconnect");
        }
    }
}
