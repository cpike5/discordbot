using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Page model for viewing AI assistant usage metrics for a guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class AssistantMetricsModel : PageModel
{
    private readonly IAssistantService _assistantService;
    private readonly IGuildService _guildService;
    private readonly ILogger<AssistantMetricsModel> _logger;

    public AssistantMetricsModel(
        IAssistantService assistantService,
        IGuildService guildService,
        ILogger<AssistantMetricsModel> logger)
    {
        _assistantService = assistantService;
        _guildService = guildService;
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

        // Get metrics for last 30 days
        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-30);

        Metrics = (await _assistantService.GetUsageMetricsRangeAsync(
            GuildId, startDate, endDate, cancellationToken)).ToList();

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

        return Page();
    }
}
