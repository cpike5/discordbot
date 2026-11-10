using System.Text.Json;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Interfaces;
using cpike.Agent;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.LLM.Tools;

/// <summary>
/// <c>get_rat_watch_leaderboard</c> — the guild's RatWatch leaderboard.
/// </summary>
public sealed class GetRatWatchLeaderboardTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "get_rat_watch_leaderboard",
        "Gets the RatWatch leaderboard showing users ranked by their rat incident count in the current guild. Use this when users ask about top rats, leaderboard, rankings, or who has the most rat incidents.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "limit": {
                        "type": "integer",
                        "description": "Maximum number of results to return. Default is 10, maximum is 50.",
                        "default": 10,
                        "minimum": 1,
                        "maximum": 50
                    }
                },
                "required": []
            }
            """).RootElement.Clone());

    private readonly IRatWatchService _ratWatchService;
    private readonly DiscordRunContext _run;
    private readonly ILogger<GetRatWatchLeaderboardTool> _logger;

    /// <summary>Creates the tool.</summary>
    public GetRatWatchLeaderboardTool(
        IRatWatchService ratWatchService,
        DiscordRunContext run,
        ILogger<GetRatWatchLeaderboardTool> logger)
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
            _logger.LogError(ex, "Error executing tool {ToolName}", "get_rat_watch_leaderboard");
            return ToolResults.Error($"Error executing tool: {ex.Message}");
        }
    }

    private async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken)
    {
        if (_run.GuildId == 0)
        {
            return ToolResults.Error("No guild context available. This tool requires a guild context.");
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

        _logger.LogDebug("Getting RatWatch leaderboard for guild {GuildId} with limit {Limit}", _run.GuildId, limit);

        // Check if public leaderboard is enabled
        var settings = await _ratWatchService.GetGuildSettingsAsync(_run.GuildId, cancellationToken);
        if (!settings.PublicLeaderboardEnabled)
        {
            return ToolResults.Error("The RatWatch leaderboard is not publicly accessible for this guild. Please contact an administrator to enable public access.");
        }

        var leaderboard = await _ratWatchService.GetLeaderboardAsync(_run.GuildId, limit, cancellationToken);

        _logger.LogDebug("Successfully retrieved leaderboard with {Count} entries", leaderboard.Count);

        return CreateJsonResult(new
        {
            guild_id = _run.GuildId.ToString(),
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

    private static string CreateJsonResult(object data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
}
