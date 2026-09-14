using DiscordBot.Agents.Contracts;
using DiscordBot.Bot.Blazor.Guilds;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models.Llm;
using DiscordBot.Infrastructure.Abstractions.LLM;
using Microsoft.AspNetCore.Components;

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
public partial class AssistantMetrics : GuildPageBase
{
    private const int CostByUserTake = 20;

    [Inject]
    private IAssistantService AssistantService { get; set; } = default!;

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

    protected List<AssistantUsageMetrics> Metrics { get; private set; } = [];
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

        Metrics = (await AssistantService.GetUsageMetricsRangeAsync(guildId, startDate, endDate)).ToList();

        var toolUsage = await InteractionLogRepository.GetToolUsageAsync(guildId, startDate, endDate.AddDays(1).AddTicks(-1));
        ToolUsage = BuildToolUsageRows(toolUsage);
        HasToolUsageData = toolUsage.Count > 0;

        PromptSurface = await ReadPromptSurfaceAsync(guildId);

        var usageQuery = new LlmUsageQuery { From = startDate, To = endDate.AddDays(1).AddTicks(-1), GuildId = guildId };
        var costByUser = await UsageRepository.GetByUserAsync(usageQuery, CostByUserTake);
        var names = await UserResolver.ResolveUsersAsync(costByUser.Select(u => u.UserId));
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
