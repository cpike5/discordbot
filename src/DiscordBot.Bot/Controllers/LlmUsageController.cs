using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Admin read endpoints for the <see cref="LlmUsageRecord"/> ledger: a portal-wide (or per-guild)
/// usage summary with breakdowns by user/model/mode/day, and a paged raw-record listing for the
/// per-user drill-down on <c>/Admin/LlmUsage</c>. All aggregation is delegated to
/// <see cref="ILlmUsageRepository"/> - this controller only validates the query range, resolves
/// Discord user display names for the rows that carry a user ID, and maps to DTOs.
/// </summary>
[Route("api/admin/llm-usage")]
[Authorize(Policy = "RequireAdmin")]
public class LlmUsageController : ApiControllerBase
{
    /// <summary>Default range when no <c>from</c>/<c>to</c> is supplied.</summary>
    private const int DefaultRangeDays = 30;

    /// <summary>Hard cap on <c>pageSize</c> for the records endpoint.</summary>
    private const int MaxPageSize = 200;

    private const int DefaultPageSize = 50;

    /// <summary>How many top-cost users the summary's <c>byUser</c> breakdown returns.</summary>
    private const int SummaryUserTake = 25;

    private readonly ILlmUsageRepository _usageRepository;
    private readonly IDiscordUserResolver _userResolver;
    private readonly ILogger<LlmUsageController> _logger;

    public LlmUsageController(
        ILlmUsageRepository usageRepository,
        IDiscordUserResolver userResolver,
        ILogger<LlmUsageController> logger)
    {
        _usageRepository = usageRepository;
        _userResolver = userResolver;
        _logger = logger;
    }

