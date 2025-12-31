using System.Text.Json;
using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Handlers;
using DiscordBot.Bot.Metrics;
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
    private readonly WelcomeHandler _welcomeHandler;
    private readonly MemberEventHandler _memberEventHandler;
    private readonly AutoModerationHandler _autoModerationHandler;
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
    private static readonly DateTime _startTime = DateTime.UtcNow;

    public BotHostedService(
        DiscordSocketClient client,
        InteractionHandler interactionHandler,
        MessageLoggingHandler messageLoggingHandler,
        WelcomeHandler welcomeHandler,
        MemberEventHandler memberEventHandler,
        AutoModerationHandler autoModerationHandler,
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
        IHostEnvironment environment)
    {
        _client = client;
        _interactionHandler = interactionHandler;
        _messageLoggingHandler = messageLoggingHandler;
        _welcomeHandler = welcomeHandler;
        _memberEventHandler = memberEventHandler;
        _autoModerationHandler = autoModerationHandler;
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
    }

    /// <summary>
    /// Starts the Discord bot.
    /// Wires up logging, initializes interaction handler, validates token, and logs in.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
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

        // Wire auto-moderation handler for message and join monitoring
        _client.MessageReceived += _autoModerationHandler.HandleMessageReceivedAsync;
        _client.UserJoined += _autoModerationHandler.HandleUserJoinedAsync;

        // Wire welcome handler for new member joins
        _client.UserJoined += _welcomeHandler.HandleUserJoinedAsync;

        // Wire member event handlers for directory sync
        _client.UserJoined += _memberEventHandler.HandleUserJoinedAsync;
        _client.UserLeft += _memberEventHandler.HandleUserLeftAsync;
        _client.GuildMemberUpdated += _memberEventHandler.HandleGuildMemberUpdatedAsync;

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
            _lifetime.StopApplication();
            return;
        }

        try
        {
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
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Failed to start Discord bot");
            _lifetime.StopApplication();
        }
    }

    /// <summary>
    /// Stops the Discord bot gracefully.
    /// Logs out and disconnects from Discord.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Discord bot hosted service");

        var uptime = DateTime.UtcNow - _startTime;

        try
        {
            // Unsubscribe from events
            _client.Connected -= OnConnectedAsync;
            _client.Disconnected -= OnDisconnectedAsync;
            _client.LatencyUpdated -= OnLatencyUpdatedAsync;
            _client.MessageReceived -= _messageLoggingHandler.HandleMessageReceivedAsync;
            _client.MessageReceived -= _autoModerationHandler.HandleMessageReceivedAsync;
            _client.UserJoined -= _autoModerationHandler.HandleUserJoinedAsync;
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while stopping Discord bot");
        }
    }

    /// <summary>
    /// Logs Discord.NET messages to ILogger with appropriate log levels.
    /// </summary>
    private Task LogDiscordMessageAsync(LogMessage message)
    {
        var logLevel = MapLogSeverity(message.Severity);
        _logger.Log(logLevel, message.Exception, "[Discord.NET] {Message}", message.Message);
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
        _logger.LogInformation("Bot connected to Discord");

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
        _logger.LogWarning(exception, "Bot disconnected from Discord");

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

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles latency updates and broadcasts periodic status updates.
    /// </summary>
    private Task OnLatencyUpdatedAsync(int oldLatency, int newLatency)
    {
        _logger.LogTrace("Bot latency updated from {OldLatency}ms to {NewLatency}ms", oldLatency, newLatency);

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

        return Task.CompletedTask;
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
