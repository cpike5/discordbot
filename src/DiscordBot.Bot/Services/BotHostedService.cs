using System.Text.Json;
using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Handlers;
using DiscordBot.Bot.Metrics;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Hosted service that manages the Discord bot lifecycle.
/// Handles bot startup, login, and graceful shutdown.
/// </summary>
public class BotHostedService : IHostedService
{
    private readonly DiscordSocketClient _client;
    private readonly InteractionHandler _interactionHandler;
    private readonly MessageLoggingHandler _messageLoggingHandler;
    private readonly ActivityEventTrackingHandler _activityEventTrackingHandler;
    private readonly WelcomeHandler _welcomeHandler;
    private readonly MemberEventHandler _memberEventHandler;
    private readonly VoiceStateHandler _voiceStateHandler;
    private readonly AutoModerationHandler _autoModerationHandler;
    private readonly AssistantMessageHandler _assistantMessageHandler;
    private readonly BusinessMetrics _businessMetrics;
    private readonly IDashboardUpdateService _dashboardUpdateService;
    private readonly IAuditLogQueue _auditLogQueue;
    private readonly IMemberSyncQueue _memberSyncQueue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BotConfiguration _config;
    private readonly ApplicationOptions _applicationOptions;
    private readonly ILogger<BotHostedService> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IHostEnvironment _environment;
    private readonly ISettingsService _settingsService;
    private readonly IRatWatchStatusService _ratWatchStatusService;
    private readonly IBotStatusService _botStatusService;
    private readonly IConnectionStateService? _connectionStateService;
    private readonly ILatencyHistoryService? _latencyHistoryService;
    private readonly IApiRequestTracker? _apiRequestTracker;
    private readonly NotificationOptions _notificationOptions;
    private static readonly DateTime _startTime = DateTime.UtcNow;
    private bool _initialConnectionComplete;

    public BotHostedService(
        DiscordSocketClient client,
        InteractionHandler interactionHandler,
        MessageLoggingHandler messageLoggingHandler,
        ActivityEventTrackingHandler activityEventTrackingHandler,
        WelcomeHandler welcomeHandler,
        MemberEventHandler memberEventHandler,
        VoiceStateHandler voiceStateHandler,
        AutoModerationHandler autoModerationHandler,
        AssistantMessageHandler assistantMessageHandler,
        BusinessMetrics businessMetrics,
        IDashboardUpdateService dashboardUpdateService,
        IAuditLogQueue auditLogQueue,
        IMemberSyncQueue memberSyncQueue,
        IServiceScopeFactory scopeFactory,
        ISettingsService settingsService,
        IRatWatchStatusService ratWatchStatusService,
        IBotStatusService botStatusService,
        IOptions<BotConfiguration> config,
        IOptions<ApplicationOptions> applicationOptions,
        ILogger<BotHostedService> logger,
        IHostApplicationLifetime lifetime,
        IHostEnvironment environment,
        IOptions<NotificationOptions> notificationOptions,
        IConnectionStateService? connectionStateService = null,
        ILatencyHistoryService? latencyHistoryService = null,
        IApiRequestTracker? apiRequestTracker = null)
    {
        _client = client;
        _interactionHandler = interactionHandler;
        _messageLoggingHandler = messageLoggingHandler;
        _activityEventTrackingHandler = activityEventTrackingHandler;
        _welcomeHandler = welcomeHandler;
        _memberEventHandler = memberEventHandler;
        _voiceStateHandler = voiceStateHandler;
        _autoModerationHandler = autoModerationHandler;
        _assistantMessageHandler = assistantMessageHandler;
        _businessMetrics = businessMetrics;
        _dashboardUpdateService = dashboardUpdateService;
        _auditLogQueue = auditLogQueue;
        _memberSyncQueue = memberSyncQueue;
        _scopeFactory = scopeFactory;
        _settingsService = settingsService;
        _ratWatchStatusService = ratWatchStatusService;
        _botStatusService = botStatusService;
        _config = config.Value;
        _applicationOptions = applicationOptions.Value;
        _logger = logger;
        _lifetime = lifetime;
        _environment = environment;
        _connectionStateService = connectionStateService;
        _latencyHistoryService = latencyHistoryService;
        _apiRequestTracker = apiRequestTracker;
        _notificationOptions = notificationOptions.Value;
    }

