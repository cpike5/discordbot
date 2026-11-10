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
/// <c>get_user_mod_history</c> — a user's cases, notes and watchlist status in one view.
/// </summary>
public sealed class GetUserModHistoryTool : IAgentTool
{
    private static readonly AgentToolDefinition ToolDefinition = AgentTools.Define(
        "get_user_mod_history",
        "Gets a comprehensive moderation history for a specific user in a guild. Returns all moderation cases, moderator notes, and watchlist status in a unified view. Requires an active guild context or explicit guild_id.",
        JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "user_id": {
                        "type": "string",
                        "description": "The Discord user ID to look up moderation history for."
                    },
                    "guild_id": {
                        "type": "string",
                        "description": "The guild ID to search in. Uses the active guild if not specified."
                    }
                },
                "required": ["user_id"]
            }
            """).RootElement.Clone());

    private readonly IModerationService _moderationService;
    private readonly IModNoteService _modNoteService;
    private readonly IWatchlistService _watchlistService;
    private readonly DiscordRunContext _run;
    private readonly ILogger<GetUserModHistoryTool> _logger;

    /// <summary>Creates the tool.</summary>
    public GetUserModHistoryTool(
        IModerationService moderationService,
        IModNoteService modNoteService,
        IWatchlistService watchlistService,
        DiscordRunContext run,
        ILogger<GetUserModHistoryTool> logger)
    {
        _moderationService = moderationService ?? throw new ArgumentNullException(nameof(moderationService));
        _modNoteService = modNoteService ?? throw new ArgumentNullException(nameof(modNoteService));
        _watchlistService = watchlistService ?? throw new ArgumentNullException(nameof(watchlistService));
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
            _logger.LogError(ex, "Error executing tool {ToolName}", "get_user_mod_history");
            return ToolResults.Error($"Error executing tool: {ex.Message}");
        }
    }

    private async Task<string> ExecuteAsync(JsonElement input, CancellationToken cancellationToken)
    {
        if (!input.TryGetProperty("user_id", out var userIdEl))
        {
            return ToolResults.Error("Missing required parameter: user_id");
        }

        var userIdStr = userIdEl.GetString();
        if (string.IsNullOrEmpty(userIdStr) || !ulong.TryParse(userIdStr, out var userId))
        {
            return ToolResults.Error("Parameter user_id must be a valid Discord user ID.");
        }

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

    private static string CreateJsonResult(object data) =>
        JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
}
