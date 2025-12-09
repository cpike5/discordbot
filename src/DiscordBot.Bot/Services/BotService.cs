using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for bot status and control operations.
/// </summary>
public class BotService : IBotService
{
    private readonly DiscordSocketClient _client;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<BotService> _logger;
    private static readonly DateTime _startTime = DateTime.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="BotService"/> class.
    /// </summary>
    /// <param name="client">The Discord socket client.</param>
    /// <param name="lifetime">The application lifetime.</param>
    /// <param name="logger">The logger.</param>
    public BotService(
        DiscordSocketClient client,
        IHostApplicationLifetime lifetime,
        ILogger<BotService> logger)
    {
        _client = client;
        _lifetime = lifetime;
        _logger = logger;
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
    public Task RestartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Restart requested but not implemented - this feature requires external process management");
        throw new NotSupportedException("Bot restart is not currently supported. Use an external process manager for restart capabilities.");
    }

    /// <inheritdoc/>
    public Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Shutdown requested via API");
        _lifetime.StopApplication();
        _logger.LogInformation("Application shutdown initiated");
        return Task.CompletedTask;
    }
}
