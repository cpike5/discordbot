using System.Text.Json;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Interfaces;
using cpike.Agent;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>get_command_analytics</c> — command usage and performance analytics for a guild.
/// </summary>
public sealed class GetCommandAnalyticsTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "get_command_analytics",
        "Gets command usage analytics including top commands by usage count and response time performance metrics. Requires an active guild context.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "days": {
                        "type": "integer",
                        "description": "Number of days to look back for analytics. Default is 7.",
                        "default": 7,
                        "minimum": 1,
                        "maximum": 90
                    },
                    "limit": {
                        "type": "integer",
                        "description": "Maximum number of top commands to return. Default is 10.",
                        "default": 10,
                        "minimum": 1,
                        "maximum": 50
                    },
                    "guild_id": {
                        "type": "string",
                        "description": "Discord guild/server ID. If omitted, uses the active guild set via set_active_guild."
                    }
                },
                "required": []
            }
            """).RootElement.Clone());

    private readonly ICommandAnalyticsService _commandAnalyticsService;
    private readonly DiscordRunContext _run;
    private readonly ILogger<GetCommandAnalyticsTool> _logger;

    /// <summary>Creates the tool.</summary>
    public GetCommandAnalyticsTool(
        ICommandAnalyticsService commandAnalyticsService,
        DiscordRunContext run,
        ILogger<GetCommandAnalyticsTool> logger)
    {
        _commandAnalyticsService = commandAnalyticsService ?? throw new ArgumentNullException(nameof(commandAnalyticsService));
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
            _logger.LogError(ex, "Error executing tool {ToolName}", "get_command_analytics");
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

        var limit = 10;
        if (input.TryGetProperty("limit", out var limitElement))
        {
            limit = Math.Clamp(limitElement.GetInt32(), 1, 50);
        }

        _logger.LogDebug("Getting command analytics for guild {GuildId}, last {Days} days, limit {Limit}", guildId, days, limit);

        var since = DateTime.UtcNow.AddDays(-days);

        var topCommands = await _commandAnalyticsService.GetTopCommandsAsync(since, guildId, limit, cancellationToken);
        var performance = await _commandAnalyticsService.GetCommandPerformanceAsync(since, guildId, limit, cancellationToken);

        return CreateJsonResult(new
        {
            guild_id = guildId.Value.ToString(),
            period_days = days,
            top_commands = topCommands.Select(kvp => new
            {
                command = kvp.Key,
                usage_count = kvp.Value
            }).ToList(),
            performance = performance.Select(p => new
            {
                command = p.CommandName,
                avg_response_time_ms = p.AvgResponseTimeMs,
                min_response_time_ms = p.MinResponseTimeMs,
                max_response_time_ms = p.MaxResponseTimeMs,
                execution_count = p.ExecutionCount
            }).ToList()
        });
    }

    private static string CreateJsonResult(object data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
}
