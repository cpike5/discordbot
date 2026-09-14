using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Resolves and validates a <c>from</c>/<c>to</c> date range for an LLM usage query, shared by
/// <c>Blazor/Pages/Admin/LlmUsage/Index.razor.cs</c>'s initial load and its per-user drill-down
/// paging (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d - "move TryResolveRange into a
/// shared helper so the page model's duplicate disappears"). Replaces the now-deleted
/// <c>Controllers/LlmUsageController.TryResolveRange</c> and <c>Pages/Admin/LlmUsage.cshtml.cs</c>'s
/// own inline clamp - the single surviving copy of that logic.
/// </summary>
public static class LlmUsageRangeHelper
{
    private const int DefaultRangeDays = 30;

    /// <summary>
    /// Resolves <paramref name="from"/>/<paramref name="to"/>, defaulting to the last
    /// <see cref="DefaultRangeDays"/> days when both are omitted, requiring <c>to &gt;= from</c>,
    /// and capping the span at <see cref="LlmUsageRangeLimits.MaxRangeDays"/> days by pulling
    /// <paramref name="from"/> forward (never truncating <paramref name="to"/>) - matching the
    /// legacy page model's "clamp to the same widest span the API enforces" behaviour.
    /// </summary>
    public static (DateTime From, DateTime To) ResolveAndClamp(DateTime? from, DateTime? to)
    {
        var resolvedTo = (to ?? DateTime.UtcNow).Date;
        var resolvedFrom = (from ?? resolvedTo.AddDays(-DefaultRangeDays)).Date;

        if (resolvedFrom > resolvedTo)
        {
            (resolvedFrom, resolvedTo) = (resolvedTo, resolvedFrom);
        }

        var earliestAllowedStart = resolvedTo.AddDays(-LlmUsageRangeLimits.MaxRangeDays);
        if (resolvedFrom < earliestAllowedStart)
        {
            resolvedFrom = earliestAllowedStart;
        }

        return (resolvedFrom, resolvedTo);
    }
}