    /// <summary>
    /// Totals plus by-user (top 25 by cost), by-model, by-mode and by-day breakdowns over the
    /// given range and optional guild/mode filters.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(LlmUsageSummaryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LlmUsageSummaryResponseDto>> GetSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] ulong? guildId,
        [FromQuery] LlmMode? mode,
        CancellationToken cancellationToken)
    {
        if (!TryResolveRange(from, to, out var range, out var rangeError))
        {
            return BadRequestError(rangeError!);
        }

        var query = new LlmUsageQuery
        {
            From = range.From,
            To = range.To,
            GuildId = guildId,
            Mode = mode
        };

        var totals = await _usageRepository.GetTotalsAsync(query, cancellationToken);
        var byUser = await _usageRepository.GetByUserAsync(query, SummaryUserTake, cancellationToken);
        var byModel = await _usageRepository.GetByModelAsync(query, cancellationToken);
        var byMode = await _usageRepository.GetByModeAsync(query, cancellationToken);
        var byDay = await _usageRepository.GetByDayAsync(query, cancellationToken);

        var names = await _userResolver.ResolveUsersAsync(byUser.Select(u => u.UserId));

        return Ok(new LlmUsageSummaryResponseDto
        {
            Totals = ToDto(totals),
            ByUser = byUser.Select(u => ToDto(u, names)).ToList(),
            ByModel = byModel.Select(ToDto).ToList(),
            ByMode = byMode.Select(ToDto).ToList(),
            ByDay = byDay.Select(ToDto).ToList(),
            From = range.From,
            To = range.To
        });
    }

    /// <summary>
    /// Paged raw ledger rows over the given range and optional guild/mode/user filters, newest
    /// first - backs the per-user drill-down panel.
    /// </summary>
    [HttpGet("records")]
    [ProducesResponseType(typeof(LlmUsagePagedRecordsResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LlmUsagePagedRecordsResponseDto>> GetRecords(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] ulong? guildId,
        [FromQuery] LlmMode? mode,
        [FromQuery] ulong? userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveRange(from, to, out var range, out var rangeError))
        {
            return BadRequestError(rangeError!);
        }

        if (page < 1)
        {
            return BadRequestError("Invalid page", "page must be 1 or greater.");
        }

        if (pageSize < 1)
        {
            return BadRequestError("Invalid pageSize", "pageSize must be 1 or greater.");
        }

        pageSize = Math.Min(pageSize, MaxPageSize);

        var query = new LlmUsageQuery
        {
            From = range.From,
            To = range.To,
            GuildId = guildId,
            Mode = mode,
            UserId = userId
        };

        var paged = await _usageRepository.GetRecordsAsync(query, page, pageSize, cancellationToken);

        var names = await _userResolver.ResolveUsersAsync(paged.Records.Select(r => r.UserId));

        return Ok(new LlmUsagePagedRecordsResponseDto
        {
            Records = paged.Records.Select(r => ToDto(r, names)).ToList(),
            TotalCount = paged.TotalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Resolves and validates the <c>from</c>/<c>to</c> query range: defaults to the last
    /// <see cref="DefaultRangeDays"/> days when both are omitted, requires <c>to &gt;= from</c>,
    /// and caps the span at <see cref="LlmUsageRangeLimits.MaxRangeDays"/> days.
    /// </summary>
    private static bool TryResolveRange(DateTime? from, DateTime? to, out (DateTime From, DateTime To) range, out string? error)
    {
        var resolvedTo = to ?? DateTime.UtcNow;
        // A bare date (e.g. `to=2026-09-10`) carries no time component; round it up to the end of
        // that day so the range includes the whole day, matching the `From` side which starts at
        // midnight. Without this, a same-day request like `from=2026-09-10&to=2026-09-10` would
        // span zero time.
        if (to.HasValue && resolvedTo.TimeOfDay == TimeSpan.Zero)
        {
            resolvedTo = resolvedTo.Date.AddDays(1).AddTicks(-1);
        }
        var resolvedFrom = from ?? resolvedTo.AddDays(-DefaultRangeDays);

        if (resolvedTo < resolvedFrom)
        {
            range = default;
            error = "'to' must not be earlier than 'from'.";
            return false;
        }

        if ((resolvedTo - resolvedFrom).TotalDays > LlmUsageRangeLimits.MaxRangeDays)
        {
            range = default;
            error = $"The date range cannot exceed {LlmUsageRangeLimits.MaxRangeDays} days.";
            return false;
        }

        range = (resolvedFrom, resolvedTo);
        error = null;
        return true;
    }

    private static LlmUsageTotalsDto ToDto(LlmUsageTotals totals) => new()
    {
        MessageCount = totals.MessageCount,
        InputTokens = totals.InputTokens,
        OutputTokens = totals.OutputTokens,
        CachedTokens = totals.CachedTokens,
        CacheWriteTokens = totals.CacheWriteTokens,
        CostUsd = totals.CostUsd,
        BilledCostShare = totals.BilledCostShare,
        FailedCount = totals.FailedCount,
        AverageLatencyMs = totals.AverageLatencyMs
    };

    private static LlmUsageByUserDto ToDto(LlmUsageByUser row, IReadOnlyDictionary<ulong, (string Username, string? AvatarUrl)> names)
    {
        var (username, avatarUrl) = names.TryGetValue(row.UserId, out var resolved)
            ? resolved
            : ($"Unknown#{row.UserId}", null);

        return new LlmUsageByUserDto
        {
            UserId = row.UserId.ToString(),
            DisplayName = username,
            AvatarUrl = avatarUrl,
            MessageCount = row.MessageCount,
            InputTokens = row.InputTokens,
            OutputTokens = row.OutputTokens,
            CachedTokens = row.CachedTokens,
            CostUsd = row.CostUsd,
            CostShare = row.CostShare
        };
    }

    private static LlmUsageByModelDto ToDto(LlmUsageByModel row) => new()
    {
        Model = row.Model,
        MessageCount = row.MessageCount,
        InputTokens = row.InputTokens,
        OutputTokens = row.OutputTokens,
        CostUsd = row.CostUsd
    };

    private static LlmUsageByModeDto ToDto(LlmUsageByMode row) => new()
    {
        Mode = row.Mode.ToString(),
        MessageCount = row.MessageCount,
        InputTokens = row.InputTokens,
        OutputTokens = row.OutputTokens,
        CostUsd = row.CostUsd
    };

    private static LlmUsageByDayDto ToDto(LlmUsageByDay row) => new()
    {
        Day = row.Day,
        MessageCount = row.MessageCount,
        InputTokens = row.InputTokens,
        OutputTokens = row.OutputTokens,
        CostUsd = row.CostUsd
    };

    private static LlmUsageRecordRowDto ToDto(LlmUsageRecord row, IReadOnlyDictionary<ulong, (string Username, string? AvatarUrl)> names)
    {
        var displayName = names.TryGetValue(row.UserId, out var resolved) ? resolved.Username : $"Unknown#{row.UserId}";

        return new LlmUsageRecordRowDto
        {
            Id = row.Id,
            Timestamp = row.Timestamp,
            Mode = row.Mode.ToString(),
            UserId = row.UserId.ToString(),
            DisplayName = displayName,
            GuildId = row.GuildId?.ToString(),
            Model = row.Model,
            InputTokens = row.InputTokens,
            OutputTokens = row.OutputTokens,
            CachedTokens = row.CachedTokens,
            CacheWriteTokens = row.CacheWriteTokens,
            LlmCalls = row.LlmCalls,
            ToolCalls = row.ToolCalls,
            CostUsd = row.CostUsd,
            CostSource = row.CostSource.ToString(),
            LatencyMs = row.LatencyMs,
            Success = row.Success,
            InteractionLogId = row.InteractionLogId
        };
    }
}
