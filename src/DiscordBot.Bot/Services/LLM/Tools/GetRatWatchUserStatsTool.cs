using System.Text.Json;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Interfaces;
using cpike.Agent;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.LLM.Tools;

/// <summary>
/// <c>get_rat_watch_user_stats</c> — RatWatch statistics for one user.
/// </summary>
public sealed class GetRatWatchUserStatsTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "get_rat_watch_user_stats",
        "Gets RatWatch statistics for a specific user including their rat incident count, rank, recent incidents, and history in the current guild. Use this when users ask about their own rat stats or another user's rat record.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "user_id": {
                        "type": "string",
                        "description": "The Discord user ID (snowflake) to get RatWatch statistics for. If not provided, returns stats for the requesting user."
                    }
                },
                "required": []
            }
            """).RootElement.Clone());

    private readonly IRatWatchService _ratWatchService;
    private readonly DiscordRunContext _run;
    private readonly ILogger<GetRatWatchUserStatsTool> _logger;

    /// <summary>Creates the tool.</summary>
    public GetRatWatchUserStatsTool(
        IRatWatchService ratWatchService,
        DiscordRunContext run,
        ILogger<GetRatWatchUserStatsTool> logger)
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
            _logger.LogError(ex, "Error executing tool {ToolName}", "get_rat_watch_user_stats");
            return ToolResults.Error($"Error executing tool: {ex.Message}");
        }
    }

    private async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken)
    {
        if (_run.GuildId == 0)
        {
            return ToolResults.Error("No guild context available. This tool requires a guild context.");
        }

        // Get user ID from input or default to requesting user
        var userId = _run.UserId;
        if (input.TryGetProperty("user_id", out var userIdElement))
        {
            var userIdString = userIdElement.GetString();
            if (!string.IsNullOrWhiteSpace(userIdString) && ulong.TryParse(userIdString, out var parsedUserId))
            {
                userId = parsedUserId;
            }
        }

        _logger.LogDebug("Getting RatWatch stats for user {UserId} in guild {GuildId}", userId, _run.GuildId);

        var stats = await _ratWatchService.GetUserStatsAsync(_run.GuildId, userId, cancellationToken);

        _logger.LogDebug("Successfully retrieved stats for user {UserId} with {GuiltyCount} guilty verdicts", userId, stats.TotalGuiltyCount);

        return CreateJsonResult(new
        {
            guild_id = _run.GuildId.ToString(),
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

    private static string CreateJsonResult(object data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
}
