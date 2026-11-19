using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Page model for the not-X (tweet link preview) settings of one guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class NotXModel : GuildPageModelBase
{
    /// <summary>The application-settings key of the hot global toggle on the Features tab.</summary>
    public const string GlobalEnabledSettingKey = "Features:NotXEnabled";

    private readonly INotXService _notXService;
    private readonly IGuildService _guildService;
    private readonly IDiscordChannelResolver _channelResolver;
    private readonly IAuditLogService _auditLogService;
    private readonly ISettingsService _settingsService;
    private readonly NotXOptions _options;
    private readonly ILogger<NotXModel> _logger;

    public NotXModel(
        INotXService notXService,
        IGuildService guildService,
        IDiscordChannelResolver channelResolver,
        IAuditLogService auditLogService,
        ISettingsService settingsService,
        IOptions<NotXOptions> options,
        ILogger<NotXModel> logger)
    {
        _notXService = notXService;
        _guildService = guildService;
        _channelResolver = channelResolver;
        _auditLogService = auditLogService;
        _settingsService = settingsService;
        _options = options.Value;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>The guild's text channels, for the output and monitored-channel pickers.</summary>
    public List<ChannelSelectItem> AvailableChannels { get; set; } = new();

    public string GuildName { get; set; } = string.Empty;

    /// <summary>True when previews are switched off bot-wide (configuration or the Features toggle).</summary>
    public bool GloballyDisabled { get; set; }

    /// <summary>Form input.</summary>
    public class InputModel
    {
        [Display(Name = "Enable not-X previews")]
        public bool IsEnabled { get; set; }

        [Display(Name = "Only preview sensitive tweets")]
        public bool SensitiveOnly { get; set; } = true;

        [Display(Name = "Hide the sensitive label")]
        public bool HideSensitiveLabel { get; set; }

        [Display(Name = "Output channel")]
        public ulong? OutputChannelId { get; set; }

        [Display(Name = "Monitored channels")]
        public List<ulong> MonitoredChannelIds { get; set; } = new();
    }

    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        // Creates the default row (disabled, sensitive-only) when the guild has none; that is the
        // service's only read, and it is harmless: the defaults are what the page shows anyway.
        var settings = await _notXService.GetOrCreateSettingsAsync(guildId);

        Input = new InputModel
        {
            IsEnabled = settings.IsEnabled,
            SensitiveOnly = settings.SensitiveOnly,
            HideSensitiveLabel = settings.HideSensitiveLabel,
            OutputChannelId = settings.OutputChannelId,
            MonitoredChannelIds = settings.GetMonitoredChannelIds()
        };

        await LoadPageAsync(guild.Id, guild.Name, guild.IconUrl, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await LoadPageAsync(guild.Id, guild.Name, guild.IconUrl, cancellationToken);
            return Page();
        }

        var settings = await _notXService.GetOrCreateSettingsAsync(guildId);
        var before = Snapshot(settings);

        var channels = _channelResolver.GetTextChannels(guildId);
        var validIds = channels.Select(c => c.Id).ToHashSet();

        // While the bot cannot see the guild's channels at all (offline, or the guild is
        // unavailable), nothing can be checked: keep what is already saved rather than wipe it.
        if (validIds.Count == 0)
        {
            if (settings.OutputChannelId.HasValue) validIds.Add(settings.OutputChannelId.Value);
            foreach (var id in settings.GetMonitoredChannelIds()) validIds.Add(id);
        }

        var outputChannelId = Input.OutputChannelId is { } o && validIds.Contains(o) ? o : (ulong?)null;
        var monitored = Input.MonitoredChannelIds.Where(validIds.Contains).Distinct().ToList();

        var dropped = (Input.OutputChannelId.HasValue && outputChannelId == null ? 1 : 0)
                      + Input.MonitoredChannelIds.Distinct().Count() - monitored.Count;
        if (dropped > 0)
        {
            _logger.LogWarning("Dropped {Count} channel ID(s) that are not text channels of guild {GuildId}", dropped, guildId);
        }

        settings.IsEnabled = Input.IsEnabled;
        settings.SensitiveOnly = Input.SensitiveOnly;
        settings.HideSensitiveLabel = Input.HideSensitiveLabel;
        settings.OutputChannelId = outputChannelId;
        // An empty list means "monitor every channel".
        settings.SetMonitoredChannelIds(monitored);

        await _notXService.UpdateSettingsAsync(settings);

        await _auditLogService.CreateBuilder()
            .ForCategory(AuditLogCategory.Guild)
            .WithAction(AuditLogAction.SettingChanged)
            .ByUser(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown")
            .InGuild(guildId)
            .OnTarget("NotXGuildSettings", guildId.ToString())
            .WithDetails(new { source = "portal", before, after = Snapshot(settings) })
            .LogAsync(cancellationToken);

        _logger.LogInformation("Updated not-X settings for guild {GuildId}", guildId);
        TempData.SetSuccessToast("not-X settings saved.");

        return RedirectToPage("NotX", new { guildId });
    }

    private static object Snapshot(NotXGuildSettings s) => new
    {
        isEnabled = s.IsEnabled,
        sensitiveOnly = s.SensitiveOnly,
        hideSensitiveLabel = s.HideSensitiveLabel,
        outputChannelId = s.OutputChannelId?.ToString(),
        monitoredChannelIds = s.GetMonitoredChannelIds().Select(i => i.ToString()).ToArray()
    };

    private async Task LoadPageAsync(ulong guildId, string guildName, string? guildIconUrl, CancellationToken cancellationToken)
    {
        GuildName = guildName;
        AvailableChannels = _channelResolver.GetTextChannels(guildId)
            .Select(ChannelSelectItem.FromChannelInfo).ToList();

        var featureOn = await _settingsService.GetSettingValueAsync<bool?>(GlobalEnabledSettingKey, cancellationToken) ?? true;
        GloballyDisabled = !_options.Enabled || !featureOn;

        PopulateGuildLayout(guildId, guildName, guildIconUrl, "notx",
            "not-X Settings", $"Tweet link previews for {guildName}");
    }
}
