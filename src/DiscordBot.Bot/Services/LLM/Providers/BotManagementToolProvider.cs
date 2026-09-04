using System.Diagnostics;
using System.Text.Json;
using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Infrastructure.Services.LLM.Implementations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.LLM.Providers;

/// <summary>
/// Tool provider for bot management operations in DM context.
/// Provides tools for listing guilds, switching active guild, health checks, and audit log search.
/// </summary>
public class BotManagementToolProvider : IDmToolProvider
{
    private readonly ILogger<BotManagementToolProvider> _logger;
    private readonly DiscordSocketClient _client;
    private readonly IAuditLogService _auditLogService;
    private readonly IMemoryCache _memoryCache;

    private static readonly TimeSpan ActiveGuildTtl = TimeSpan.FromHours(24);

    /// <inheritdoc />
    public string Name => "BotManagement";

    /// <inheritdoc />
    public string Description => "Manage guild context, check bot health, and search audit logs";

    public BotManagementToolProvider(
        ILogger<BotManagementToolProvider> logger,
        DiscordSocketClient client,
        IAuditLogService auditLogService,
        IMemoryCache memoryCache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
    }

    /// <inheritdoc />
    public IEnumerable<LlmToolDefinition> GetTools()
    {
        return BotManagementTools.GetAllTools();
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing bot management tool {ToolName}", toolName);

        try
        {
            return toolName.ToLowerInvariant() switch
            {
                BotManagementTools.ListGuilds => ExecuteListGuilds(),
                BotManagementTools.SetActiveGuild => ExecuteSetActiveGuild(input, context),
                BotManagementTools.GetBotHealth => ExecuteGetBotHealth(),
                BotManagementTools.SearchAuditLogs => await ExecuteSearchAuditLogsAsync(input, context, cancellationToken),
                _ => throw new NotSupportedException($"Tool '{toolName}' is not supported by this provider")
            };
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing bot management tool {ToolName}", toolName);
            return ToolExecutionResult.CreateError($"Error executing tool: {ex.Message}");
        }
    }

    private ToolExecutionResult ExecuteListGuilds()
    {
        var guilds = _client.Guilds
            .OrderBy(g => g.Name)
            .Select(g => new
            {
                id = g.Id.ToString(),
                name = g.Name,
                member_count = g.MemberCount
            })
            .ToList();

        return CreateJsonResult(new
        {
            guilds,
            total_count = guilds.Count
        });
    }

