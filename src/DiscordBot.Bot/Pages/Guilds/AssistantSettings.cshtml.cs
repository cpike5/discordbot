using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models.Llm;
using Discord;
using Discord.WebSocket;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Page model for managing AI assistant configuration for a guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class AssistantSettingsModel : GuildPageModelBase
{
    private readonly IAssistantGuildSettingsService _settingsService;
    private readonly IGuildService _guildService;
    private readonly IDiscordChannelResolver _channelResolver;
    private readonly IOptions<AssistantOptions> _assistantOptions;
    private readonly ISettingsService _globalSettingsService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<AssistantSettingsModel> _logger;

    public AssistantSettingsModel(
        IAssistantGuildSettingsService settingsService,
        IGuildService guildService,
        IDiscordChannelResolver channelResolver,
        IOptions<AssistantOptions> assistantOptions,
        ISettingsService globalSettingsService,
        DiscordSocketClient discordClient,
        ILogger<AssistantSettingsModel> logger)
    {
        _settingsService = settingsService;
        _guildService = guildService;
        _channelResolver = channelResolver;
        _assistantOptions = assistantOptions;
        _globalSettingsService = globalSettingsService;
        _discordClient = discordClient;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// Guild display information.
    /// </summary>
    public GuildViewModel Guild { get; set; } = new();

    /// <summary>
    /// List of available text channels in the guild.
    /// </summary>
    public List<ChannelSelectItem> AvailableChannels { get; set; } = new();

    /// <summary>
    /// The guild's tool allow-list, grouped by category, with the current selection.
    /// </summary>
    public List<ToolCategoryGroup> ToolCategories { get; set; } = new();

    /// <summary>
    /// Whether the guild has no saved tool selection and so uses the default set.
    /// </summary>
    public bool UsingDefaultToolSet { get; set; }

    /// <summary>
    /// Gets the default rate limit from configuration.
    /// </summary>
    public int DefaultRateLimit { get; set; }

    /// <summary>
    /// Gets the rate limit window in minutes from configuration.
    /// </summary>
    public int RateLimitWindowMinutes { get; set; }

    /// <summary>
    /// Whether the assistant is globally enabled (from the runtime settings).
    /// </summary>
    public bool GloballyEnabled { get; set; }

    /// <summary>
    /// Allowed channels (or every text channel, when none is chosen) where the bot cannot open a
    /// thread or post in one. Shown under the Threads option; saving is still allowed, the handler
    /// answers in the channel for those.
    /// </summary>
    public List<string> ThreadPermissionGaps { get; set; } = new();

    /// <summary>
    /// Input model for form binding with validation attributes.
    /// </summary>
    public class InputModel
    {
        [Display(Name = "Enable AI Assistant")]
        public bool IsEnabled { get; set; }

        [Display(Name = "Allowed Channels")]
        public List<string> AllowedChannelIds { get; set; } = new();

        [Display(Name = "Rate Limit Override")]
        [Range(1, 100, ErrorMessage = "The rate limit must be a whole number from 1 to 100.")]
        public int? RateLimitOverride { get; set; }

        [Display(Name = "Enabled Tools")]
        public List<string> EnabledTools { get; set; } = new();

        [Display(Name = "Conversation mode")]
        public AssistantConversationMode ConversationMode { get; set; } = AssistantConversationMode.SingleReply;
    }

    public class ToolCategoryGroup
    {
        public string Category { get; set; } = string.Empty;
        public List<ToolSelectItem> Tools { get; set; } = new();
    }

    public class ToolSelectItem
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }

    public class GuildViewModel
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? IconUrl { get; set; }
    }

    public class ChannelSelectItem
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Position { get; set; }
        public string Type { get; set; } = "Text";
        public bool IsSelected { get; set; }

        /// <summary>True for a saved channel the bot can no longer see (deleted, or not visible to it).</summary>
        public bool IsMissing { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing assistant settings page for guild {GuildId}", guildId);

        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        var settings = await _settingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);

        Input = new InputModel
        {
            IsEnabled = settings.IsEnabled,
            AllowedChannelIds = settings.GetAllowedChannelIdsList().Select(id => id.ToString()).ToList(),
            RateLimitOverride = settings.RateLimitOverride,
            EnabledTools = settings.GetEnabledToolsList(),
            ConversationMode = settings.ConversationMode
        };

        await LoadPageAsync(guild.Id, guild.Name, guild.IconUrl, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("POST received for assistant settings - GuildId={GuildId}, IsEnabled={IsEnabled}",
            guildId, Input.IsEnabled);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Assistant settings for guild {GuildId} are invalid. Errors: {Errors}",
                guildId,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
            if (guild == null)
            {
                return NotFound();
            }

            // What the user ticked and typed is shown again, not what is saved
            await LoadPageAsync(guild.Id, guild.Name, guild.IconUrl, cancellationToken);
            return Page();
        }

        // The guild is not read on this path: loading it into the same context as the settings'
        // own guild reference makes the save fail with a duplicate-tracking error.
        var settings = await _settingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);

        settings.IsEnabled = Input.IsEnabled;
        settings.RateLimitOverride = Input.RateLimitOverride;
        settings.ConversationMode = Enum.IsDefined(Input.ConversationMode)
            ? Input.ConversationMode
            : AssistantConversationMode.SingleReply;

        var channelIds = new List<ulong>();
        foreach (var channelIdStr in Input.AllowedChannelIds ?? new List<string>())
        {
            if (ulong.TryParse(channelIdStr, out var channelId))
            {
                channelIds.Add(channelId);
            }
        }
        settings.SetAllowedChannelIdsList(channelIds);

        // Drops anything outside the guild checklist, and stores "exactly the defaults" as empty so
        // saving the page untouched leaves the guild on the default set rather than pinning it to
        // today's members of that set. See ToolCatalog.NormalizeSelection.
        settings.SetEnabledToolsList(ToolCatalog.NormalizeSelection(Input.EnabledTools, ToolScopes.Guild));

        await _settingsService.UpdateSettingsAsync(settings, cancellationToken);

        _logger.LogInformation("Successfully updated assistant settings for guild {GuildId}", guildId);
        TempData.SetSuccessToast("Assistant settings saved.");

        return RedirectToPage("AssistantSettings", new { guildId });
    }

    /// <summary>
    /// Everything the view needs besides <see cref="Input"/>: layout chrome, the channel list, the
    /// tool checklist and the configuration defaults. Shared by GET and every failed POST. The
    /// selection shown is always <see cref="Input"/>'s, so a failed save keeps what was ticked.
    /// </summary>
    private async Task LoadPageAsync(ulong guildId, string guildName, string? guildIconUrl, CancellationToken cancellationToken)
    {
        Guild = new GuildViewModel { Id = guildId, Name = guildName, IconUrl = guildIconUrl };

        var selectedChannels = (Input.AllowedChannelIds ?? new List<string>())
            .Select(id => ulong.TryParse(id, out var parsed) ? parsed : (ulong?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
        AvailableChannels = GetTextChannels(guildId, selectedChannels);

        var enabledTools = Input.EnabledTools ?? new List<string>();
        UsingDefaultToolSet = enabledTools.Count == 0;
        ToolCategories = BuildToolCategories(enabledTools);

        ThreadPermissionGaps = FindThreadPermissionGaps(guildId, selectedChannels);

        DefaultRateLimit = _assistantOptions.Value.RateLimits.DefaultRateLimit;
        RateLimitWindowMinutes = _assistantOptions.Value.RateLimits.RateLimitWindowMinutes;

        // Read GloballyEnabled from settings service (respects runtime changes from Settings page)
        GloballyEnabled = await _globalSettingsService.GetSettingValueAsync<bool>("Assistant:GloballyEnabled", cancellationToken);

        PopulateGuildLayout(guildId, guildName, guildIconUrl, "assistant",
            "AI Assistant Settings", $"Configure AI assistant for {guildName}");
        Header.Actions = new List<HeaderAction>
        {
            new()
            {
                Label = "View Metrics",
                Url = $"/Guilds/AssistantMetrics/{guildId}",
                Style = HeaderActionStyle.Link,
                Icon = "M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"
            }
        };
    }

    private List<ChannelSelectItem> GetTextChannels(ulong guildId, List<ulong> selectedChannelIds)
    {
        var channels = _channelResolver.GetTextChannels(guildId)
            .Where(c => c.Type == ChannelDisplayType.Text || c.Type == ChannelDisplayType.Announcement)
            .Select(c => new ChannelSelectItem
            {
                Id = c.Id,
                Name = c.Name,
                Position = c.Position,
                Type = c.Type == ChannelDisplayType.Announcement ? "Announcement" : "Text",
                IsSelected = selectedChannelIds.Contains(c.Id)
            })
            .ToList();

        // A saved channel the bot cannot see any more is still listed (and ticked), so saving the
        // page does not quietly drop it; the admin can untick it on purpose.
        var visible = channels.Select(c => c.Id).ToHashSet();
        channels.AddRange(selectedChannelIds
            .Where(id => !visible.Contains(id))
            .Distinct()
            .Select(id => new ChannelSelectItem
            {
                Id = id,
                Name = $"Unknown channel ({id})",
                Type = "Not found",
                IsSelected = true,
                IsMissing = true
            }));

        return channels;
    }

    /// <summary>
    /// The names of channels where thread mode would fall back to a channel reply because the bot
    /// lacks Create Public Threads or Send Messages in Threads. Empty while the bot cannot see the
    /// guild (offline mode), since nothing can be checked.
    /// </summary>
    private List<string> FindThreadPermissionGaps(ulong guildId, List<ulong> selectedChannelIds)
    {
        var guild = _discordClient.GetGuild(guildId);
        if (guild?.CurrentUser is null)
        {
            return new List<string>();
        }

        var channels = selectedChannelIds.Count == 0
            ? guild.TextChannels.AsEnumerable()
            : selectedChannelIds.Select(guild.GetTextChannel).Where(c => c is not null)!;

        return channels
            .Where(c => !CanHostAssistantThread(guild.CurrentUser.GetPermissions(c)))
            .OrderBy(c => c.Position)
            .Select(c => c.Name)
            .ToList();
    }

    /// <summary>What a channel needs for the bot to open a thread there and answer in it.</summary>
    public static bool CanHostAssistantThread(ChannelPermissions permissions) =>
        permissions.CreatePublicThreads && permissions.SendMessagesInThreads;

    private static List<ToolCategoryGroup> BuildToolCategories(List<string> enabledTools)
    {
        var selected = enabledTools.Count > 0
            ? enabledTools.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : ToolCatalog.DefaultsForScope(ToolScopes.Guild).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return ToolCatalog.ForScope(ToolScopes.Guild)
            .GroupBy(t => t.Category, StringComparer.Ordinal)
            .Select(g => new ToolCategoryGroup
            {
                Category = g.Key,
                Tools = g.Select(t => new ToolSelectItem
                {
                    Name = t.Name,
                    DisplayName = t.DisplayName,
                    Description = t.Description,
                    IsSelected = selected.Contains(t.Name)
                }).ToList()
            })
            .ToList();
    }
}
