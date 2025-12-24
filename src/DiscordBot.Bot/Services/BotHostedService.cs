using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Handlers;
using DiscordBot.Bot.Metrics;
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
    private readonly BusinessMetrics _businessMetrics;
    private readonly BotConfiguration _config;
    private readonly ILogger<BotHostedService> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public BotHostedService(
        DiscordSocketClient client,
        InteractionHandler interactionHandler,
        BusinessMetrics businessMetrics,
        IOptions<BotConfiguration> config,
        ILogger<BotHostedService> logger,
        IHostApplicationLifetime lifetime)
    {
        _client = client;
        _interactionHandler = interactionHandler;
        _businessMetrics = businessMetrics;
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

        // Wire guild join/leave events to business metrics
        _client.JoinedGuild += OnJoinedGuildAsync;
        _client.LeftGuild += OnLeftGuildAsync;

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
    /// Handles guild join events and records them in business metrics.
    /// </summary>
    private Task OnJoinedGuildAsync(SocketGuild guild)
    {
        _logger.LogInformation("Bot joined guild {GuildId} ({GuildName})", guild.Id, guild.Name);
        _businessMetrics.RecordGuildJoin();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles guild leave events and records them in business metrics.
    /// </summary>
    private Task OnLeftGuildAsync(SocketGuild guild)
    {
        _logger.LogInformation("Bot left guild {GuildId} ({GuildName})", guild.Id, guild.Name);
        _businessMetrics.RecordGuildLeave();
        return Task.CompletedTask;
    }
}
