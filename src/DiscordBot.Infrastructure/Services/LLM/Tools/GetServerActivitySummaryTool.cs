using System.Text.Json;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Interfaces;
using cpike.Agent;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>get_server_activity_summary</c> — server activity summary for a guild.
/// </summary>
public sealed class GetServerActivitySummaryTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "get_server_activity_summary",
        "Gets a summary of server activity including member counts, message volume, active members, and growth metrics for a specified time period. Requires an active guild context.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "days": {
                        "type": "integer",
                        "description": "Number of days to look back for the activity summary. Default is 7.",
                        "default": 7,
                        "minimum": 1,
                        "maximum": 90
                    },
                    "guild_id": {
                        "type": "string",
                        "description": "Discord guild/server ID. If omitted, uses the active guild set via set_active_guild."
                    }
                },
                "required": []
            }
            """).RootElement.Clone());

    private readonly IServerAnalyticsService _serverAnalyticsService;
    private readonly DiscordRunContext _run;
    private readonly ILogger<GetServerActivitySummaryTool> _logger;

    /// <summary>Creates the tool.</summary>
    public GetServerActivitySummaryTool(
        IServerAnalyticsService serverAnalyticsService,
        DiscordRunContext run,
        ILogger<GetServerActivitySummaryTool> logger)
    {
        _serverAnalyticsService = serverAnalyticsService ?? throw new ArgumentNullException(nameof(serverAnalyticsService));
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
            _logger.LogError(ex, "Error executing tool {ToolName}", "get_server_activity_summary");
            return ToolResults.Error($"Error executing tool: {ex.Message}");
        }
    }

    private async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken)
    {
        ulong? guildId = null;
        if (input.TryGetProperty("guild_id", out var gid))
        {
            var gidStr = gid.GetString();
            if (!string.IsNullOrEmpty(gidStr) && ulong.TryParse(gidStr, out var parsed))
                guildId = parsed;
        }
        guildId ??= _run.ActiveGuildId;

        if (guildId is null or 0)
            return ToolResults.Error("No guild context. Use set_active_guild first or provide guild_id.");

        var days = 7;
        if (input.TryGetProperty("days", out var daysElement))
        {
            days = Math.Clamp(daysElement.GetInt32(), 1, 90);
        }

        _logger.LogDebug("Getting server activity summary for guild {GuildId}, last {Days} days", guildId, days);

        var end = DateTime.UtcNow;
        var start = end.AddDays(-days);

        var summary = await _serverAnalyticsService.GetSummaryAsync(guildId.Value, start, end, cancellationToken);

        return CreateJsonResult(new
        {
            guild_id = guildId.Value.ToString(),
            period_days = days,
            total_members = summary.TotalMembers,
            online_members = summary.OnlineMembers,
            active_members_24h = summary.ActiveMembers24h,
            active_members_7d = summary.ActiveMembers7d,
            active_members_30d = summary.ActiveMembers30d,
            messages_24h = summary.Messages24h,
            messages_7d = summary.Messages7d,
            member_growth_7d = summary.MemberGrowth7d,
            member_growth_percent = summary.MemberGrowthPercent,
            active_channels = summary.ActiveChannels
        });
    }

    private static string CreateJsonResult(object data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
}
