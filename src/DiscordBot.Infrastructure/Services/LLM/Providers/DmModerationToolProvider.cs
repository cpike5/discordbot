using System.Text.Json;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM.Implementations;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Providers;

/// <summary>
/// Tool provider for moderation lookup operations in DM context.
/// Provides tools for searching moderation cases and viewing user moderation history.
/// </summary>
public class DmModerationToolProvider : IDmToolProvider
{
    private readonly ILogger<DmModerationToolProvider> _logger;
    private readonly IModerationService _moderationService;
    private readonly IModNoteService _modNoteService;
    private readonly IWatchlistService _watchlistService;

    /// <inheritdoc />
    public string Name => "DmModeration";

    /// <inheritdoc />
    public string Description => "Look up moderation cases and user moderation history";

    public DmModerationToolProvider(
        ILogger<DmModerationToolProvider> logger,
        IModerationService moderationService,
        IModNoteService modNoteService,
        IWatchlistService watchlistService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _moderationService = moderationService ?? throw new ArgumentNullException(nameof(moderationService));
        _modNoteService = modNoteService ?? throw new ArgumentNullException(nameof(modNoteService));
        _watchlistService = watchlistService ?? throw new ArgumentNullException(nameof(watchlistService));
    }

    /// <inheritdoc />
    public IEnumerable<LlmToolDefinition> GetTools()
    {
        return DmModerationTools.GetAllTools();
    }

    /// <inheritdoc />
    public async Task<ToolExecutionResult> ExecuteToolAsync(
        string toolName,
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing DM moderation tool {ToolName}", toolName);

        try
        {
            return toolName.ToLowerInvariant() switch
            {
                DmModerationTools.GetModerationCases => await ExecuteGetModerationCasesAsync(input, context, cancellationToken),
                DmModerationTools.GetUserModHistory => await ExecuteGetUserModHistoryAsync(input, context, cancellationToken),
                _ => throw new NotSupportedException($"Tool '{toolName}' is not supported by this provider")
            };
        }
        catch (NotSupportedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing DM moderation tool {ToolName}", toolName);
            return ToolExecutionResult.CreateError($"Error executing tool: {ex.Message}");
        }
    }

    private async Task<ToolExecutionResult> ExecuteGetModerationCasesAsync(
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

    private async Task<ToolExecutionResult> ExecuteGetUserModHistoryAsync(
        JsonElement input,
        ToolContext context,
        CancellationToken cancellationToken)
    {
        if (!input.TryGetProperty("user_id", out var userIdEl))
        {
            return ToolExecutionResult.CreateError("Missing required parameter: user_id");
        }

        var userIdStr = userIdEl.GetString();
        if (string.IsNullOrEmpty(userIdStr) || !ulong.TryParse(userIdStr, out var userId))
        {
            return ToolExecutionResult.CreateError("Parameter user_id must be a valid Discord user ID.");
        }

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

        var gid64 = guildId.Value;

        // Fetch all three data sources in parallel
        var casesTask = _moderationService.GetUserCasesAsync(gid64, userId, 1, 50, cancellationToken);
        var notesTask = _modNoteService.GetNotesAsync(gid64, userId, cancellationToken);
        var watchlistTask = _watchlistService.GetEntryAsync(gid64, userId, cancellationToken);

        await Task.WhenAll(casesTask, notesTask, watchlistTask);

        var (cases, caseTotalCount) = await casesTask;
        var notes = await notesTask;
        var watchlistEntry = await watchlistTask;

        var caseResults = cases.Select(c => new
        {
            case_number = c.CaseNumber,
            type = c.Type.ToString(),
            moderator_user_id = c.ModeratorUserId.ToString(),
            moderator_username = c.ModeratorUsername,
            reason = c.Reason,
            duration = c.Duration?.ToString(),
            created_at = c.CreatedAt.ToString("o"),
            expires_at = c.ExpiresAt?.ToString("o")
        }).ToList();

        var noteResults = notes.Select(n => new
        {
            id = n.Id.ToString(),
            author_user_id = n.AuthorUserId.ToString(),
            author_username = n.AuthorUsername,
            content = n.Content,
            created_at = n.CreatedAt.ToString("o")
        }).ToList();

        object? watchlistResult = watchlistEntry is not null
            ? new
            {
                on_watchlist = true,
                reason = watchlistEntry.Reason,
                added_by_user_id = watchlistEntry.AddedByUserId.ToString(),
                added_by_username = watchlistEntry.AddedByUsername,
                added_at = watchlistEntry.AddedAt.ToString("o")
            }
            : new
            {
                on_watchlist = false,
                reason = (string?)null,
                added_by_user_id = (string?)null,
                added_by_username = (string?)null,
                added_at = (string?)null
            };

        return CreateJsonResult(new
        {
            user_id = userId.ToString(),
            guild_id = gid64.ToString(),
            cases = caseResults,
            total_cases = caseTotalCount,
            notes = noteResults,
            total_notes = noteResults.Count,
            watchlist_entry = watchlistResult
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
