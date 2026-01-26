using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Discord.WebSocket;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds.ModerationSettings;

/// <summary>
/// Page model for the Guild Moderation Settings page.
/// Allows administrators to configure auto-moderation settings for a guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : PageModel
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

    public GuildBreadcrumbViewModel Breadcrumb { get; set; } = new();
    public GuildHeaderViewModel Header { get; set; } = new();
    public GuildNavBarViewModel Navigation { get; set; } = new();

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
        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, Url = $"/Guilds/Details/{guild.Id}" },
                new() { Label = "Moderation Settings", IsCurrent = true }
            }
        };

        Header = new GuildHeaderViewModel
        {
            GuildId = guild.Id,
            GuildName = guild.Name,
            GuildIconUrl = guild.IconUrl,
            PageTitle = "Moderation Settings",
            PageDescription = "Configure auto-moderation rules for this server",
            Actions = new List<HeaderAction>
            {
                new()
                {
                    Label = "View Flagged Events",
                    Url = $"/Guilds/FlaggedEvents/{GuildId}",
                    Style = HeaderActionStyle.Secondary,
                    Icon = "M3 21v-4m0 0V5a2 2 0 012-2h6.5l1 1H21l-3 6 3 6h-8.5l-1-1H5a2 2 0 00-2 2zm9-13.5V9"
                }
            }
        };

        Navigation = new GuildNavBarViewModel
        {
            GuildId = guild.Id,
            ActiveTab = "moderation",
            Tabs = GuildNavigationConfig.GetTabs().ToList()
        };

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

    /// <summary>
    /// Handles POST requests to save overview settings (mode and preset).
    /// </summary>
    public async Task<IActionResult> OnPostSaveOverviewAsync([FromBody] OverviewUpdateDto request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving overview settings for guild {GuildId}: Mode={Mode}, Preset={Preset}",
            GuildId, request.Mode, request.SimplePreset);

        try
        {
            var config = await _configService.GetConfigAsync(GuildId, cancellationToken);
            config.Mode = request.Mode;
            config.SimplePreset = request.SimplePreset;

            var updated = await _configService.UpdateConfigAsync(GuildId, config, cancellationToken);

            _logger.LogInformation("Overview settings saved successfully for guild {GuildId}", GuildId);

            return new JsonResult(new { success = true, message = "Overview settings saved successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save overview settings for guild {GuildId}", GuildId);
            return new JsonResult(new { success = false, message = "Failed to save overview settings." }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Handles POST requests to save spam detection settings.
    /// </summary>
    public async Task<IActionResult> OnPostSaveSpamAsync([FromBody] SpamDetectionConfigDto request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving spam detection settings for guild {GuildId}", GuildId);

        try
        {
            var config = await _configService.GetConfigAsync(GuildId, cancellationToken);
            config.SpamConfig = request;

            var updated = await _configService.UpdateConfigAsync(GuildId, config, cancellationToken);

            _logger.LogInformation("Spam detection settings saved successfully for guild {GuildId}", GuildId);

            return new JsonResult(new { success = true, message = "Spam detection settings saved successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save spam detection settings for guild {GuildId}", GuildId);
            return new JsonResult(new { success = false, message = "Failed to save spam detection settings." }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Handles POST requests to save content filter settings.
    /// </summary>
    public async Task<IActionResult> OnPostSaveContentAsync([FromBody] ContentFilterConfigDto request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving content filter settings for guild {GuildId}", GuildId);

        try
        {
            var config = await _configService.GetConfigAsync(GuildId, cancellationToken);
            config.ContentFilterConfig = request;

            var updated = await _configService.UpdateConfigAsync(GuildId, config, cancellationToken);

            _logger.LogInformation("Content filter settings saved successfully for guild {GuildId}", GuildId);

            return new JsonResult(new { success = true, message = "Content filter settings saved successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save content filter settings for guild {GuildId}", GuildId);
            return new JsonResult(new { success = false, message = "Failed to save content filter settings." }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Handles POST requests to save raid protection settings.
    /// </summary>
    public async Task<IActionResult> OnPostSaveRaidAsync([FromBody] RaidProtectionConfigDto request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving raid protection settings for guild {GuildId}", GuildId);

        try
        {
            var config = await _configService.GetConfigAsync(GuildId, cancellationToken);
            config.RaidProtectionConfig = request;

            var updated = await _configService.UpdateConfigAsync(GuildId, config, cancellationToken);

            _logger.LogInformation("Raid protection settings saved successfully for guild {GuildId}", GuildId);

            return new JsonResult(new { success = true, message = "Raid protection settings saved successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save raid protection settings for guild {GuildId}", GuildId);
            return new JsonResult(new { success = false, message = "Failed to save raid protection settings." }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Handles POST requests to apply a preset configuration.
    /// </summary>
    public async Task<IActionResult> OnPostApplyPresetAsync([FromBody] ApplyPresetDto request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying preset {PresetName} for guild {GuildId}", request.PresetName, GuildId);

        try
        {
            var config = await _configService.ApplyPresetAsync(GuildId, request.PresetName, cancellationToken);

            _logger.LogInformation("Preset {PresetName} applied successfully for guild {GuildId}", request.PresetName, GuildId);

            return new JsonResult(new { success = true, message = $"Preset '{request.PresetName}' applied successfully.", config });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply preset {PresetName} for guild {GuildId}", request.PresetName, GuildId);
            return new JsonResult(new { success = false, message = "Failed to apply preset." }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Handles POST requests to create a new mod tag.
    /// </summary>
    public async Task<IActionResult> OnPostCreateTagAsync([FromBody] ModTagCreateDto request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating tag {TagName} for guild {GuildId}", request.Name, GuildId);

        try
        {
            request.GuildId = GuildId;
            var tag = await _modTagService.CreateTagAsync(GuildId, request, cancellationToken);

            _logger.LogInformation("Tag {TagName} created successfully for guild {GuildId}", request.Name, GuildId);

            return new JsonResult(new { success = true, message = "Tag created successfully.", tag });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create tag {TagName} for guild {GuildId}", request.Name, GuildId);
            return new JsonResult(new { success = false, message = "Failed to create tag." }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Handles POST requests to delete a mod tag.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteTagAsync([FromQuery] string tagName, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting tag {TagName} for guild {GuildId}", tagName, GuildId);

        try
        {
            var success = await _modTagService.DeleteTagAsync(GuildId, tagName, cancellationToken);

            if (!success)
            {
                _logger.LogWarning("Tag {TagName} not found for guild {GuildId}", tagName, GuildId);
                return new JsonResult(new { success = false, message = "Tag not found." }) { StatusCode = 404 };
            }

            _logger.LogInformation("Tag {TagName} deleted successfully for guild {GuildId}", tagName, GuildId);

            return new JsonResult(new { success = true, message = "Tag deleted successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete tag {TagName} for guild {GuildId}", tagName, GuildId);
            return new JsonResult(new { success = false, message = "Failed to delete tag." }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Handles POST requests to import template tags.
    /// </summary>
    public async Task<IActionResult> OnPostImportTemplatesAsync([FromBody] string[] templateNames, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Importing {Count} template tags for guild {GuildId}", templateNames.Length, GuildId);

        try
        {
            var count = await _modTagService.ImportTemplateTagsAsync(GuildId, templateNames, cancellationToken);

            _logger.LogInformation("{Count} template tags imported successfully for guild {GuildId}", count, GuildId);

            return new JsonResult(new { success = true, message = $"{count} template tags imported successfully.", count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import template tags for guild {GuildId}", GuildId);
            return new JsonResult(new { success = false, message = "Failed to import template tags." }) { StatusCode = 500 };
        }
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
