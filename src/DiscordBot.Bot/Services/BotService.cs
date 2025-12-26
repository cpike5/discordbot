using Discord;
using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for bot status and control operations.
/// </summary>
public class BotService : IBotService
{
    private readonly DiscordSocketClient _client;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IDashboardUpdateService _dashboardUpdateService;
    private readonly ILogger<BotService> _logger;
    private readonly BotConfiguration _config;
    private static readonly DateTime _startTime = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="BotService"/> class.
    /// </summary>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="lifetime">The application lifetime.</param>
    /// <param name="dashboardUpdateService">The dashboard update service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="config">The bot configuration.</param>
    public BotService(
        DiscordSocketClient client,
        IHostApplicationLifetime lifetime,
        IDashboardUpdateService dashboardUpdateService,
        ILogger<BotService> logger,
        IOptions<BotConfiguration> config)
    {
        _client = client;
        _lifetime = lifetime;
        _dashboardUpdateService = dashboardUpdateService;
        _logger = logger;
        _config = config.Value;
    }

    /// <inheritdoc/>
    public BotStatusDto GetStatus()
    {
        _logger.LogDebug("Retrieving bot status");

        var status = new BotStatusDto
        {
            Uptime = DateTime.UtcNow - _startTime,
            GuildCount = _client.Guilds.Count,
            LatencyMs = _client.Latency,
            StartTime = _startTime,
            BotUsername = _client.CurrentUser?.Username ?? "Unknown",
            ConnectionState = _client.ConnectionState.ToString()
        };

        _logger.LogTrace("Bot status: ConnectionState={ConnectionState}, GuildCount={GuildCount}, Latency={Latency}ms",
            status.ConnectionState, status.GuildCount, status.LatencyMs);

        return status;
    }

    /// <inheritdoc/>
    public IReadOnlyList<GuildInfoDto> GetConnectedGuilds()
    {
        _logger.LogDebug("Retrieving connected guilds from Discord client");

        var guilds = _client.Guilds
            .Select(g => new GuildInfoDto
            {
                Id = g.Id,
                Name = g.Name,
                MemberCount = g.MemberCount,
                IconUrl = g.IconUrl,
                JoinedAt = g.CurrentUser?.JoinedAt?.UtcDateTime
            })
            .ToList();

        _logger.LogDebug("Retrieved {Count} connected guilds", guilds.Count);

        return guilds.AsReadOnly();
    }

    /// <inheritdoc/>
    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Bot soft restart requested");

        // Disconnect from Discord
        await _client.StopAsync();
        await _client.LogoutAsync();

        _logger.LogInformation("Bot disconnected, waiting before reconnect...");

        // Brief delay to ensure clean disconnect
        await Task.Delay(2000, cancellationToken);

        // Reconnect
        await _client.LoginAsync(TokenType.Bot, _config.Token);
        await _client.StartAsync();

        _logger.LogInformation("Bot reconnected successfully");

        // Broadcast updated bot status after restart (fire-and-forget, failure tolerant)
        _ = BroadcastBotStatusAsync();
    }

    /// <inheritdoc/>
    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Shutdown requested via API");
        _lifetime.StopApplication();
        _logger.LogInformation("Application shutdown initiated");
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public BotConfigurationDto GetConfiguration()
    {
        _logger.LogDebug("Retrieving bot configuration");

        var token = _config.Token ?? string.Empty;
        var maskedToken = token.Length > 4
            ? $"{new string('\u2022', 20)}{token[^4..]}"
            : new string('\u2022', 24);

        // Get Discord.NET version
        var discordNetVersion = typeof(DiscordSocketClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(DiscordSocketClient).Assembly.GetName().Version?.ToString()
            ?? "Unknown";

        // Get application version
        var appVersion = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? "Unknown";

        return new BotConfigurationDto
        {
            TokenMasked = maskedToken,
            TestGuildId = _config.TestGuildId,
            HasTestGuild = _config.TestGuildId.HasValue,
            DatabaseProvider = "SQLite", // TODO: Get from actual configuration
            DiscordNetVersion = discordNetVersion,
            AppVersion = appVersion,
            RuntimeVersion = Environment.Version.ToString(),
            DefaultRateLimitInvokes = _config.DefaultRateLimitInvokes,
            DefaultRateLimitPeriodSeconds = _config.DefaultRateLimitPeriodSeconds
        };
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
}
