using DiscordBot.Bot.Extensions;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for bot status and control operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BotController : ControllerBase
{
    private readonly IBotService _botService;
    private readonly IGuildService _guildService;
    private readonly ICommandLogService _commandLogService;
    private readonly IMemoryCache _cache;
    private readonly CachingOptions _cachingOptions;
    private readonly ILogger<BotController> _logger;

    private const string DashboardStatsCacheKey = "dashboard:aggregated-stats";

    /// <summary>
    /// Initializes a new instance of the <see cref="BotController"/> class.
    /// </summary>
    /// <param name="botService">The bot service.</param>
    /// <param name="guildService">The guild service.</param>
    /// <param name="commandLogService">The command log service.</param>
    /// <param name="cache">The memory cache.</param>
    /// <param name="cachingOptions">The caching configuration options.</param>
    /// <param name="logger">The logger.</param>
    public BotController(
        IBotService botService,
        IGuildService guildService,
        ICommandLogService commandLogService,
        IMemoryCache cache,
        IOptions<CachingOptions> cachingOptions,
        ILogger<BotController> logger)
    {
        _botService = botService;
        _guildService = guildService;
        _commandLogService = commandLogService;
        _cache = cache;
        _cachingOptions = cachingOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current bot status.
    /// </summary>
    /// <returns>Bot status information including uptime, guild count, and latency.</returns>
    [HttpGet("status")]
    [ProducesResponseType(typeof(BotStatusDto), StatusCodes.Status200OK)]
    public ActionResult<BotStatusDto> GetStatus()
    {
        _logger.LogDebug("Bot status requested");

        var status = _botService.GetStatus();

        _logger.LogTrace("Bot status retrieved: {ConnectionState}, {GuildCount} guilds",
            status.ConnectionState, status.GuildCount);

        return Ok(status);
    }

    /// <summary>
    /// Gets the list of guilds the bot is currently connected to.
    /// </summary>
    /// <returns>A list of guild information.</returns>
    [HttpGet("guilds")]
    [ProducesResponseType(typeof(IReadOnlyList<GuildInfoDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<GuildInfoDto>> GetConnectedGuilds()
    {
        _logger.LogDebug("Connected guilds list requested");

        var guilds = _botService.GetConnectedGuilds();

        _logger.LogTrace("Retrieved {Count} connected guilds", guilds.Count);

        return Ok(guilds);
    }

    /// <summary>
    /// Gets aggregated dashboard statistics for initial load or SignalR fallback.
    /// Returns bot status, guild stats, command stats, and recent activity.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregated dashboard statistics.</returns>
    [HttpGet("dashboard-stats")]
    [Authorize(Policy = "RequireViewer")]
    [ProducesResponseType(typeof(DashboardAggregatedDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DashboardAggregatedDto>> GetDashboardStats(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Dashboard stats requested");

        // Try to get from cache first
        if (_cache.TryGetValue(DashboardStatsCacheKey, out DashboardAggregatedDto? cachedStats) && cachedStats != null)
        {
            _logger.LogTrace("Dashboard stats retrieved from cache");
            return Ok(cachedStats);
        }

        // Build fresh stats
        var stats = await BuildDashboardStatsAsync(cancellationToken);

        // Cache the result
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_cachingOptions.DashboardStatsCacheDurationSeconds)
        };
        _cache.Set(DashboardStatsCacheKey, stats, cacheOptions);

        _logger.LogTrace("Dashboard stats retrieved and cached: {GuildCount} guilds, {TotalCommands} commands",
            stats.GuildStats.TotalGuilds, stats.CommandStats.TotalCommands);

        return Ok(stats);
    }

    /// <summary>
    /// Builds aggregated dashboard statistics from all sources.
    /// </summary>
    private async Task<DashboardAggregatedDto> BuildDashboardStatsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var since24Hours = now.AddHours(-24);

        // Get bot status
        var botStatus = _botService.GetStatus();

        // Get all guilds for member count
        var guilds = await _guildService.GetAllGuildsAsync(cancellationToken);

        // Get command stats for last 24 hours
        var commandUsage = await _commandLogService.GetCommandStatsAsync(since24Hours, cancellationToken);

        // Get recent command logs
        var recentLogsResponse = await _commandLogService.GetLogsAsync(
            new CommandLogQueryDto { Page = 1, PageSize = 10 },
            cancellationToken);

        // Build response matching SignalR update format
        return new DashboardAggregatedDto
        {
            BotStatus = new BotStatusUpdateDto
            {
                ConnectionState = botStatus.ConnectionState,
                Latency = botStatus.LatencyMs,
                GuildCount = botStatus.GuildCount,
                Uptime = botStatus.Uptime,
                Timestamp = now
            },
            GuildStats = new GuildStatsDto
            {
                TotalGuilds = guilds.Count,
                TotalMembers = guilds.Sum(g => g.MemberCount ?? 0)
            },
            CommandStats = new CommandStatsDto
            {
                TotalCommands = commandUsage.Values.Sum(),
                SuccessfulCommands = recentLogsResponse.Items.Count(c => c.Success),
                FailedCommands = recentLogsResponse.Items.Count(c => !c.Success),
                CommandUsage = commandUsage
            },
            RecentActivity = recentLogsResponse.Items
                .Select(log => new RecentActivityItemDto
                {
                    Id = log.Id,
                    Type = "CommandExecuted",
                    Description = $"/{log.CommandName}",
                    Timestamp = log.ExecutedAt,
                    GuildId = log.GuildId,
                    GuildName = log.GuildName,
                    UserId = log.UserId,
                    Username = log.Username,
                    Success = log.Success
                })
                .ToList(),
            Timestamp = now
        };
    }

    /// <summary>
    /// Restarts the bot.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Accepted response.</returns>
    [HttpPost("restart")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Restart(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Bot restart requested via API");

        try
        {
            await _botService.RestartAsync(cancellationToken);
            return Accepted();
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Restart operation not supported");

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Restart operation is not supported",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Shuts down the bot gracefully.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Accepted response.</returns>
    [HttpPost("shutdown")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Shutdown(CancellationToken cancellationToken)
    {
        _logger.LogWarning("Bot shutdown requested via API");

        await _botService.ShutdownAsync(cancellationToken);

        return Accepted(new { Message = "Shutdown initiated" });
    }
}
