using Discord;
using Discord.WebSocket;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for managing the bot's Discord status during active Rat Watches.
/// Coordinates between Rat Watch state changes and bot presence display.
/// </summary>
public class RatWatchStatusService : IRatWatchStatusService
{
    private readonly DiscordSocketClient _client;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<RatWatchStatusService> _logger;

    private bool _isRatWatchActive;
    private readonly SemaphoreSlim _statusLock = new(1, 1);

    public RatWatchStatusService(
        DiscordSocketClient client,
        IServiceScopeFactory scopeFactory,
        ISettingsService settingsService,
        ILogger<RatWatchStatusService> logger)
    {
        _client = client;
        _scopeFactory = scopeFactory;
        _settingsService = settingsService;
        _logger = logger;
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
        // Use a lock to prevent race conditions when multiple status updates are triggered simultaneously
        await _statusLock.WaitAsync(ct);
        try
        {
            // Check if there are active watches
            using var scope = _scopeFactory.CreateScope();
            var ratWatchService = scope.ServiceProvider.GetRequiredService<IRatWatchService>();

            var hasActiveWatches = await ratWatchService.HasActiveWatchesAsync(ct);

            _logger.LogDebug("Rat Watch status check: HasActiveWatches={HasActive}, CurrentlyActive={IsActive}",
                hasActiveWatches, _isRatWatchActive);

            // If status hasn't changed, no need to update Discord
            if (hasActiveWatches == _isRatWatchActive)
            {
                return _isRatWatchActive;
            }

            // Status changed - update Discord presence
            if (hasActiveWatches)
            {
                await SetRatWatchStatusAsync();
                _isRatWatchActive = true;
                _logger.LogInformation("Bot status changed to Rat Watch mode");
            }
            else
            {
                await RestoreNormalStatusAsync(ct);
                _isRatWatchActive = false;
                _logger.LogInformation("Bot status restored to normal");
            }

            return _isRatWatchActive;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update Rat Watch bot status");
            return _isRatWatchActive;
        }
        finally
        {
            _statusLock.Release();
        }
    }

    /// <summary>
    /// Sets the bot status to indicate an active Rat Watch.
    /// </summary>
    private async Task SetRatWatchStatusAsync()
    {
        try
        {
            await _client.SetActivityAsync(new Game("for rats...", ActivityType.Watching));
            _logger.LogDebug("Bot activity set to 'Watching for rats...'");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set Rat Watch status");
        }
    }

    /// <summary>
    /// Restores the bot status to the normal/configured status.
    /// </summary>
    private async Task RestoreNormalStatusAsync(CancellationToken ct)
    {
        try
        {
            var statusMessage = await _settingsService.GetSettingValueAsync<string>("General:StatusMessage");
            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                await _client.SetGameAsync(statusMessage);
                _logger.LogDebug("Bot status restored to configured message: {StatusMessage}", statusMessage);
            }
            else
            {
                await _client.SetGameAsync(null);
                _logger.LogDebug("Bot status cleared (no configured status message)");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore normal bot status");
        }
    }
}
