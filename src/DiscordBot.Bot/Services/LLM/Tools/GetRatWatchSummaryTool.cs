using System.Text.Json;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Interfaces;
using cpike.Agent;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.LLM.Tools;

/// <summary>
/// <c>get_rat_watch_summary</c> — an overview of the guild's RatWatch activity.
/// </summary>
public sealed class GetRatWatchSummaryTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "get_rat_watch_summary",
        "Gets an overview summary of RatWatch activity in the current guild including total incidents, active watchers, recent activity, and trends. Use this when users ask about overall rat watch status or guild-wide statistics.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {},
                "required": []
            }
            """).RootElement.Clone());

    private readonly IRatWatchService _ratWatchService;
    private readonly DiscordRunContext _run;
    private readonly ILogger<GetRatWatchSummaryTool> _logger;

    /// <summary>Creates the tool.</summary>
    public GetRatWatchSummaryTool(
        IRatWatchService ratWatchService,
        DiscordRunContext run,
        ILogger<GetRatWatchSummaryTool> logger)
    {
        _ratWatchService = ratWatchService ?? throw new ArgumentNullException(nameof(ratWatchService));
        _run = run ?? throw new ArgumentNullException(nameof(run));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public AgentToolDefinition Definition => ToolDefinition;

    /// <inheritdoc />
    public async Task<string> InvokeAsync(
        IReadOnlyDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        var input = ToolInput.ToElement(arguments);

        try
        {
            return await ExecuteAsync(input, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", "get_rat_watch_summary");
            return ToolResults.Error($"Error executing tool: {ex.Message}");
        }
    }

    private async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken)
    {
        if (_run.GuildId == 0)
        {
            return ToolResults.Error("No guild context available. This tool requires a guild context.");
        }

        _logger.LogDebug("Getting RatWatch summary for guild {GuildId}", _run.GuildId);

        // Fetch first page of 100 items to compile summary
        var (items, totalCount) = await _ratWatchService.GetByGuildAsync(_run.GuildId, 1, 100, cancellationToken);

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

        _logger.LogDebug("Successfully compiled summary for guild {GuildId} with {TotalCount} total watches", _run.GuildId, totalCount);

        return CreateJsonResult(new
        {
            guild_id = _run.GuildId.ToString(),
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

    private static string CreateJsonResult(object data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
}
