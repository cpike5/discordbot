using System.Text.Json;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Interfaces;
using cpike.Agent;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Tools;

/// <summary>
/// <c>get_moderation_cases</c> — searches a guild's moderation cases.
/// </summary>
public sealed class GetModerationCasesTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "get_moderation_cases",
        "Searches moderation cases for a guild with optional filters. Supports filtering by case type (Warn, Kick, Ban, Mute, Note, Unban), target user, moderator, and date range. Requires an active guild context or explicit guild_id.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "guild_id": {
                        "type": "string",
                        "description": "The guild ID to search cases for. Uses the active guild if not specified."
                    },
                    "type": {
                        "type": "string",
                        "description": "Filter by case type: Warn, Kick, Ban, Mute, Note, Unban."
                    },
                    "target_user_id": {
                        "type": "string",
                        "description": "Filter by the target user's Discord ID."
                    },
                    "moderator_id": {
                        "type": "string",
                        "description": "Filter by the moderator's Discord ID."
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

    private readonly IModerationService _moderationService;
    private readonly DiscordRunContext _run;
    private readonly ILogger<GetModerationCasesTool> _logger;

    /// <summary>Creates the tool.</summary>
    public GetModerationCasesTool(
        IModerationService moderationService,
        DiscordRunContext run,
        ILogger<GetModerationCasesTool> logger)
    {
        _moderationService = moderationService ?? throw new ArgumentNullException(nameof(moderationService));
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
            _logger.LogError(ex, "Error executing tool {ToolName}", "get_moderation_cases");
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

        var query = new ModerationCaseQueryDto
        {
            GuildId = guildId.Value
        };

        if (input.TryGetProperty("type", out var typeEl))
        {
            var typeStr = typeEl.GetString();
            if (!string.IsNullOrEmpty(typeStr) && Enum.TryParse<CaseType>(typeStr, true, out var caseType))
                query.Type = caseType;
        }

        if (input.TryGetProperty("target_user_id", out var targetEl))
        {
            var targetStr = targetEl.GetString();
            if (!string.IsNullOrEmpty(targetStr) && ulong.TryParse(targetStr, out var targetId))
                query.TargetUserId = targetId;
        }

        if (input.TryGetProperty("moderator_id", out var modEl))
        {
            var modStr = modEl.GetString();
            if (!string.IsNullOrEmpty(modStr) && ulong.TryParse(modStr, out var modId))
                query.ModeratorUserId = modId;
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

        var (items, totalCount) = await _moderationService.GetCasesAsync(query, cancellationToken);

        var results = items.Select(c => new
        {
            case_number = c.CaseNumber,
            type = c.Type.ToString(),
            target_user_id = c.TargetUserId.ToString(),
            target_username = c.TargetUsername,
            moderator_user_id = c.ModeratorUserId.ToString(),
            moderator_username = c.ModeratorUsername,
            reason = c.Reason,
            duration = c.Duration?.ToString(),
            created_at = c.CreatedAt.ToString("o"),
            expires_at = c.ExpiresAt?.ToString("o")
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
