using System.Text.Json;
using System.Diagnostics;
using Discord.WebSocket;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using cpike.Agent;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Bot.Services.LLM.Tools;

/// <summary>
/// <c>search_audit_logs</c> — searches a guild's audit logs.
/// </summary>
public sealed class SearchAuditLogsTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "search_audit_logs",
        "Searches the bot's audit logs for a guild. Supports filtering by category, action type, actor, date range, and free-text search. Requires an active guild context or explicit guild_id parameter.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "guild_id": {
                        "type": "string",
                        "description": "The guild ID to search audit logs for. Uses the active guild if not specified."
                    },
                    "category": {
                        "type": "string",
                        "description": "Filter by audit log category: User, Guild, Configuration, Security, Command, Message, System."
                    },
                    "action": {
                        "type": "string",
                        "description": "Filter by action: Created, Updated, Deleted, Login, Logout, PermissionChanged, SettingChanged, CommandExecuted, MessageDeleted, MessageEdited, UserBanned, UserUnbanned, UserKicked, RoleAssigned, RoleRemoved, BotStarted, BotStopped, BotConnected, BotDisconnected."
                    },
                    "actor_id": {
                        "type": "string",
                        "description": "Filter by the actor (user) who performed the action."
                    },
                    "search_term": {
                        "type": "string",
                        "description": "Free-text search across audit log details."
                    },
                    "start_date": {
                        "type": "string",
                        "description": "Start date filter in ISO 8601 format (e.g., '2024-01-01')."
                    },
                    "end_date": {
                        "type": "string",
                        "description": "End date filter in ISO 8601 format."
                    },
                    "page": {
                        "type": "integer",
                        "description": "Page number (1-based). Default: 1.",
                        "default": 1,
                        "minimum": 1
                    },
                    "page_size": {
                        "type": "integer",
                        "description": "Results per page. Default: 10, maximum: 25.",
                        "default": 10,
                        "minimum": 1,
                        "maximum": 25
                    }
                },
                "required": []
            }
            """).RootElement.Clone());

    private readonly IAuditLogService _auditLogService;
    private readonly DiscordRunContext _run;
    private readonly ILogger<SearchAuditLogsTool> _logger;

    /// <summary>Creates the tool.</summary>
    public SearchAuditLogsTool(
        IAuditLogService auditLogService,
        DiscordRunContext run,
        ILogger<SearchAuditLogsTool> logger)
    {
        _auditLogService = auditLogService ?? throw new ArgumentNullException(nameof(auditLogService));
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
            _logger.LogError(ex, "Error executing tool {ToolName}", "search_audit_logs");
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

    private static string CreateJsonResult(object data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
}
