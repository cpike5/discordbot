using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.Helpers;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Moderation;
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
    /// <summary>The preset names the "Protection level" cards offer.</summary>
    public static readonly string[] PresetNames = { "Relaxed", "Moderate", "Strict" };

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
    /// Gets or sets the number of events in the last 24 hours that a moderator has recorded an outcome for.
    /// </summary>
    public int ActionedEvents { get; set; }

    /// <summary>
    /// Gets or sets the number of active moderation rules.
    /// </summary>
    public int ActiveRules { get; set; }

    /// <summary>
    /// Gets or sets the number of events from the last 24 hours that a moderator dismissed.
    /// </summary>
    public int DismissedEvents { get; set; }

    /// <summary>
    /// Whether the last-24-hours numbers could be loaded. When false the page says so instead of showing zeros.
    /// </summary>
    public bool StatisticsLoaded { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of available text channels for alert routing.
    /// </summary>
    public List<ChannelOption> AvailableChannels { get; set; } = new();

    /// <summary>
    /// True when a mod-log channel is saved but is not among the channels the bot can see (deleted,
    /// or the bot is offline). The picker still lists it so a save does not silently clear it.
    /// </summary>
    public bool ModLogChannelMissing { get; set; }

    /// <summary>
    /// A channel named like a mod log, offered when no channel is saved. Until this setting existed
    /// the automod alert posted to a channel by that name, so the server probably wants it here.
    /// </summary>
    public ChannelOption? SuggestedModLogChannel { get; set; }

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

        ModLogChannelMissing = config.ModLogChannelId.HasValue
            && AvailableChannels.All(c => c.Id != config.ModLogChannelId.Value);

        if (!config.ModLogChannelId.HasValue)
        {
            SuggestedModLogChannel = AvailableChannels.FirstOrDefault(c => LooksLikeModLog(c.Name));
        }

        // Load statistics for the last 24 hours
        await LoadStatisticsAsync(GuildId, cancellationToken);

        // Calculate active rules count
        ActiveRules = CalculateActiveRulesCount(config);

        return Page();
    }

    /// <summary>
    /// Handles POST requests to save overview settings (the configuration mode).
    /// </summary>
    public async Task<IActionResult> OnPostSaveOverviewAsync([FromBody] OverviewUpdateDto request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Saving overview settings for guild {GuildId}: Mode={Mode}, Preset={Preset}",
            GuildId, request?.Mode, request?.SimplePreset);

        if (request == null)
        {
            return Rejected("Nothing to save.");
        }

        var errors = new Dictionary<string, string>();
        if (request.SimplePreset != null && !PresetNames.Contains(request.SimplePreset, StringComparer.OrdinalIgnoreCase))
        {
            errors["simplePreset"] = "Choose Relaxed, Moderate or Strict.";
        }

        ulong? modLogChannelId = null;
        if (request.ModLogChannelId != null)
        {
            var channelError = ModLogSettings.TryParseChannel(request.ModLogChannelId, out modLogChannelId)
                ?? ModLogSettings.ValidateChannel(_discordClient, GuildId, modLogChannelId);
            if (channelError != null)
            {
                errors["modLogChannelId"] = channelError;
            }
        }

        if (request.ModLogEvents.HasValue && ModLogSettings.ValidateEvents(request.ModLogEvents.Value) is { } eventsError)
        {
            errors["modLogEvents"] = eventsError;
        }

        if (errors.Count > 0)
        {
            return ValidationFailure(errors);
        }

        try
        {
            var config = await _configService.GetConfigAsync(GuildId, cancellationToken);
            if (request.Mode.HasValue)
            {
                config.Mode = request.Mode.Value;
            }
            if (request.SimplePreset != null)
            {
                config.SimplePreset = request.SimplePreset;
            }
            if (request.ModLogChannelId != null)
            {
                config.ModLogChannelId = modLogChannelId;
            }
            if (request.ModLogEvents.HasValue)
            {
                config.ModLogEvents = (ModLogEventKinds)request.ModLogEvents.Value;
            }

            await _configService.UpdateConfigAsync(GuildId, config, cancellationToken);

            _logger.LogInformation("Overview settings saved successfully for guild {GuildId}", GuildId);

            return new JsonResult(new
            {
                success = true,
                message = "Overview settings saved successfully.",
                mode = (int)config.Mode,
                modLogChannelId = config.ModLogChannelId?.ToString() ?? string.Empty,
                modLogEvents = (int)config.ModLogEvents
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save overview settings for guild {GuildId}", GuildId);
            return new JsonResult(new { success = false, message = "Failed to save overview settings." }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Handles POST requests to save spam detection settings. Only the fields sent change.
    /// </summary>
    public Task<IActionResult> OnPostSaveSpamAsync([FromBody] SpamConfigPatchDto request, CancellationToken cancellationToken)
        => SaveSectionAsync(request, "Spam detection", c => c.SpamConfig, cancellationToken);

    /// <summary>
    /// Handles POST requests to save content filter settings. Only the fields sent change.
    /// </summary>
    public Task<IActionResult> OnPostSaveContentAsync([FromBody] ContentFilterPatchDto request, CancellationToken cancellationToken)
        => SaveSectionAsync(request, "Content filter", c => c.ContentFilterConfig, cancellationToken);

    /// <summary>
    /// Handles POST requests to save raid protection settings. Only the fields sent change.
    /// </summary>
    public Task<IActionResult> OnPostSaveRaidAsync([FromBody] RaidProtectionPatchDto request, CancellationToken cancellationToken)
        => SaveSectionAsync(request, "Raid protection", c => c.RaidProtectionConfig, cancellationToken);

    /// <summary>
    /// Handles POST requests to apply a preset configuration. This replaces the spam, content filter
    /// and raid rules, including the blocklist.
    /// </summary>
    public async Task<IActionResult> OnPostApplyPresetAsync([FromBody] ApplyPresetDto request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying preset {PresetName} for guild {GuildId}", request?.PresetName, GuildId);

        if (request == null || !PresetNames.Contains(request.PresetName ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            return ValidationFailure(new Dictionary<string, string> { ["presetName"] = "Choose Relaxed, Moderate or Strict." });
        }

        try
        {
            var config = await _configService.ApplyPresetAsync(GuildId, request.PresetName, cancellationToken);

            _logger.LogInformation("Preset {PresetName} applied successfully for guild {GuildId}", request.PresetName, GuildId);

            return new JsonResult(new
            {
                success = true,
                message = $"Preset '{request.PresetName}' applied successfully.",
                config,
                activeRules = CalculateActiveRulesCount(config)
            });
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
        _logger.LogInformation("Creating tag {TagName} for guild {GuildId}", request?.Name, GuildId);

        if (request == null)
        {
            return Rejected("Nothing to save.");
        }

        request.Name = (request.Name ?? string.Empty).Trim();
        var errors = new Dictionary<string, string>();
        if (request.Name.Length == 0)
        {
            errors["name"] = "Give the tag a name.";
        }
        else if (request.Name.Length > 50)
        {
            errors["name"] = "Tag names can be up to 50 characters.";
        }
        if (!Enum.IsDefined(request.Category))
        {
            errors["category"] = "Choose a colour.";
        }
        if (errors.Count > 0)
        {
            return ValidationFailure(errors);
        }

        // The stored colour follows the category unless the caller sent one
        if (string.IsNullOrWhiteSpace(request.Color))
        {
            request.Color = ModTagStyle.DefaultColor(request.Category);
        }

        try
        {
            request.GuildId = GuildId;
            var tag = await _modTagService.CreateTagAsync(GuildId, request, cancellationToken);

            _logger.LogInformation("Tag {TagName} created successfully for guild {GuildId}", request.Name, GuildId);

            return new JsonResult(new
            {
                success = true,
                message = "Tag created successfully.",
                tag,
                cssClass = ModTagStyle.CssClass(tag.Category)
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonResult(new
            {
                success = false,
                message = $"A tag named \"{request.Name}\" already exists.",
                errors = new Dictionary<string, string> { ["name"] = "A tag with this name already exists." }
            })
            { StatusCode = 409 };
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
    /// Handles POST requests to import template tags. Templates the guild already has are skipped.
    /// The response lists the tags that were added so the page can show them without a reload.
    /// </summary>
    public async Task<IActionResult> OnPostImportTemplatesAsync([FromBody] string[] templateNames, CancellationToken cancellationToken)
    {
        templateNames ??= Array.Empty<string>();
        _logger.LogInformation("Importing {Count} template tags for guild {GuildId}", templateNames.Length, GuildId);

        if (templateNames.Length == 0)
        {
            return ValidationFailure(new Dictionary<string, string> { ["templates"] = "Choose at least one template tag." });
        }

        try
        {
            var before = (await _modTagService.GetGuildTagsAsync(GuildId, cancellationToken))
                .Select(t => t.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var count = await _modTagService.ImportTemplateTagsAsync(GuildId, templateNames, cancellationToken);

            var added = (await _modTagService.GetGuildTagsAsync(GuildId, cancellationToken))
                .Where(t => !before.Contains(t.Name))
                .Select(t => new { name = t.Name, userCount = t.UserCount, cssClass = ModTagStyle.CssClass(t.Category) })
                .ToList();

            _logger.LogInformation("{Count} template tags imported successfully for guild {GuildId}", count, GuildId);

            var message = count == 0
                ? "Those tags already exist, so nothing was added."
                : $"{count} template tags imported successfully.";

            return new JsonResult(new { success = true, message, count, tags = added });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import template tags for guild {GuildId}", GuildId);
            return new JsonResult(new { success = false, message = "Failed to import template tags." }) { StatusCode = 500 };
        }
    }

    /// <summary>
    /// Loads the saved config, applies only the fields the patch carries, and saves.
    /// </summary>
    private async Task<IActionResult> SaveSectionAsync<TPatch, TSection>(
        TPatch? request,
        string label,
        Func<GuildModerationConfigDto, TSection> section,
        CancellationToken cancellationToken)
        where TPatch : class, IModerationConfigPatch<TSection>
    {
        _logger.LogInformation("Saving {Section} settings for guild {GuildId}", label, GuildId);

        if (request == null)
        {
            return Rejected("Nothing to save.");
        }

        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return ValidationFailure(errors);
        }

        try
        {
            var config = await _configService.GetConfigAsync(GuildId, cancellationToken);
            request.ApplyTo(section(config));

            var updated = await _configService.UpdateConfigAsync(GuildId, config, cancellationToken);

            _logger.LogInformation("{Section} settings saved successfully for guild {GuildId}", label, GuildId);

            return new JsonResult(new
            {
                success = true,
                message = $"{label} settings saved successfully.",
                settings = section(updated ?? config),
                activeRules = CalculateActiveRulesCount(updated ?? config)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save {Section} settings for guild {GuildId}", label, GuildId);
            return new JsonResult(new { success = false, message = $"Failed to save {label.ToLowerInvariant()} settings." }) { StatusCode = 500 };
        }
    }

    private static IActionResult ValidationFailure(IReadOnlyDictionary<string, string> errors)
        => new JsonResult(new { success = false, message = "Fix the highlighted fields and try again.", errors }) { StatusCode = 400 };

    private static IActionResult Rejected(string message)
        => new JsonResult(new { success = false, message }) { StatusCode = 400 };

    private async Task LoadStatisticsAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddHours(-24);

        try
        {
            // Every event from the last day, whatever its status: the "pending" list would hide the
            // dismissed and actioned ones these numbers are about.
            var (events, _) = await _flaggedEventService.GetFilteredEventsAsync(
                guildId,
                new FlaggedEventQueryDto { DateFrom = since, Page = 1, PageSize = 1000 },
                cancellationToken);

            var recent = events.Where(e => e.CreatedAt >= since).ToList();

            EventsFlagged = recent.Count;
            ActionedEvents = recent.Count(e => e.Status == FlaggedEventStatus.Actioned);
            DismissedEvents = recent.Count(e => e.Status == FlaggedEventStatus.Dismissed);
            StatisticsLoaded = true;

            _logger.LogDebug("Loaded statistics for guild {GuildId}: Events={Events}, Actioned={Actioned}, Dismissed={Dismissed}",
                guildId, EventsFlagged, ActionedEvents, DismissedEvents);
        }
        catch (Exception ex)
        {
            StatisticsLoaded = false;
            _logger.LogWarning(ex, "Failed to load statistics for guild {GuildId}", guildId);
        }
    }

    /// <summary>The names the retired automod alert used to look for.</summary>
    public static bool LooksLikeModLog(string channelName) =>
        channelName.Contains("mod-log", StringComparison.OrdinalIgnoreCase)
        || channelName.Contains("mod-alert", StringComparison.OrdinalIgnoreCase);

    private static int CalculateActiveRulesCount(GuildModerationConfigDto config)
    {
        int count = 0;

        if (config.SpamConfig.Enabled) count++;
        if (config.ContentFilterConfig.Enabled) count++;
        if (config.RaidProtectionConfig.Enabled) count++;

        return count;
    }

    /// <summary>
    /// The tag templates the import dialog offers, each marked when the guild already has a tag by that name.
    /// </summary>
    public IEnumerable<(ModTagTemplates.TagTemplate Template, bool AlreadyImported)> TemplateOptions()
    {
        var existing = ViewModel.Tags.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ModTagTemplates.AllTemplates.Select(t => (t, existing.Contains(t.Name)));
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
