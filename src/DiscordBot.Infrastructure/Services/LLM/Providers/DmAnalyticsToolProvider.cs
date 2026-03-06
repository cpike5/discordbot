using System.Text.Json;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM.Implementations;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Providers;

/// <summary>
/// Tool provider for server and command analytics in DM assistant context.
/// </summary>
public class DmAnalyticsToolProvider : IDmToolProvider
{
    private readonly ILogger<DmAnalyticsToolProvider> _logger;
    private readonly IServerAnalyticsService _serverAnalyticsService;
    private readonly ICommandAnalyticsService _commandAnalyticsService;

    /// <inheritdoc />
    public string Name => "DmAnalytics";

    /// <inheritdoc />
    public string Description => "Access server activity summaries and command usage analytics";

    public DmAnalyticsToolProvider(
        ILogger<DmAnalyticsToolProvider> logger,
        IServerAnalyticsService serverAnalyticsService,
        ICommandAnalyticsService commandAnalyticsService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serverAnalyticsService = serverAnalyticsService ?? throw new ArgumentNullException(nameof(serverAnalyticsService));
        _commandAnalyticsService = commandAnalyticsService ?? throw new ArgumentNullException(nameof(commandAnalyticsService));
    }

    /// <inheritdoc />
    public IEnumerable<LlmToolDefinition> GetTools()
    {
        return DmAnalyticsTools.GetAllTools();
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing DM analytics tool {ToolName}", toolName);

        try
        {
            return toolName.ToLowerInvariant() switch
            {
                DmAnalyticsTools.GetServerActivitySummary => await ExecuteGetServerActivitySummaryAsync(input, context, cancellationToken),
                DmAnalyticsTools.GetCommandAnalytics => await ExecuteGetCommandAnalyticsAsync(input, context, cancellationToken),
                _ => throw new NotSupportedException($"Tool '{toolName}' is not supported by this provider")
            };
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing DM analytics tool {ToolName}", toolName);
            return ToolExecutionResult.CreateError($"Error executing tool: {ex.Message}");
        }
    }

    private async Task<ToolExecutionResult> ExecuteGetServerActivitySummaryAsync(
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        ulong? guildId = null;
        if (input.TryGetProperty("guild_id", out var gid))
        {
            var gidStr = gid.GetString();
            if (!string.IsNullOrEmpty(gidStr) && ulong.TryParse(gidStr, out var parsed))
                guildId = parsed;
        }
        guildId ??= context.ActiveGuildId;

        if (guildId is null or 0)
            return ToolExecutionResult.CreateError("No guild context. Use set_active_guild first or provide guild_id.");

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

    private async Task<ToolExecutionResult> ExecuteGetCommandAnalyticsAsync(
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        ulong? guildId = null;
        if (input.TryGetProperty("guild_id", out var gid))
        {
            var gidStr = gid.GetString();
            if (!string.IsNullOrEmpty(gidStr) && ulong.TryParse(gidStr, out var parsed))
                guildId = parsed;
        }
        guildId ??= context.ActiveGuildId;

        if (guildId is null or 0)
            return ToolExecutionResult.CreateError("No guild context. Use set_active_guild first or provide guild_id.");

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
