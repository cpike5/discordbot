using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Services.RatWatch;

/// <summary>
/// Service for managing the bot's Discord status during active Rat Watches.
/// Coordinates between Rat Watch state changes and bot presence display.
/// </summary>
public class RatWatchStatusService : IRatWatchStatusService
{
    private readonly IBotStatusService _botStatusService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RatWatchStatusService> _logger;

    public RatWatchStatusService(
        IBotStatusService botStatusService,
        IServiceScopeFactory scopeFactory,
        ILogger<RatWatchStatusService> logger)
    {
        _botStatusService = botStatusService;
        _scopeFactory = scopeFactory;
        _logger = logger;

        // Register Rat Watch as a status source with RatWatch priority
        _botStatusService.RegisterStatusSource(
            "RatWatch",
            StatusSourcePriority.RatWatch,
            GetRatWatchStatusAsync);

        _logger.LogDebug("RatWatchStatusService initialized and registered as status source");
    }

    /// <inheritdoc/>
    public event EventHandler? StatusUpdateRequested;

    /// <inheritdoc/>
    public void RequestStatusUpdate()
    {
        _logger.LogDebug("Rat Watch status update requested");
        StatusUpdateRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateBotStatusAsync(CancellationToken ct = default)
    {
        _logger.LogDebug("Rat Watch status update requested, triggering status refresh");

        try
        {
            // Refresh the overall bot status (re-evaluates all status sources)
            await _botStatusService.RefreshStatusAsync();

            // Check if there are active watches for return value
            using var scope = _scopeFactory.CreateScope();
            var ratWatchService = scope.ServiceProvider.GetRequiredService<IRatWatchService>();
            var hasActiveWatches = await ratWatchService.HasActiveWatchesAsync(ct);

            _logger.LogDebug("Rat Watch status update completed: HasActiveWatches={HasActive}", hasActiveWatches);
            return hasActiveWatches;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update Rat Watch bot status");
            return false;
        }
    }

    /// <summary>
    /// Gets the Rat Watch status message if there are active watches.
    /// Returns null if no active watches (allows other status sources to take priority).
    /// </summary>
    private async Task<string?> GetRatWatchStatusAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var ratWatchService = scope.ServiceProvider.GetRequiredService<IRatWatchService>();

            var hasActiveWatches = await ratWatchService.HasActiveWatchesAsync();
            if (hasActiveWatches)
            {
                _logger.LogTrace("Rat Watch status provider returning 'Watching for rats...'");
                return "Watching for rats...";
            }

            _logger.LogTrace("Rat Watch status provider returning null (no active watches)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check for active Rat Watches in status provider");
            return null;
        }
    }
}
