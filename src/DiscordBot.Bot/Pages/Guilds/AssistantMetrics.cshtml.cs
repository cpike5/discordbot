using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Agents.Configuration;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Models.Llm;
using DiscordBot.Infrastructure.Abstractions.LLM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Page model for viewing AI assistant usage metrics for a guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class AssistantMetricsModel : GuildPageModelBase
{
    /// <summary>How many top-cost users the "Cost by User" table shows.</summary>
    private const int CostByUserTake = 20;

    private readonly IAssistantTelemetryReader _telemetryReader;
    private readonly IOptions<OpenRouterOptions> _openRouterOptions;
    private readonly IGuildService _guildService;
    private readonly IAssistantInteractionLogRepository _interactionLogRepository;
    private readonly ILlmUsageRepository _usageRepository;
    private readonly IDiscordUserResolver _userResolver;
    private readonly IPromptSurfaceReporter _promptSurface;
    private readonly ILogger<AssistantMetricsModel> _logger;

    public AssistantMetricsModel(
        IAssistantTelemetryReader telemetryReader,
        IOptions<OpenRouterOptions> openRouterOptions,
        IGuildService guildService,
        IAssistantInteractionLogRepository interactionLogRepository,
        ILlmUsageRepository usageRepository,
        IDiscordUserResolver userResolver,
        IPromptSurfaceReporter promptSurface,
        ILogger<AssistantMetricsModel> logger)
    {
        _telemetryReader = telemetryReader;
        _openRouterOptions = openRouterOptions;
        _guildService = guildService;
        _interactionLogRepository = interactionLogRepository;
        _usageRepository = usageRepository;
        _userResolver = userResolver;
        _promptSurface = promptSurface;
        _logger = logger;
    }

    /// <summary>
    /// Guild ID from route.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Guild information for display.
    /// </summary>
    public GuildViewModel Guild { get; set; } = new();

    /// <summary>
    /// Daily metrics for the last 30 days.
    /// </summary>
    public List<AssistantUsageMetrics> Metrics { get; set; } = new();

    /// <summary>
    /// Total questions asked in the period.
    /// </summary>
    public int TotalQuestions { get; set; }

    /// <summary>
    /// Total cost in USD for the period.
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Average response latency in ms.
    /// </summary>
    public int AverageLatencyMs { get; set; }

    /// <summary>
    /// Total cache hits.
    /// </summary>
    public int TotalCacheHits { get; set; }

    /// <summary>
    /// Total cache misses.
    /// </summary>
    public int TotalCacheMisses { get; set; }

    /// <summary>
    /// Cache hit rate as a percentage.
    /// </summary>
    public double CacheHitRate { get; set; }

    /// <summary>
    /// Total failed requests.
    /// </summary>
    public int TotalFailedRequests { get; set; }

    /// <summary>
    /// Success rate as a percentage.
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Total tool calls made.
    /// </summary>
    public int TotalToolCalls { get; set; }

    /// <summary>
    /// Thread-mode conversations in the period and the turns they took. Zero for a guild on single
    /// replies, which is what the tile says.
    /// </summary>
    public AssistantConversationStats Conversations { get; set; } = AssistantConversationStats.Empty;

    /// <summary>
    /// Top spenders in this guild over the same 30-day window, from the LLM usage ledger
    /// (<see cref="ILlmUsageRepository"/>), newest ledger data source - not the daily
    /// <see cref="AssistantUsageMetrics"/> aggregates above, which carry no per-user breakdown.
    /// </summary>
    public IReadOnlyList<LlmUsageByUserDto> CostByUser { get; set; } = Array.Empty<LlmUsageByUserDto>();

    /// <summary>
    /// Per-tool usage over the same 30-day window, including tools that were never called - "which
    /// of my tools has never been used" is the first question worth answering, and it is only
    /// answerable if the zeroes are shown.
    /// </summary>
    public IReadOnlyList<ToolUsageRow> ToolUsage { get; set; } = Array.Empty<ToolUsageRow>();

    /// <summary>
    /// Whether any interaction in the window recorded tool names at all. Rows logged before the
    /// <c>ToolNames</c> column existed carry none, so an established guild can legitimately show an
    /// all-zero table for a while.
    /// </summary>
    public bool HasToolUsageData { get; set; }

    /// <summary>
    /// What this guild's assistant puts in front of the model on every question, and what it costs.
    /// Null when no OpenRouter key is configured, so there is no registry to measure.
    /// </summary>
    /// <remarks>
    /// The usage table above answers "which tools did we use"; this answers "which tools did we pay
    /// for". They are different questions, and the gap between them is the one worth acting on: a
    /// tool that is 15% of every request and was called twice in a month belongs behind a skill or
    /// turned off.
    /// </remarks>
    public PromptSurfaceReport? PromptSurface { get; set; }

    /// <summary>
    /// Whether an OpenRouter API key is configured. Without one the assistant is not registered and
    /// nothing is ever recorded, so the page says so instead of showing a wall of zeros.
    /// </summary>
    public bool IsAssistantConfigured { get; set; }

    /// <summary>
    /// Whether any usage was recorded in the window. False renders "no data yet" rather than
    /// red zeros (a 0% success rate on a server that was never asked a question).
    /// </summary>
    public bool HasUsageData => Metrics.Count > 0;

    /// <summary>One row of the per-tool usage table.</summary>
    public class ToolUsageRow
    {
        public string ToolName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Calls { get; set; }
        public int Interactions { get; set; }
        public double FailureRate { get; set; }
        public DateTime? LastUsed { get; set; }
    }

    /// <summary>
    /// View model for guild display.
    /// </summary>
    public class GuildViewModel
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? IconUrl { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing assistant metrics page for guild {GuildId}", GuildId);

        // Get guild info
        var guild = await _guildService.GetGuildByIdAsync(GuildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", GuildId);
            return NotFound();
        }

        Guild = new GuildViewModel
        {
            Id = guild.Id,
            Name = guild.Name,
            IconUrl = guild.IconUrl
        };

        IsAssistantConfigured = !string.IsNullOrWhiteSpace(_openRouterOptions.Value.ApiKey);

        // Populate guild layout ViewModels first so a failed load still renders the page chrome
        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, Url = $"/Guilds/Details/{GuildId}" },
                new() { Label = "Assistant", Url = $"/Guilds/AssistantSettings/{GuildId}" },
                new() { Label = "Metrics", IsCurrent = true }
            }
        };
        Header = BuildHeader(guild.Id, guild.Name, guild.IconUrl,
            "Assistant Usage Metrics", $"AI assistant usage statistics for {guild.Name}");
        Navigation = BuildNavigation(guild.Id, "assistant");

        try
        {
            await LoadMetricsAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load assistant metrics for guild {GuildId}", GuildId);
            Metrics = new List<AssistantUsageMetrics>();
            ErrorMessage = "The usage metrics could not be loaded. Try again in a moment.";
        }

        return Page();
    }

    private async Task LoadMetricsAsync(CancellationToken cancellationToken)
    {
        // Get metrics for last 30 days
        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-30);

        Metrics = (await _telemetryReader.GetUsageMetricsRangeAsync(
            GuildId, startDate, endDate, cancellationToken)).ToList();

        Conversations = await _telemetryReader.GetConversationStatsAsync(
            GuildId, startDate, endDate.AddDays(1).AddTicks(-1), cancellationToken);

        var toolUsage = await _interactionLogRepository.GetToolUsageAsync(
            GuildId, startDate, endDate.AddDays(1).AddTicks(-1), cancellationToken);
        ToolUsage = BuildToolUsageRows(toolUsage);
        HasToolUsageData = toolUsage.Count > 0;

        PromptSurface = await ReadPromptSurfaceAsync(cancellationToken);

        var usageQuery = new LlmUsageQuery
        {
            From = startDate,
            To = endDate.AddDays(1).AddTicks(-1),
            GuildId = GuildId
        };
        var costByUser = await _usageRepository.GetByUserAsync(usageQuery, CostByUserTake, cancellationToken);
        var names = await _userResolver.ResolveUsersAsync(costByUser.Select(u => u.UserId));
        CostByUser = costByUser.Select(u =>
        {
            var (username, avatarUrl) = names.TryGetValue(u.UserId, out var resolved) ? resolved : (UserDisplay.UnknownName, null);
            username = UserDisplay.Name(username);
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

        // Calculate summary statistics
        if (Metrics.Any())
        {
            TotalQuestions = Metrics.Sum(m => m.TotalQuestions);
            TotalCost = Metrics.Sum(m => m.EstimatedCostUsd);
            TotalCacheHits = Metrics.Sum(m => m.TotalCacheHits);
            TotalCacheMisses = Metrics.Sum(m => m.TotalCacheMisses);
            TotalFailedRequests = Metrics.Sum(m => m.FailedRequests);
            TotalToolCalls = Metrics.Sum(m => m.TotalToolCalls);

            // Calculate weighted average latency
            var totalLatencyWeight = Metrics.Sum(m => m.TotalQuestions * m.AverageLatencyMs);
            AverageLatencyMs = TotalQuestions > 0 ? totalLatencyWeight / TotalQuestions : 0;

            // Calculate cache hit rate
            var totalCacheRequests = TotalCacheHits + TotalCacheMisses;
            CacheHitRate = totalCacheRequests > 0 ? (double)TotalCacheHits / totalCacheRequests * 100 : 0;

            // Calculate success rate
            var totalRequests = TotalQuestions + TotalFailedRequests;
            SuccessRate = totalRequests > 0 ? (double)TotalQuestions / totalRequests * 100 : 100;
        }
    }

    /// <summary>
    /// The prompt-surface report for this guild, or null when it cannot be built.
    /// </summary>
    /// <remarks>
    /// Building it constructs every registered tool provider, which is the one thing on this page
    /// that can fail for a reason unrelated to metrics. A panel that cannot be drawn is not a reason
    /// to fail the page, so it is caught and the panel is left out.
    /// </remarks>
    private async Task<PromptSurfaceReport?> ReadPromptSurfaceAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _promptSurface.ReportAsync(ToolScopes.Guild, GuildId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not build the prompt-surface report for guild {GuildId}", GuildId);
            return null;
        }
    }

    /// <summary>
    /// Pairs the aggregate with the tool catalogue so every guild-scoped tool gets a row, called or
    /// not, and so a tool that has been renamed or removed still shows its history under the
    /// catalogue's <c>Other</c> bucket rather than vanishing.
    /// </summary>
    private static List<ToolUsageRow> BuildToolUsageRows(IReadOnlyList<AssistantToolUsage> usage)
    {
        var byName = usage.ToDictionary(u => u.ToolName, StringComparer.OrdinalIgnoreCase);

        var rows = ToolCatalog.ForScope(ToolScopes.Guild)
            .Select(entry => BuildRow(entry.Name, byName.GetValueOrDefault(entry.Name)))
            .ToList();

        // Anything logged that the catalogue does not know - a removed or renamed tool - is still
        // real usage and is shown rather than dropped.
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
            FailureRate = usage is { Interactions: > 0 }
                ? (double)usage.FailedInteractions / usage.Interactions * 100
                : 0,
            LastUsed = usage?.LastUsed
        };
    }
}