    /// <summary>
    /// Starts the Discord bot.
    /// Wires up logging, initializes interaction handler, validates token, and logs in.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var activity = BotActivitySource.StartLifecycleActivity("startup");

        try
        {
            _logger.LogInformation("Starting Discord bot hosted service");

            // Wire Discord.NET logging to ILogger
            _client.Log += LogDiscordMessageAsync;

            // Wire connection state changes for dashboard updates
            _client.Connected += OnConnectedAsync;
            _client.Disconnected += OnDisconnectedAsync;
            _client.LatencyUpdated += OnLatencyUpdatedAsync;

            // Wire guild join/leave events to business metrics
            _client.JoinedGuild += OnJoinedGuildAsync;
            _client.LeftGuild += OnLeftGuildAsync;

            // Wire message logging handler
            _client.MessageReceived += _messageLoggingHandler.HandleMessageReceivedAsync;

            // Wire activity event tracking handler (consent-free analytics)
            _client.MessageReceived += _activityEventTrackingHandler.HandleMessageReceivedAsync;
            _client.ReactionAdded += _activityEventTrackingHandler.HandleReactionAddedAsync;
            _client.UserVoiceStateUpdated += _activityEventTrackingHandler.HandleUserVoiceStateUpdatedAsync;
            _client.UserJoined += _activityEventTrackingHandler.HandleUserJoinedAsync;
            _client.UserLeft += _activityEventTrackingHandler.HandleUserLeftAsync;

            // Wire auto-moderation handler for message and join monitoring
            _client.MessageReceived += _autoModerationHandler.HandleMessageReceivedAsync;
            _client.UserJoined += _autoModerationHandler.HandleUserJoinedAsync;

            // Wire AI assistant handler for bot mentions
            _client.MessageReceived += _assistantMessageHandler.HandleMessageReceivedAsync;

            // Wire welcome handler for new member joins
            _client.UserJoined += _welcomeHandler.HandleUserJoinedAsync;

            // Wire member event handlers for directory sync
            _client.UserJoined += _memberEventHandler.HandleUserJoinedAsync;
            _client.UserLeft += _memberEventHandler.HandleUserLeftAsync;
            _client.GuildMemberUpdated += _memberEventHandler.HandleGuildMemberUpdatedAsync;

            // Wire voice state handler for real-time member count updates
            _client.UserVoiceStateUpdated += _voiceStateHandler.HandleUserVoiceStateUpdatedAsync;

            // Queue member sync for new guilds
            _client.JoinedGuild += OnBotJoinedGuild;

            // Subscribe to settings changes for real-time updates
            _settingsService.SettingsChanged += OnSettingsChangedAsync;

            // Subscribe to Rat Watch status updates
            _ratWatchStatusService.StatusUpdateRequested += OnRatWatchStatusUpdateRequested;

            // Register custom status source with CustomStatus priority
            _botStatusService.RegisterStatusSource(
                "CustomStatus",
                StatusSourcePriority.CustomStatus,
                GetCustomStatusAsync);

            // Initialize interaction handler (discovers and registers commands)
            await _interactionHandler.InitializeAsync();

            // Validate token
            if (string.IsNullOrWhiteSpace(_config.Token))
            {
                _logger.LogCritical("Discord bot token is missing in configuration. Please configure it via user secrets or appsettings.json");
                activity?.SetTag(TracingConstants.Attributes.ErrorMessage, "Missing Discord token");
                BotActivitySource.RecordException(activity, new InvalidOperationException("Discord bot token is missing"));
                _lifetime.StopApplication();
                return;
            }

            activity?.SetTag("bot.version", _applicationOptions.Version);
            activity?.SetTag("bot.environment", _environment.EnvironmentName);

            // Login and start the bot
            await _client.LoginAsync(TokenType.Bot, _config.Token);
            await _client.StartAsync();

            _logger.LogInformation("Discord bot started successfully");

            // Log bot startup to audit log
            _auditLogQueue.Enqueue(new AuditLogCreateDto
            {
                Category = AuditLogCategory.System,
                Action = AuditLogAction.BotStarted,
                ActorType = AuditLogActorType.Bot,
                Details = JsonSerializer.Serialize(new
                {
                    Version = _applicationOptions.Version,
                    Environment = _environment.EnvironmentName,
                    StartTime = _startTime
                })
            });

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to start Discord bot");
            BotActivitySource.RecordException(activity, ex);
            _lifetime.StopApplication();
        }
    }

    /// <summary>
    /// Stops the Discord bot gracefully.
    /// Logs out and disconnects from Discord.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        using var activity = BotActivitySource.StartLifecycleActivity("shutdown");

        try
        {
            _logger.LogInformation("Stopping Discord bot hosted service");

            var uptime = DateTime.UtcNow - _startTime;

            activity?.SetTag("bot.uptime_seconds", uptime.TotalSeconds);
            activity?.SetTag("bot.shutdown_reason", "ApplicationStopping");

            // Unsubscribe from events
            _client.Connected -= OnConnectedAsync;
            _client.Disconnected -= OnDisconnectedAsync;
            _client.LatencyUpdated -= OnLatencyUpdatedAsync;
            _client.MessageReceived -= _messageLoggingHandler.HandleMessageReceivedAsync;
            _client.MessageReceived -= _activityEventTrackingHandler.HandleMessageReceivedAsync;
            _client.ReactionAdded -= _activityEventTrackingHandler.HandleReactionAddedAsync;
            _client.UserVoiceStateUpdated -= _activityEventTrackingHandler.HandleUserVoiceStateUpdatedAsync;
            _client.UserJoined -= _activityEventTrackingHandler.HandleUserJoinedAsync;
            _client.UserLeft -= _activityEventTrackingHandler.HandleUserLeftAsync;
            _client.MessageReceived -= _autoModerationHandler.HandleMessageReceivedAsync;
            _client.UserJoined -= _autoModerationHandler.HandleUserJoinedAsync;
            _client.MessageReceived -= _assistantMessageHandler.HandleMessageReceivedAsync;
            _client.UserJoined -= _welcomeHandler.HandleUserJoinedAsync;
            _client.UserJoined -= _memberEventHandler.HandleUserJoinedAsync;
            _client.UserLeft -= _memberEventHandler.HandleUserLeftAsync;
            _client.GuildMemberUpdated -= _memberEventHandler.HandleGuildMemberUpdatedAsync;
            _client.JoinedGuild -= OnBotJoinedGuild;
            _settingsService.SettingsChanged -= OnSettingsChangedAsync;
            _ratWatchStatusService.StatusUpdateRequested -= OnRatWatchStatusUpdateRequested;

            // Log bot shutdown to audit log before stopping
            _auditLogQueue.Enqueue(new AuditLogCreateDto
            {
                Category = AuditLogCategory.System,
                Action = AuditLogAction.BotStopped,
                ActorType = AuditLogActorType.Bot,
                Details = JsonSerializer.Serialize(new
                {
                    Reason = "ApplicationStopping",
                    UptimeSeconds = uptime.TotalSeconds,
                    UptimeFormatted = uptime.ToString(@"d\.hh\:mm\:ss")
                })
            });

            await _client.StopAsync();
            await _client.LogoutAsync();

            _logger.LogInformation("Discord bot stopped successfully");
            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while stopping Discord bot");
            BotActivitySource.RecordException(activity, ex);
        }
    }

    /// <summary>
    /// Logs Discord.NET messages to ILogger with appropriate log levels.
    /// Also forwards messages to the API request tracker for metrics collection.
    /// </summary>
    private Task LogDiscordMessageAsync(LogMessage message)
    {
        var logLevel = MapLogSeverity(message.Severity);
        _logger.Log(logLevel, message.Exception, "[Discord.NET] {Message}", message.Message);

        // Forward to API request tracker for performance metrics
        _apiRequestTracker?.TrackLogEvent(
            message.Source ?? string.Empty,
            message.Message ?? string.Empty,
            (int)message.Severity);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps Discord.NET LogSeverity to Microsoft.Extensions.Logging LogLevel.
    /// </summary>
    private static LogLevel MapLogSeverity(LogSeverity severity) => severity switch
    {
        LogSeverity.Critical => LogLevel.Critical,
        LogSeverity.Error => LogLevel.Error,
        LogSeverity.Warning => LogLevel.Warning,
        LogSeverity.Info => LogLevel.Information,
        LogSeverity.Verbose => LogLevel.Debug,
        LogSeverity.Debug => LogLevel.Trace,
        _ => LogLevel.Information
    };

    /// <summary>
    /// Handles bot connected event and broadcasts status update.
    /// </summary>
    private Task OnConnectedAsync()
    {
        using var activity = BotActivitySource.StartGatewayActivity(
            TracingConstants.Spans.DiscordGatewayConnected,
            latency: _client.Latency,
            connectionState: _client.ConnectionState.ToString());

        try
        {
            _logger.LogInformation("Bot connected to Discord");

            activity?.SetTag(TracingConstants.Attributes.GuildsCount, _client.Guilds.Count);

            // Record connection state change for performance metrics
            _connectionStateService?.RecordConnected();

            // Check for active Rat Watches and set appropriate status (fire-and-forget)
            // This prioritizes Rat Watch status over custom status
            _ = ApplyStartupStatusAsync();

            // Broadcast status update (fire-and-forget, failure tolerant)
            _ = BroadcastBotStatusAsync();

            // Log connection event to audit log (fire-and-forget)
            _auditLogQueue.Enqueue(new AuditLogCreateDto
            {
                Category = AuditLogCategory.System,
                Action = AuditLogAction.BotConnected,
                ActorType = AuditLogActorType.Bot,
                Details = JsonSerializer.Serialize(new
                {
                    Latency = _client.Latency,
                    GuildCount = _client.Guilds.Count
                })
            });

            // Create admin notification for reconnection (skip initial startup)
            if (_initialConnectionComplete)
            {
                _ = CreateBotStatusNotificationAsync(
                    "Bot Connected",
                    $"Bot reconnected to Discord. Latency: {_client.Latency}ms, Guilds: {_client.Guilds.Count}");
            }
            else
            {
                _initialConnectionComplete = true;
            }

            BotActivitySource.SetSuccess(activity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnConnectedAsync");
            BotActivitySource.RecordException(activity, ex);
        }

        return Task.CompletedTask;
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
    /// Handles bot disconnected event and broadcasts status update.
    /// </summary>
    private Task OnDisconnectedAsync(Exception exception)
    {
        using var activity = BotActivitySource.StartGatewayActivity(
            TracingConstants.Spans.DiscordGatewayDisconnected,
            connectionState: _client.ConnectionState.ToString());

        try
        {
            _logger.LogWarning(exception, "Bot disconnected from Discord");

            if (exception != null)
            {
                activity?.SetTag(TracingConstants.Attributes.ErrorMessage, exception.Message);
                activity?.SetTag("exception.type", exception.GetType().Name);
                BotActivitySource.RecordException(activity, exception);
            }

            // Record disconnection for performance metrics
            _connectionStateService?.RecordDisconnected(exception);

            // Broadcast status update (fire-and-forget, failure tolerant)
            _ = BroadcastBotStatusAsync();

            // Log disconnection event to audit log (fire-and-forget)
            _auditLogQueue.Enqueue(new AuditLogCreateDto
            {
                Category = AuditLogCategory.System,
                Action = AuditLogAction.BotDisconnected,
                ActorType = AuditLogActorType.Bot,
                Details = JsonSerializer.Serialize(new
                {
                    Exception = exception?.Message,
                    ExceptionType = exception?.GetType().Name
                })
            });

            // Create admin notification for disconnection
            var disconnectMessage = exception != null
                ? $"Bot disconnected from Discord. Reason: {exception.Message}"
                : "Bot disconnected from Discord.";
            _ = CreateBotStatusNotificationAsync("Bot Disconnected", disconnectMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in OnDisconnectedAsync");
            BotActivitySource.RecordException(activity, ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles latency updates and broadcasts periodic status updates.
    /// </summary>
    private Task OnLatencyUpdatedAsync(int oldLatency, int newLatency)
    {
        _logger.LogTrace("Bot latency updated from {OldLatency}ms to {NewLatency}ms", oldLatency, newLatency);

        // Record latency sample for performance metrics
        _latencyHistoryService?.RecordSample(newLatency);

        // Broadcast status update periodically (fire-and-forget, failure tolerant)
        _ = BroadcastBotStatusAsync();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Broadcasts current bot status to dashboard clients.
    /// Fire-and-forget with internal error handling.
    /// </summary>
    private async Task BroadcastBotStatusAsync()
    {
        try
        {
            var status = new BotStatusUpdateDto
            {
                ConnectionState = _client.ConnectionState.ToString(),
                Latency = _client.Latency,
                GuildCount = _client.Guilds.Count,
                Uptime = DateTime.UtcNow - _startTime,
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

    /// <summary>
    /// Handles bot joining a new guild event.
    /// Queues member sync for the guild and records metrics.
    /// </summary>
    private Task OnBotJoinedGuild(SocketGuild guild)
    {
        _logger.LogInformation("Bot joined new guild {GuildId} ({GuildName})", guild.Id, guild.Name);

        // Queue member sync for the new guild
        _memberSyncQueue.EnqueueGuild(guild.Id, MemberSyncReason.BotJoinedGuild);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles guild join events and records them in business metrics.
    /// </summary>
    private Task OnJoinedGuildAsync(SocketGuild guild)
    {
        _logger.LogInformation("Bot joined guild {GuildId} ({GuildName})", guild.Id, guild.Name);
        _businessMetrics.RecordGuildJoin();

        // Broadcast guild activity update (fire-and-forget, failure tolerant)
        _ = BroadcastGuildActivityAsync(guild, "BotJoined");

        // Update guild active status in database (fire-and-forget, failure tolerant)
        _ = UpdateGuildActiveStatusAsync(guild.Id, isActive: true);

        // Create admin notification for guild join
        _ = CreateGuildEventNotificationAsync(
            guild.Id,
            $"Bot Joined {guild.Name}",
            $"Bot was added to guild '{guild.Name}' ({guild.Id}). Members: {guild.MemberCount}");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles guild leave events and records them in business metrics.
    /// </summary>
    private Task OnLeftGuildAsync(SocketGuild guild)
    {
        _logger.LogInformation("Bot left guild {GuildId} ({GuildName})", guild.Id, guild.Name);
        _businessMetrics.RecordGuildLeave();

        // Broadcast guild activity update (fire-and-forget, failure tolerant)
        _ = BroadcastGuildActivityAsync(guild, "BotLeft");

        // Update guild active status and LeftAt timestamp in database (fire-and-forget, failure tolerant)
        _ = UpdateGuildActiveStatusAsync(guild.Id, isActive: false);

        // Create admin notification for guild leave
        _ = CreateGuildEventNotificationAsync(
            guild.Id,
            $"Bot Left {guild.Name}",
            $"Bot was removed from guild '{guild.Name}' ({guild.Id}).");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates guild active status in the database.
    /// Fire-and-forget with internal error handling.
    /// </summary>
    private async Task UpdateGuildActiveStatusAsync(ulong guildId, bool isActive)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var guildRepository = scope.ServiceProvider.GetRequiredService<IGuildRepository>();
            await guildRepository.SetActiveStatusAsync(guildId, isActive);

            _logger.LogDebug("Updated guild {GuildId} active status to {IsActive}", guildId, isActive);
        }
        catch (Exception ex)
        {
            // Log but don't throw - this is fire-and-forget
            _logger.LogWarning(ex, "Failed to update guild {GuildId} active status to {IsActive}, but continuing normal operation", guildId, isActive);
        }
    }

    /// <summary>
    /// Broadcasts guild activity to dashboard clients.
    /// Fire-and-forget with internal error handling.
    /// </summary>
    private async Task BroadcastGuildActivityAsync(SocketGuild guild, string eventType)
    {
        try
        {
            var update = new GuildActivityUpdateDto
            {
                GuildId = guild.Id,
                GuildName = guild.Name,
                EventType = eventType,
                Timestamp = DateTime.UtcNow
            };

            await _dashboardUpdateService.BroadcastGuildActivityAsync(update);
        }
        catch (Exception ex)
        {
            // Log but don't throw - this is fire-and-forget
            _logger.LogWarning(ex, "Failed to broadcast guild activity update for {GuildId}, but continuing normal operation", guild.Id);
        }
    }

    /// <summary>
    /// Creates an admin notification for bot status changes (connected/disconnected).
    /// Fire-and-forget with internal error handling.
    /// </summary>
    private async Task CreateBotStatusNotificationAsync(string title, string message)
    {
        if (!_notificationOptions.EnableBotStatusChanges)
        {
            _logger.LogDebug("Bot status notifications are disabled, skipping notification");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var deduplicationWindow = TimeSpan.FromMinutes(_notificationOptions.DuplicateSuppressionMinutes);

            await notificationService.CreateForAllAdminsAsync(
                NotificationType.BotStatus,
                title,
                message,
                linkUrl: "/Admin/Performance",
                relatedEntityType: "BotStatus",
                relatedEntityId: title.Contains("Connected") ? "connected" : "disconnected",
                deduplicationWindow: deduplicationWindow);

            _logger.LogDebug("Created bot status notification: {Title}", title);
        }
        catch (Exception ex)
        {
            // Log but don't throw - this is fire-and-forget
            _logger.LogWarning(ex, "Failed to create bot status notification, but continuing normal operation");
        }
    }

    /// <summary>
    /// Creates an admin notification for guild events (joined/left).
    /// Fire-and-forget with internal error handling.
    /// </summary>
    private async Task CreateGuildEventNotificationAsync(ulong guildId, string title, string message)
    {
        if (!_notificationOptions.EnableGuildEvents)
        {
            _logger.LogDebug("Guild event notifications are disabled, skipping notification for guild {GuildId}", guildId);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var deduplicationWindow = TimeSpan.FromMinutes(_notificationOptions.DuplicateSuppressionMinutes);
            var eventType = title.Contains("Joined") ? "joined" : "left";

            await notificationService.CreateForAllAdminsAsync(
                NotificationType.GuildEvent,
                title,
                message,
                linkUrl: $"/Guilds/Details?id={guildId}",
                relatedEntityType: "Guild",
                relatedEntityId: $"{guildId}:{eventType}",
                deduplicationWindow: deduplicationWindow);

            _logger.LogDebug("Created guild event notification for guild {GuildId}: {Title}", guildId, title);
        }
        catch (Exception ex)
        {
            // Log but don't throw - this is fire-and-forget
            _logger.LogWarning(ex, "Failed to create guild event notification for guild {GuildId}, but continuing normal operation", guildId);
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
            _ = _botStatusService.RefreshStatusAsync();
        }
    }

    /// <summary>
    /// Handles Rat Watch status update requests.
    /// Called when a Rat Watch state changes (created, voting started, voting ended, cleared early, etc.).
    /// </summary>
    private void OnRatWatchStatusUpdateRequested(object? sender, EventArgs e)
    {
        _logger.LogDebug("Rat Watch status update event received, refreshing bot status");
        _ = _botStatusService.RefreshStatusAsync();
    }

    /// <summary>
    /// Applies the appropriate bot status on startup.
    /// Evaluates all registered status sources and applies the highest priority active status.
    /// Fire-and-forget with internal error handling.
    /// </summary>
    private async Task ApplyStartupStatusAsync()
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
}
