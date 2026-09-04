using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Publishes bot connection status to dashboard clients and drives Discord presence.
/// Split out of <see cref="BotHostedService"/> so status/presence broadcasting is a
/// standalone concern, independent of gateway login/logout lifecycle.
/// </summary>
public class BotStatusBroadcaster : IBotStatusBroadcaster
{
    private readonly DiscordSocketClient _client;
    private readonly IDashboardUpdateService _dashboardUpdateService;
    private readonly ILogger<BotStatusBroadcaster> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IRatWatchStatusService _ratWatchStatusService;
    private readonly IBotStatusService _botStatusService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundTaskRunner _backgroundTaskRunner;
    private readonly IBotUptimeProvider _uptimeProvider;
    private bool _initialized;

    public BotStatusBroadcaster(
        DiscordSocketClient client,
        IDashboardUpdateService dashboardUpdateService,
        ILogger<BotStatusBroadcaster> logger,
        ISettingsService settingsService,
        IRatWatchStatusService ratWatchStatusService,
        IBotStatusService botStatusService,
        IServiceScopeFactory scopeFactory,
        IBackgroundTaskRunner backgroundTaskRunner,
        IBotUptimeProvider uptimeProvider)
    {
        _client = client;
        _dashboardUpdateService = dashboardUpdateService;
        _logger = logger;
        _settingsService = settingsService;
        _ratWatchStatusService = ratWatchStatusService;
        _botStatusService = botStatusService;
        _scopeFactory = scopeFactory;
        _backgroundTaskRunner = backgroundTaskRunner;
        _uptimeProvider = uptimeProvider;
    }

    /// <inheritdoc />
    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        // Register custom status source with CustomStatus priority
        _botStatusService.RegisterStatusSource(
            "CustomStatus",
            StatusSourcePriority.CustomStatus,
            GetCustomStatusAsync);

        // Subscribe to settings changes for real-time updates
        _settingsService.SettingsChanged += OnSettingsChangedAsync;

        // Subscribe to Rat Watch status updates
        _ratWatchStatusService.StatusUpdateRequested += OnRatWatchStatusUpdateRequested;

        _initialized = true;
    }

    /// <inheritdoc />
    public void Shutdown()
    {
        if (!_initialized)
        {
            return;
        }

        _settingsService.SettingsChanged -= OnSettingsChangedAsync;
        _ratWatchStatusService.StatusUpdateRequested -= OnRatWatchStatusUpdateRequested;
        _botStatusService.UnregisterStatusSource("CustomStatus");

        _initialized = false;
    }

    /// <inheritdoc />
    public async Task BroadcastStatusAsync()
    {
        try
        {
            var status = new BotStatusUpdateDto
            {
                ConnectionState = _client.ConnectionState.ToString(),
                Latency = _client.Latency,
                GuildCount = _client.Guilds.Count,
                Uptime = _uptimeProvider.Uptime,
                Timestamp = DateTime.UtcNow
            };

            await _dashboardUpdateService.BroadcastBotStatusAsync(status);
        }
        catch (Exception ex)
        {
            // Log but don't throw - this is fire-and-forget
            _logger.LogWarning(ex, "Failed to broadcast bot status update, but continuing normal operation");
        }
    }

    /// <inheritdoc />
    public async Task ApplyStartupStatusAsync()
    {
        try
        {
            _logger.LogDebug("Applying startup bot status");
            // Refresh status to evaluate all sources (Rat Watch, custom status, etc.)
            await _botStatusService.RefreshStatusAsync();

            var (sourceName, message) = _botStatusService.GetCurrentStatus();
            _logger.LogInformation("Startup bot status applied: Source={Source}, Message={Message}",
                sourceName, message ?? "(none)");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to apply startup status, but continuing normal operation");
        }
    }

    /// <summary>
    /// Gets the custom status message from settings if configured.
    /// Returns null if no custom status is configured (allows other status sources to take priority).
    /// </summary>
    private async Task<string?> GetCustomStatusAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();

            var statusMessage = await settingsService.GetSettingValueAsync<string>("General:StatusMessage");
            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                _logger.LogTrace("Custom status provider returning: {StatusMessage}", statusMessage);
                return statusMessage;
            }

            _logger.LogTrace("Custom status provider returning null (no configured status)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to retrieve custom status message");
            return null;
        }
    }

    /// <summary>
    /// Handles settings changed events to apply real-time updates.
    /// </summary>
    private void OnSettingsChangedAsync(object? sender, SettingsChangedEventArgs e)
    {
        // Check if bot status message was updated
        if (e.UpdatedKeys.Contains("General:StatusMessage"))
        {
            _logger.LogInformation("Bot status message setting changed, refreshing bot status");
            // Refresh status to apply the new custom status (respects priority)
            _backgroundTaskRunner.Run(_ => _botStatusService.RefreshStatusAsync(), "RefreshBotStatus.SettingsChanged");
        }
    }

    /// <summary>
    /// Handles Rat Watch status update requests.
    /// Called when a Rat Watch state changes (created, voting started, voting ended, cleared early, etc.).
    /// </summary>
    private void OnRatWatchStatusUpdateRequested(object? sender, EventArgs e)
    {
        _logger.LogDebug("Rat Watch status update event received, refreshing bot status");
        _backgroundTaskRunner.Run(_ => _botStatusService.RefreshStatusAsync(), "RefreshBotStatus.RatWatchUpdate");
    }
}