    private ToolExecutionResult ExecuteSetActiveGuild(JsonElement input, ToolContext context)
    {
        if (!input.TryGetProperty("guild", out var guildElement))
        {
            return ToolExecutionResult.CreateError("Missing required parameter: guild");
        }

        var guildInput = guildElement.GetString();
        if (string.IsNullOrWhiteSpace(guildInput))
        {
            return ToolExecutionResult.CreateError("Parameter guild cannot be empty");
        }

        SocketGuild? matchedGuild = null;

        // Try exact ID match first
        if (ulong.TryParse(guildInput, out var guildId))
        {
            matchedGuild = _client.GetGuild(guildId);
        }

        // Fuzzy name match if no ID match
        if (matchedGuild is null)
        {
            var candidates = _client.Guilds
                .Where(g => g.Name.Contains(guildInput, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (candidates.Count == 1)
            {
                matchedGuild = candidates[0];
            }
            else if (candidates.Count > 1)
            {
                var options = candidates
                    .Select(g => new { id = g.Id.ToString(), name = g.Name })
                    .ToList();

                return CreateJsonResult(new
                {
                    error = true,
                    message = $"Multiple guilds match '{guildInput}'. Please be more specific or use the guild ID.",
                    matches = options
                });
            }
        }

        if (matchedGuild is null)
        {
            return ToolExecutionResult.CreateError($"No guild found matching '{guildInput}'. Use list_guilds to see available guilds.");
        }

        // Write to cache with 24h TTL
        var cacheKey = DmAssistantContextFactory.ActiveGuildCacheKeyPrefix + context.UserId;
        _memoryCache.Set(cacheKey, (ulong?)matchedGuild.Id, ActiveGuildTtl);

        _logger.LogInformation("User {UserId} set active guild to {GuildId} ({GuildName})",
            context.UserId, matchedGuild.Id, matchedGuild.Name);

        return CreateJsonResult(new
        {
            guild_id = matchedGuild.Id.ToString(),
            guild_name = matchedGuild.Name,
            member_count = matchedGuild.MemberCount,
            message = $"Active guild set to {matchedGuild.Name}. Subsequent commands will use this guild context."
        });
    }

    private ToolExecutionResult ExecuteGetBotHealth()
    {
        var process = Process.GetCurrentProcess();
        var uptime = DateTime.UtcNow - process.StartTime.ToUniversalTime();

        return CreateJsonResult(new
        {
            status = _client.ConnectionState.ToString(),
            uptime_hours = Math.Round(uptime.TotalHours, 2),
            uptime_formatted = $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m",
            memory_mb = Math.Round(process.WorkingSet64 / (1024.0 * 1024.0), 1),
            websocket_latency_ms = _client.Latency,
            guild_count = _client.Guilds.Count,
            connection_state = _client.ConnectionState.ToString()
        });
    }

    private async Task<ToolExecutionResult> ExecuteSearchAuditLogsAsync(
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

        var query = new AuditLogQueryDto
        {
            GuildId = guildId
        };

        if (input.TryGetProperty("category", out var catEl))
        {
            var catStr = catEl.GetString();
            if (!string.IsNullOrEmpty(catStr) && Enum.TryParse<AuditLogCategory>(catStr, true, out var cat))
                query.Category = cat;
        }

        if (input.TryGetProperty("action", out var actEl))
        {
            var actStr = actEl.GetString();
            if (!string.IsNullOrEmpty(actStr) && Enum.TryParse<AuditLogAction>(actStr, true, out var act))
                query.Action = act;
        }

        if (input.TryGetProperty("actor_id", out var actorEl))
        {
            var actorStr = actorEl.GetString();
            if (!string.IsNullOrEmpty(actorStr))
                query.ActorId = actorStr;
        }

        if (input.TryGetProperty("search_term", out var searchEl))
        {
            var searchStr = searchEl.GetString();
            if (!string.IsNullOrEmpty(searchStr))
                query.SearchTerm = searchStr;
        }

        if (input.TryGetProperty("start_date", out var startEl))
        {
            var startStr = startEl.GetString();
            if (!string.IsNullOrEmpty(startStr) && DateTime.TryParse(startStr, out var startDate))
                query.StartDate = DateTime.SpecifyKind(startDate, DateTimeKind.Utc);
        }

        if (input.TryGetProperty("end_date", out var endEl))
        {
            var endStr = endEl.GetString();
            if (!string.IsNullOrEmpty(endStr) && DateTime.TryParse(endStr, out var endDate))
                query.EndDate = DateTime.SpecifyKind(endDate, DateTimeKind.Utc);
        }

        if (input.TryGetProperty("page", out var pageEl))
            query.Page = Math.Max(1, pageEl.GetInt32());

        if (input.TryGetProperty("page_size", out var pageSizeEl))
            query.PageSize = Math.Clamp(pageSizeEl.GetInt32(), 1, 25);
        else
            query.PageSize = 10;

        var (items, totalCount) = await _auditLogService.GetLogsAsync(query, cancellationToken);

        var results = items.Select(log => new
        {
            id = log.Id,
            timestamp = log.Timestamp.ToString("o"),
            category = log.CategoryName,
            action = log.ActionName,
            actor_id = log.ActorId,
            actor_name = log.ActorDisplayName,
            target_type = log.TargetType,
            target_id = log.TargetId,
            guild_id = log.GuildId?.ToString(),
            details = log.Details
        }).ToList();

        return CreateJsonResult(new
        {
            results,
            total_count = totalCount,
            page = query.Page,
            page_size = query.PageSize,
            total_pages = (int)Math.Ceiling((double)totalCount / query.PageSize)
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
