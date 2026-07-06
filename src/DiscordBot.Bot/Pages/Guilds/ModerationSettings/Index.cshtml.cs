using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Discord.WebSocket;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.ModerationSettings;

/// <summary>
/// Page model for the Guild Moderation Settings page.
/// Allows administrators to configure auto-moderation settings for a guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : GuildPageModelBase
{
    private readonly IGuildModerationConfigService _configService;
    private readonly IModTagService _modTagService;
    private readonly IGuildService _guildService;
    private readonly IFlaggedEventService _flaggedEventService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<IndexModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexModel"/> class.
    /// </summary>
    public IndexModel(
        IGuildModerationConfigService configService,
        IModTagService modTagService,
        IGuildService guildService,
        IFlaggedEventService flaggedEventService,
        DiscordSocketClient discordClient,
        ILogger<IndexModel> logger)
    {
        _configService = configService;
        _modTagService = modTagService;
        _guildService = guildService;
        _flaggedEventService = flaggedEventService;
        _discordClient = discordClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets or sets the view model for the page.
    /// </summary>
    public ModerationSettingsViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Gets or sets the guild ID from the route.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the guild name for display.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the guild icon URL (optional).
    /// </summary>
    public string? GuildIconUrl { get; set; }

    /// <summary>
    /// Gets or sets the number of events flagged in the last 24 hours.
    /// </summary>
    public int EventsFlagged { get; set; }

    /// <summary>
    /// Gets or sets the number of auto-actions taken in the last 24 hours.
    /// </summary>
    public int AutoActions { get; set; }

    /// <summary>
    /// Gets or sets the number of active moderation rules.
    /// </summary>
    public int ActiveRules { get; set; }

    /// <summary>
    /// Gets or sets the number of false positives dismissed in the last 24 hours.
    /// </summary>
    public int FalsePositives { get; set; }

    /// <summary>
    /// Gets or sets the list of available text channels for alert routing.
    /// </summary>
    public List<ChannelOption> AvailableChannels { get; set; } = new();

    /// <summary>
    /// Handles GET requests for the Moderation Settings page.
    /// </summary>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Moderation settings page accessed for guild {GuildId} by user {UserId}",
            GuildId, User.Identity?.Name);

        // Load guild information
        var guild = await _guildService.GetGuildByIdAsync(GuildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", GuildId);
            return NotFound();
        }

        GuildName = guild.Name;
        GuildIconUrl = guild.IconUrl;

        // Populate guild layout ViewModels
        Breadcrumb = BuildPageBreadcrumb(guild.Id, guild.Name, "Moderation Settings");

        Header = BuildHeader(guild.Id, guild.Name, guild.IconUrl,
            "Moderation Settings", "Configure auto-moderation rules for this server");
        Header.Actions = new List<HeaderAction>
        {
            new()
            {
                Label = "View Flagged Events",
                Url = $"/Guilds/FlaggedEvents/{GuildId}",
                Style = HeaderActionStyle.Secondary,
                Icon = "M3 21v-4m0 0V5a2 2 0 012-2h6.5l1 1H21l-3 6 3 6h-8.5l-1-1H5a2 2 0 00-2 2zm9-13.5V9"
            }
        };

        Navigation = BuildNavigation(guild.Id, "moderation");

        // Load moderation config and tags
        var config = await _configService.GetConfigAsync(GuildId, cancellationToken);
        var tags = await _modTagService.GetGuildTagsAsync(GuildId, cancellationToken);

        ViewModel = ModerationSettingsViewModel.FromDto(config, tags);

        // Load guild channels for raid alert configuration
        var discordGuild = _discordClient.GetGuild(GuildId);
        if (discordGuild != null)
        {
            AvailableChannels = discordGuild.TextChannels
                .Where(c => c != null)
                .OrderBy(c => c.Position)
                .Select(c => new ChannelOption { Id = c.Id, Name = c.Name })
                .ToList();
        }

        // Load statistics for the last 24 hours
        await LoadStatisticsAsync(GuildId, cancellationToken);

        // Calculate active rules count
        ActiveRules = CalculateActiveRulesCount(config);

        return Page();
    }

    private async Task LoadStatisticsAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddHours(-24);

        try
        {
            // Get pending events for the guild (page size of 1000 to get all recent events for stats)
            var (events, totalCount) = await _flaggedEventService.GetPendingEventsAsync(guildId, 1, 1000, cancellationToken);

            EventsFlagged = events.Count(e => e.CreatedAt >= since);
            AutoActions = events.Count(e => e.CreatedAt >= since && !string.IsNullOrEmpty(e.ActionTaken));
            FalsePositives = events.Count(e => e.CreatedAt >= since && e.Status == Core.Enums.FlaggedEventStatus.Dismissed);

            _logger.LogDebug("Loaded statistics for guild {GuildId}: Events={Events}, AutoActions={AutoActions}, FalsePositives={FalsePositives}",
                guildId, EventsFlagged, AutoActions, FalsePositives);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load statistics for guild {GuildId}", guildId);
        }
    }

    private int CalculateActiveRulesCount(GuildModerationConfigDto config)
    {
        int count = 0;

        if (config.SpamConfig.Enabled) count++;
        if (config.ContentFilterConfig.Enabled) count++;
        if (config.RaidProtectionConfig.Enabled) count++;

        return count;
    }

    /// <summary>
    /// Represents a Discord channel option for dropdowns.
    /// </summary>
    public class ChannelOption
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
