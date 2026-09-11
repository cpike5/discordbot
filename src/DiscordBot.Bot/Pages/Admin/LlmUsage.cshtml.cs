using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Bot.Pages.Admin;

/// <summary>
/// Portal-wide LLM token/cost usage dashboard: hero totals and breakdowns by user, model, mode and
/// day over a date range, with an optional guild/mode filter. Renders the summary server-side from
/// <see cref="ILlmUsageRepository"/> (the same aggregation <see cref="Controllers.LlmUsageController"/>
/// exposes at <c>api/admin/llm-usage/summary</c>, called independently here so the initial page load
/// needs no extra round trip - the same pattern used by the sibling Performance dashboard tabs).
/// The per-user drill-down (paged raw records) is fetched client-side from
/// <c>api/admin/llm-usage/records</c> by <c>wwwroot/js/llm-usage.js</c>.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
public class LlmUsageModel : PageModel
{
    private const int DefaultRangeDays = 30;
    private const int SummaryUserTake = 25;

    private readonly ILlmUsageRepository _usageRepository;
    private readonly IDiscordUserResolver _userResolver;
    private readonly IGuildService _guildService;
    private readonly ILogger<LlmUsageModel> _logger;

    public LlmUsageModel(
        ILlmUsageRepository usageRepository,
        IDiscordUserResolver userResolver,
        IGuildService guildService,
        ILogger<LlmUsageModel> logger)
    {
        _usageRepository = usageRepository;
        _userResolver = userResolver;
        _guildService = guildService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? EndDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public ulong? GuildId { get; set; }

    [BindProperty(SupportsGet = true)]
    public LlmMode? Mode { get; set; }

    /// <summary>Guilds for the guild filter <c>&lt;select&gt;</c>.</summary>
    public IReadOnlyList<GuildDto> Guilds { get; private set; } = Array.Empty<GuildDto>();

    public LlmUsageTotalsDto Totals { get; private set; } = new();

    public IReadOnlyList<LlmUsageByUserDto> ByUser { get; private set; } = Array.Empty<LlmUsageByUserDto>();

    public IReadOnlyList<LlmUsageByModelDto> ByModel { get; private set; } = Array.Empty<LlmUsageByModelDto>();

    public IReadOnlyList<LlmUsageByModeDto> ByMode { get; private set; } = Array.Empty<LlmUsageByModeDto>();

    public IReadOnlyList<LlmUsageByDayDto> ByDay { get; private set; } = Array.Empty<LlmUsageByDayDto>();

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        EndDate = (EndDate ?? DateTime.UtcNow).Date;
        StartDate = (StartDate ?? EndDate.Value.AddDays(-DefaultRangeDays)).Date;

        if (StartDate > EndDate)
        {
            (StartDate, EndDate) = (EndDate, StartDate);
        }

        // Clamp to the same widest span the API enforces (see LlmUsageController.TryResolveRange)
        // so a hand-edited query string can't request a range the summary API would reject.
        var earliestAllowedStart = EndDate.Value.AddDays(-LlmUsageRangeLimits.MaxRangeDays);
        if (StartDate < earliestAllowedStart)
        {
            StartDate = earliestAllowedStart;
        }

        var query = new LlmUsageQuery
        {
            From = StartDate.Value,
            // Include the entire end day.
            To = EndDate.Value.AddDays(1).AddTicks(-1),
            GuildId = GuildId,
            Mode = Mode
        };

        Guilds = await _guildService.GetAllGuildsAsync(cancellationToken);

        var totals = await _usageRepository.GetTotalsAsync(query, cancellationToken);
        var byUser = await _usageRepository.GetByUserAsync(query, SummaryUserTake, cancellationToken);
        var byModel = await _usageRepository.GetByModelAsync(query, cancellationToken);
        var byMode = await _usageRepository.GetByModeAsync(query, cancellationToken);
        var byDay = await _usageRepository.GetByDayAsync(query, cancellationToken);

        var names = await _userResolver.ResolveUsersAsync(byUser.Select(u => u.UserId));

        Totals = new LlmUsageTotalsDto
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

        ByUser = byUser.Select(u =>
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

        ByModel = byModel.Select(m => new LlmUsageByModelDto
        {
            Model = m.Model,
            MessageCount = m.MessageCount,
            InputTokens = m.InputTokens,
            OutputTokens = m.OutputTokens,
            CostUsd = m.CostUsd
        }).ToList();

        ByMode = byMode.Select(m => new LlmUsageByModeDto
        {
            Mode = m.Mode.ToString(),
            MessageCount = m.MessageCount,
            InputTokens = m.InputTokens,
            OutputTokens = m.OutputTokens,
            CostUsd = m.CostUsd
        }).ToList();

        ByDay = byDay.Select(d => new LlmUsageByDayDto
        {
            Day = d.Day,
            MessageCount = d.MessageCount,
            InputTokens = d.InputTokens,
            OutputTokens = d.OutputTokens,
            CostUsd = d.CostUsd
        }).ToList();

        _logger.LogDebug(
            "Loaded LLM usage dashboard for {From} to {To} (guild={GuildId}, mode={Mode}): {MessageCount} messages",
            StartDate, EndDate, GuildId, Mode, Totals.MessageCount);

        return Page();
    }
}
