using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Handlers;
using DiscordBot.Bot.Metrics;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
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
    private readonly BusinessMetrics _businessMetrics;
    private readonly IDashboardUpdateService _dashboardUpdateService;
    private readonly BotConfiguration _config;
    private readonly ILogger<BotHostedService> _logger;
    private readonly IHostApplicationLifetime _lifetime;
    private static readonly DateTime _startTime = DateTime.UtcNow;

    public BotHostedService(
        DiscordSocketClient client,
        InteractionHandler interactionHandler,
        MessageLoggingHandler messageLoggingHandler,
        WelcomeHandler welcomeHandler,
        BusinessMetrics businessMetrics,
        IDashboardUpdateService dashboardUpdateService,
        IOptions<BotConfiguration> config,
        ILogger<BotHostedService> logger,
        IHostApplicationLifetime lifetime)
    {
        _client = client;
        _interactionHandler = interactionHandler;
        _messageLoggingHandler = messageLoggingHandler;
        _welcomeHandler = welcomeHandler;
        _businessMetrics = businessMetrics;
        _dashboardUpdateService = dashboardUpdateService;
        _config = config.Value;
        _logger = logger;
        _lifetime = lifetime;
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

        // Wire welcome handler for new member joins
        _client.UserJoined += _welcomeHandler.HandleUserJoinedAsync;

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

        try
        {
            // Unsubscribe from events
            _client.Connected -= OnConnectedAsync;
            _client.Disconnected -= OnDisconnectedAsync;
            _client.LatencyUpdated -= OnLatencyUpdatedAsync;
            _client.MessageReceived -= _messageLoggingHandler.HandleMessageReceivedAsync;
            _client.UserJoined -= _welcomeHandler.HandleUserJoinedAsync;

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

        // Broadcast status update (fire-and-forget, failure tolerant)
        _ = BroadcastBotStatusAsync();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles bot disconnected event and broadcasts status update.
    /// </summary>
    private Task OnDisconnectedAsync(Exception exception)
    {
        _logger.LogWarning(exception, "Bot disconnected from Discord");

        // Broadcast status update (fire-and-forget, failure tolerant)
        _ = BroadcastBotStatusAsync();

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
}
