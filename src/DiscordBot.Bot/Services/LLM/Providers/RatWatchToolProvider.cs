using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM.Implementations;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.LLM.Providers;

/// <summary>
/// Tool provider for RatWatch accountability system information access.
/// Provides tools for getting leaderboard, user statistics, and guild summary.
/// </summary>
public class RatWatchToolProvider : IToolProvider
{
    private readonly ILogger<RatWatchToolProvider> _logger;
    private readonly IRatWatchService _ratWatchService;

    /// <inheritdoc />
    public string Name => "RatWatch";

    /// <inheritdoc />
    public string Description => "Get information about RatWatch accountability tracking system";

    /// <summary>
    /// Initializes a new instance of the RatWatchToolProvider.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="ratWatchService">RatWatch service for data access.</param>
    public RatWatchToolProvider(
        ILogger<RatWatchToolProvider> logger,
        IRatWatchService ratWatchService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ratWatchService = ratWatchService ?? throw new ArgumentNullException(nameof(ratWatchService));
    }

    /// <inheritdoc />
    public IEnumerable<LlmToolDefinition> GetTools()
    {
        return RatWatchTools.GetAllTools();
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing RatWatch tool {ToolName}", toolName);

        try
        {
            return toolName.ToLowerInvariant() switch
            {
                RatWatchTools.GetRatWatchLeaderboard => await ExecuteGetLeaderboardAsync(input, context, cancellationToken),
                RatWatchTools.GetRatWatchUserStats => await ExecuteGetUserStatsAsync(input, context, cancellationToken),
                RatWatchTools.GetRatWatchSummary => await ExecuteGetSummaryAsync(context, cancellationToken),
                _ => throw new NotSupportedException($"Tool '{toolName}' is not supported by this provider")
            };
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing RatWatch tool {ToolName}", toolName);
            return ToolExecutionResult.CreateError($"Error executing tool: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the get_rat_watch_leaderboard tool.
    /// </summary>
    private async Task<ToolExecutionResult> ExecuteGetLeaderboardAsync(
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        if (context.GuildId == 0)
        {
            return ToolExecutionResult.CreateError("No guild context available. This tool requires a guild context.");
        }

        // Parse limit from input (default 10, clamp to 1-50)
        var limit = 10;
        if (input.TryGetProperty("limit", out var limitElement))
        {
            if (limitElement.TryGetInt32(out var parsedLimit))
            {
                limit = Math.Clamp(parsedLimit, 1, 50);
            }
        }

        _logger.LogDebug("Getting RatWatch leaderboard for guild {GuildId} with limit {Limit}", context.GuildId, limit);

        // Check if public leaderboard is enabled
        var settings = await _ratWatchService.GetGuildSettingsAsync(context.GuildId, cancellationToken);
        if (!settings.PublicLeaderboardEnabled)
        {
            return ToolExecutionResult.CreateError("The RatWatch leaderboard is not publicly accessible for this guild. Please contact an administrator to enable public access.");
        }

        var leaderboard = await _ratWatchService.GetLeaderboardAsync(context.GuildId, limit, cancellationToken);

        _logger.LogDebug("Successfully retrieved leaderboard with {Count} entries", leaderboard.Count);

        return CreateJsonResult(new
        {
            guild_id = context.GuildId.ToString(),
            limit,
            entries = leaderboard.Select(e => new
            {
                rank = e.Rank,
                user_id = e.UserId.ToString(),
                username = e.Username,
                guilty_count = e.GuiltyCount
            })
        });
    }

    /// <summary>
    /// Executes the get_rat_watch_user_stats tool.
    /// </summary>
    private async Task<ToolExecutionResult> ExecuteGetUserStatsAsync(
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        if (context.GuildId == 0)
        {
            return ToolExecutionResult.CreateError("No guild context available. This tool requires a guild context.");
        }

        // Get user ID from input or default to requesting user
        var userId = context.UserId;
        if (input.TryGetProperty("user_id", out var userIdElement))
        {
            var userIdString = userIdElement.GetString();
            if (!string.IsNullOrWhiteSpace(userIdString) && ulong.TryParse(userIdString, out var parsedUserId))
            {
                userId = parsedUserId;
            }
        }

        _logger.LogDebug("Getting RatWatch stats for user {UserId} in guild {GuildId}", userId, context.GuildId);

        var stats = await _ratWatchService.GetUserStatsAsync(context.GuildId, userId, cancellationToken);

        _logger.LogDebug("Successfully retrieved stats for user {UserId} with {GuiltyCount} guilty verdicts", userId, stats.TotalGuiltyCount);

        return CreateJsonResult(new
        {
            guild_id = context.GuildId.ToString(),
            user_id = stats.UserId.ToString(),
            username = stats.Username,
            total_guilty_count = stats.TotalGuiltyCount,
            recent_records = stats.RecentRecords.Select(r => new
            {
                recorded_at = r.RecordedAt.ToString("o"),
                guilty_votes = r.GuiltyVotes,
                not_guilty_votes = r.NotGuiltyVotes,
                original_message_link = r.OriginalMessageLink
            })
        });
    }

    /// <summary>
    /// Executes the get_rat_watch_summary tool.
    /// </summary>
    private async Task<ToolExecutionResult> ExecuteGetSummaryAsync(
        ToolContext context,
        CancellationToken cancellationToken)
    {
        if (context.GuildId == 0)
        {
            return ToolExecutionResult.CreateError("No guild context available. This tool requires a guild context.");
        }

        _logger.LogDebug("Getting RatWatch summary for guild {GuildId}", context.GuildId);

        // Fetch first page of 100 items to compile summary
        var (items, totalCount) = await _ratWatchService.GetByGuildAsync(context.GuildId, 1, 100, cancellationToken);

        // Compute counts by status
        var itemsList = items.ToList();
        var guiltyCount = itemsList.Count(w => w.Status == RatWatchStatus.Guilty);
        var notGuiltyCount = itemsList.Count(w => w.Status == RatWatchStatus.NotGuilty);
        var clearedCount = itemsList.Count(w => w.Status == RatWatchStatus.ClearedEarly);
        var cancelledCount = itemsList.Count(w => w.Status == RatWatchStatus.Cancelled);
        var pendingCount = itemsList.Count(w => w.Status == RatWatchStatus.Pending);
        var votingCount = itemsList.Count(w => w.Status == RatWatchStatus.Voting);
        var expiredCount = itemsList.Count(w => w.Status == RatWatchStatus.Expired);

        // Get recent activity (most recent 10 items from the list)
        var recentActivity = itemsList
            .OrderByDescending(w => w.CreatedAt)
            .Take(10)
            .Select(w => new
            {
                id = w.Id,
                accused_user_id = w.AccusedUserId.ToString(),
                accused_username = w.AccusedUsername,
                initiator_user_id = w.InitiatorUserId.ToString(),
                initiator_username = w.InitiatorUsername,
                status = w.Status.ToString(),
                scheduled_at = w.ScheduledAt.ToString("o"),
                created_at = w.CreatedAt.ToString("o"),
                guilty_votes = w.GuiltyVotes,
                not_guilty_votes = w.NotGuiltyVotes
            });

        _logger.LogDebug("Successfully compiled summary for guild {GuildId} with {TotalCount} total watches", context.GuildId, totalCount);

        return CreateJsonResult(new
        {
            guild_id = context.GuildId.ToString(),
            total_watches = totalCount,
            status_breakdown = new
            {
                guilty = guiltyCount,
                not_guilty = notGuiltyCount,
                cleared_early = clearedCount,
                cancelled = cancelledCount,
                pending = pendingCount,
                voting = votingCount,
                expired = expiredCount
            },
            recent_activity = recentActivity
        });
    }

    /// <summary>
    /// Creates a JSON result from an object.
    /// </summary>
    private static ToolExecutionResult CreateJsonResult(object data)
    {
        var jsonString = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
        var jsonElement = JsonDocument.Parse(jsonString).RootElement.Clone();
        return ToolExecutionResult.CreateSuccess(jsonElement);
    }
}
