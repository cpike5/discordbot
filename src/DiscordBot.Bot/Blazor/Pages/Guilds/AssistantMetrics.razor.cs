using DiscordBot.Agents.Contracts;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models.Llm;
using DiscordBot.Infrastructure.Abstractions.LLM;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Blazor.Pages.Guilds;

/// <summary>
/// Code-behind for the routable replacement of <c>Pages/Guilds/AssistantMetrics.cshtml</c> +
/// <c>AssistantMetricsModel</c> (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4b).
/// Read-only: a 30-day window of daily metrics, per-tool usage (every catalogued tool, called or
/// not), the prompt-surface report (null when the assistant is not configured), and the top 20
/// spenders from the LLM usage ledger.
/// </summary>
/// <remarks>
/// <c>GuildLayout</c> resolves this route's active tab from <c>GuildNavigationConfig</c>, whose
/// "assistant" tab points at <c>/Guilds/AssistantSettings/{id}</c> - a different URL than this
/// page's own - so the layout finds no matching tab here and falls back to the guild's plain name
/// for the header/breadcrumb rather than the legacy page's custom "Home &gt; Servers &gt; Guild
/// &gt; Assistant &gt; Metrics" breadcrumb. Accepted per the cluster brief; noted for the
/// orchestrator.
/// </remarks>
/// <remarks>
/// <see cref="IAssistantService"/> is resolved optionally through <see cref="IServiceProvider"/>
/// rather than a plain <c>[Inject]</c>: <c>AssistantServiceExtensions.AddAssistant</c> only
/// registers it when <c>OpenRouter:ApiKey</c> is configured (CLAUDE.md, "the assistant talks to
/// OpenRouter"), unlike every other service this page depends on
/// (<see cref="IAssistantInteractionLogRepository"/>, <see cref="ILlmUsageRepository"/>,
/// <see cref="IPromptSurfaceReporter"/> are all registered ungated). A plain <c>[Inject]</c> would
/// throw <c>InvalidOperationException</c> constructing this component on any deployment with no
/// key configured - reproduced against the real host in web-only mode, the exact same crash the
/// deleted <c>AssistantMetricsModel</c>'s constructor-injected <c>IAssistantService</c> parameter
/// would have hit had any test ever visited it (none had, before this cluster's own E2E coverage).
/// Null here means the daily-metrics summary/table renders its existing "No usage data yet" empty
/// state - the same "a panel that cannot be drawn is not a reason to fail the whole page" posture
/// <see cref="ReadPromptSurfaceAsync"/> already uses for its own gated dependency, extended by
/// <see cref="ReadOrEmptyAsync{T}"/> to the metrics/tool-usage/cost-by-user queries too: a fresh
/// SQLite database can independently fail one of those (observed against the real host, a
/// pre-existing gap the migration set applying at app-startup leaves some late columns missing -
/// out of scope for this cluster to fix), and none of the four panels should take the other three
/// down with it.
/// </remarks>
public partial class AssistantMetrics : GuildPageBase
{
    private const int CostByUserTake = 20;

    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    [Inject]
    private IAssistantInteractionLogRepository InteractionLogRepository { get; set; } = default!;

    [Inject]
    private ILlmUsageRepository UsageRepository { get; set; } = default!;

    [Inject]
    private IDiscordUserResolver UserResolver { get; set; } = default!;

    [Inject]
    private IPromptSurfaceReporter PromptSurfaceReporter { get; set; } = default!;

    [Inject]
    private ILogger<AssistantMetrics> Logger { get; set; } = default!;

    protected IReadOnlyList<AssistantUsageMetrics> Metrics { get; private set; } = [];
    protected int TotalQuestions { get; private set; }
    protected decimal TotalCost { get; private set; }
    protected int AverageLatencyMs { get; private set; }
    protected int TotalCacheHits { get; private set; }
    protected int TotalCacheMisses { get; private set; }
    protected double CacheHitRate { get; private set; }
    protected int TotalFailedRequests { get; private set; }
    protected double SuccessRate { get; private set; } = 100;
    protected int TotalToolCalls { get; private set; }
    protected IReadOnlyList<LlmUsageByUserDto> CostByUser { get; private set; } = [];
    protected IReadOnlyList<ToolUsageRow> ToolUsage { get; private set; } = [];
    protected bool HasToolUsageData { get; private set; }
    protected PromptSurfaceReport? PromptSurface { get; private set; }

    protected override async Task OnGuildContextReadyAsync()
    {
        if (Guild is null)
        {
            return;
        }

        var guildId = (ulong)GuildId;
        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-30);

        // Null when no OpenRouter:ApiKey is configured - see the class remarks. Metrics stays
        // empty, which the daily-breakdown table already renders as "No usage data yet".
        var assistantService = ServiceProvider.GetService<IAssistantService>();
        Metrics = assistantService is null
            ? []
            : await ReadOrEmptyAsync<AssistantUsageMetrics>(async () => (await assistantService.GetUsageMetricsRangeAsync(guildId, startDate, endDate)).ToList(), "usage metrics", guildId);

        var toolUsage = await ReadOrEmptyAsync(
            () => InteractionLogRepository.GetToolUsageAsync(guildId, startDate, endDate.AddDays(1).AddTicks(-1)),
            "tool usage", guildId);
        ToolUsage = BuildToolUsageRows(toolUsage);
        HasToolUsageData = toolUsage.Count > 0;

        PromptSurface = await ReadPromptSurfaceAsync(guildId);

