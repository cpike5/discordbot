using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Background service that automatically disconnects the bot from voice channels when it is alone.
/// Runs at configured intervals and checks each active connection for inactivity timeout.
/// </summary>
public class VoiceAutoLeaveService : MonitoredBackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IAudioService _audioService;
    private readonly DiscordSocketClient _client;
    private readonly IOptions<VoiceChannelOptions> _options;

    public override string ServiceName => "Voice Auto-Leave Service";

    /// <summary>
    /// Gets the tracing service name in snake_case format.
    /// </summary>
    private string TracingServiceName => "voice_auto_leave_service";

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceAutoLeaveService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for health monitoring.</param>
    /// <param name="audioService">The audio service for managing voice connections.</param>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="options">The voice channel configuration options.</param>
    /// <param name="logger">The logger.</param>
    public VoiceAutoLeaveService(
        IServiceProvider serviceProvider,
        IAudioService audioService,
        DiscordSocketClient client,
        IOptions<VoiceChannelOptions> options,
        ILogger<VoiceAutoLeaveService> logger)
        : base(serviceProvider, logger)
    {
        _serviceProvider = serviceProvider;
        _audioService = audioService;
        _client = client;
        _options = options;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteMonitoredAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Voice auto-leave service starting");

        var timeoutSeconds = _options.Value.AutoLeaveTimeoutSeconds;
        var checkIntervalSeconds = _options.Value.CheckIntervalSeconds;

        if (timeoutSeconds == 0)
        {
            _logger.LogInformation("Voice auto-leave disabled (timeout set to 0), service will not perform checks");
            // Keep service running but idle
            await Task.Delay(Timeout.Infinite, stoppingToken);
            return;
        }

        _logger.LogInformation(
            "Voice auto-leave service enabled. Timeout: {TimeoutSeconds}s, Check interval: {IntervalSeconds}s",
            timeoutSeconds,
            checkIntervalSeconds);

        // Wait for Discord client to connect
        while (_client.ConnectionState != ConnectionState.Connected && !stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Waiting for Discord client to connect (current state: {State})", _client.ConnectionState);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        if (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Voice auto-leave service stopping before Discord connection established");
            return;
        }

        _logger.LogInformation("Discord client connected, voice auto-leave service ready");

        var executionCycle = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            executionCycle++;
            var correlationId = Guid.NewGuid().ToString("N")[..16];

            using var activity = BotActivitySource.StartBackgroundServiceActivity(
                TracingServiceName,
                executionCycle,
                correlationId);

            UpdateHeartbeat();

            try
            {
                var disconnectedCount = await CheckAndDisconnectIdleConnectionsAsync(stoppingToken);

                BotActivitySource.SetRecordsProcessed(activity, disconnectedCount);
                BotActivitySource.SetSuccess(activity);
                ClearError();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during voice auto-leave check");
                BotActivitySource.RecordException(activity, ex);
                RecordError(ex);
            }

            // Wait for next check interval
            var interval = TimeSpan.FromSeconds(checkIntervalSeconds);
            await Task.Delay(interval, stoppingToken);
        }

        _logger.LogInformation("Voice auto-leave service stopping");
    }

    /// <summary>
    /// Checks all active voice connections and disconnects from channels where the bot is alone
    /// and the timeout has elapsed since the last activity.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to respect during processing.</param>
    /// <returns>The number of connections that were disconnected.</returns>
    private async Task<int> CheckAndDisconnectIdleConnectionsAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Checking for idle voice connections");

        // Get snapshot of active connections
        // We need to access the internal method, so cast to the concrete type
        var audioServiceImpl = _audioService as AudioService;
        if (audioServiceImpl == null)
        {
            _logger.LogWarning("AudioService is not the expected implementation type, cannot check connections");
            return 0;
        }

        var connections = audioServiceImpl.GetActiveConnections();

        if (connections.Count == 0)
        {
            _logger.LogTrace("No active voice connections to check");
            return 0;
        }

        _logger.LogDebug("Found {Count} active voice connections to check", connections.Count);

        var disconnectedCount = 0;
        var timeoutThreshold = TimeSpan.FromSeconds(_options.Value.AutoLeaveTimeoutSeconds);
        var now = DateTime.UtcNow;

        foreach (var (guildId, connectionInfo) in connections)
        {
            try
            {
                var guild = _client.GetGuild(guildId);
                if (guild == null)
                {
                    _logger.LogWarning("Guild {GuildId} not found, skipping auto-leave check", guildId);
                    continue;
                }

                var voiceChannel = guild.GetVoiceChannel(connectionInfo.ChannelId);
                if (voiceChannel == null)
                {
                    _logger.LogWarning("Voice channel {ChannelId} not found in guild {GuildId}, skipping auto-leave check",
                        connectionInfo.ChannelId, guildId);
                    continue;
                }

                // Get users in the voice channel (excluding bots)
                var humanUsersInChannel = voiceChannel.Users.Count(u => !u.IsBot);

                // Check if bot is alone
                if (humanUsersInChannel > 0)
                {
                    _logger.LogTrace("Voice channel {ChannelId} in guild {GuildId} has {UserCount} human users, not leaving",
                        connectionInfo.ChannelId, guildId, humanUsersInChannel);
                    continue;
                }

                // Check if timeout has elapsed since last activity
                var timeSinceLastActivity = now - connectionInfo.LastActivity;
                if (timeSinceLastActivity < timeoutThreshold)
                {
                    var remainingTime = timeoutThreshold - timeSinceLastActivity;
                    _logger.LogTrace("Bot is alone in voice channel {ChannelId} in guild {GuildId}, but timeout not reached (remaining: {RemainingSeconds}s)",
                        connectionInfo.ChannelId, guildId, remainingTime.TotalSeconds);
                    continue;
                }

                // Timeout reached - disconnect
                _logger.LogInformation("Bot is alone in voice channel {ChannelId} in guild {GuildId} and timeout reached ({TimeoutSeconds}s), disconnecting",
                    connectionInfo.ChannelId, guildId, _options.Value.AutoLeaveTimeoutSeconds);

                await _audioService.LeaveChannelAsync(guildId, stoppingToken);
                disconnectedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking auto-leave for guild {GuildId}", guildId);
            }
        }

        if (disconnectedCount > 0)
        {
            _logger.LogInformation("Disconnected from {Count} idle voice connections", disconnectedCount);
        }
        else
        {
            _logger.LogTrace("No idle voice connections to disconnect");
        }

        return disconnectedCount;
    }
}