        var usageQuery = new LlmUsageQuery { From = startDate, To = endDate.AddDays(1).AddTicks(-1), GuildId = guildId };
        var costByUser = await ReadOrEmptyAsync(
            () => UsageRepository.GetByUserAsync(usageQuery, CostByUserTake),
            "cost-by-user", guildId);
        var names = costByUser.Count > 0
            ? await UserResolver.ResolveUsersAsync(costByUser.Select(u => u.UserId))
            : new Dictionary<ulong, (string Username, string? AvatarUrl)>();
        CostByUser = costByUser.Select(u =>
        {
            var (username, avatarUrl) = names.TryGetValue(u.UserId, out var resolved) ? resolved : ($"Unknown#{u.UserId}", null);
            return new LlmUsageByUserDto
            {
                UserId = u.UserId.ToString(),
                DisplayName = username,
                AvatarUrl = avatarUrl,
                MessageCount = u.MessageCount,
                InputTokens = u.InputTokens,
                OutputTokens = u.OutputTokens,
                CachedTokens = u.CachedTokens,
                CostUsd = u.CostUsd,
                CostShare = u.CostShare
            };
        }).ToList();

        if (Metrics.Count > 0)
        {
            TotalQuestions = Metrics.Sum(m => m.TotalQuestions);
            TotalCost = Metrics.Sum(m => m.EstimatedCostUsd);
            TotalCacheHits = Metrics.Sum(m => m.TotalCacheHits);
            TotalCacheMisses = Metrics.Sum(m => m.TotalCacheMisses);
            TotalFailedRequests = Metrics.Sum(m => m.FailedRequests);
            TotalToolCalls = Metrics.Sum(m => m.TotalToolCalls);

            var totalLatencyWeight = Metrics.Sum(m => m.TotalQuestions * m.AverageLatencyMs);
            AverageLatencyMs = TotalQuestions > 0 ? totalLatencyWeight / TotalQuestions : 0;

            var totalCacheRequests = TotalCacheHits + TotalCacheMisses;
            CacheHitRate = totalCacheRequests > 0 ? (double)TotalCacheHits / totalCacheRequests * 100 : 0;

            var totalRequests = TotalQuestions + TotalFailedRequests;
            SuccessRate = totalRequests > 0 ? (double)TotalQuestions / totalRequests * 100 : 100;
        }
    }

    /// <remarks>
    /// Building the report constructs every registered tool provider, which is the one thing on
    /// this page that can fail for a reason unrelated to metrics. A panel that cannot be drawn is
    /// not a reason to fail the whole page, so it is caught and the panel is left out.
    /// </remarks>
    private async Task<PromptSurfaceReport?> ReadPromptSurfaceAsync(ulong guildId)
    {
        try
        {
            return await PromptSurfaceReporter.ReportAsync(ToolScopes.Guild, guildId);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not build the prompt-surface report for guild {GuildId}", guildId);
            return null;
        }
    }

    /// <summary>
    /// Runs one data source for this page's read-only panels, degrading to an empty result on
    /// failure instead of taking the whole page down with it - the same "a panel that cannot be
    /// drawn is not a reason to fail the whole page" posture <see cref="ReadPromptSurfaceAsync"/>
    /// already uses, extended to every other best-effort query here (each already has its own
    /// empty-state UI: "No usage data yet", the no-tool-names banner, "No per-user usage recorded
    /// for this range").
    /// </summary>
    private async Task<IReadOnlyList<T>> ReadOrEmptyAsync<T>(Func<Task<IReadOnlyList<T>>> query, string panelName, ulong guildId)
    {
        try
        {
            return await query();
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Could not load the {Panel} panel for guild {GuildId}", panelName, guildId);
            return [];
        }
    }

    /// <summary>
    /// Pairs the aggregate with the tool catalogue so every guild-scoped tool gets a row, called
    /// or not, and a tool the catalogue no longer knows still shows its history under
    /// <see cref="ToolCatalog.OtherCategory"/> rather than vanishing.
    /// </summary>
    private static List<ToolUsageRow> BuildToolUsageRows(IReadOnlyList<AssistantToolUsage> usage)
    {
        var byName = usage.ToDictionary(u => u.ToolName, StringComparer.OrdinalIgnoreCase);

        var rows = ToolCatalog.ForScope(ToolScopes.Guild)
            .Select(entry => BuildRow(entry.Name, byName.GetValueOrDefault(entry.Name)))
            .ToList();

        rows.AddRange(usage
            .Where(u => !ToolCatalog.IsCatalogued(u.ToolName))
            .Select(u => BuildRow(u.ToolName, u)));

        return rows
            .OrderByDescending(r => r.Calls)
            .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ToolUsageRow BuildRow(string toolName, AssistantToolUsage? usage)
    {
        var entry = ToolCatalog.Describe(toolName);

        return new ToolUsageRow
        {
            ToolName = toolName,
            DisplayName = entry.DisplayName,
            Category = entry.Category,
            Calls = usage?.Calls ?? 0,
            Interactions = usage?.Interactions ?? 0,
            FailureRate = usage is { Interactions: > 0 } ? (double)usage.FailedInteractions / usage.Interactions * 100 : 0,
            LastUsed = usage?.LastUsed
        };
    }

    /// <summary>One row of the per-tool usage table.</summary>
    public sealed class ToolUsageRow
    {
        public string ToolName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Calls { get; set; }
        public int Interactions { get; set; }
        public double FailureRate { get; set; }
        public DateTime? LastUsed { get; set; }
    }
}
