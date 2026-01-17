using Discord.WebSocket;
using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Page model for managing AI assistant configuration for a guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class AssistantSettingsModel : PageModel
{
    private readonly IAssistantGuildSettingsService _settingsService;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly IOptions<AssistantOptions> _assistantOptions;
    private readonly ISettingsService _globalSettingsService;
    private readonly ILogger<AssistantSettingsModel> _logger;

    public AssistantSettingsModel(
        IAssistantGuildSettingsService settingsService,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        IOptions<AssistantOptions> assistantOptions,
        ISettingsService globalSettingsService,
        ILogger<AssistantSettingsModel> logger)
    {
        _settingsService = settingsService;
        _guildService = guildService;
        _discordClient = discordClient;
        _assistantOptions = assistantOptions;
        _globalSettingsService = globalSettingsService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// Guild information for display.
    /// </summary>
    public GuildViewModel Guild { get; set; } = new();

    public GuildBreadcrumbViewModel Breadcrumb { get; set; } = new();
    public GuildHeaderViewModel Header { get; set; } = new();
    public GuildNavBarViewModel Navigation { get; set; } = new();

    /// <summary>
    /// List of available text channels in the guild.
    /// </summary>
    public List<ChannelSelectItem> AvailableChannels { get; set; } = new();

    /// <summary>
    /// Default rate limit from configuration.
    /// </summary>
    public int DefaultRateLimit { get; set; }

    /// <summary>
    /// Rate limit window in minutes from configuration.
    /// </summary>
    public int RateLimitWindowMinutes { get; set; }

    /// <summary>
    /// Whether the assistant feature is globally enabled.
    /// </summary>
    public bool GloballyEnabled { get; set; }

    /// <summary>
    /// Error message to display.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Success message from TempData.
    /// </summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Input model for form binding.
    /// </summary>
    public class InputModel
    {
        public ulong GuildId { get; set; }

        [Display(Name = "Enable AI Assistant")]
        public bool IsEnabled { get; set; }

        [Display(Name = "Allowed Channels")]
        public List<string> AllowedChannelIds { get; set; } = new();

        [Display(Name = "Rate Limit Override")]
        [Range(1, 100, ErrorMessage = "Rate limit must be between 1 and 100")]
        public int? RateLimitOverride { get; set; }
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

    /// <summary>
    /// Model for channel selection.
    /// </summary>
    public class ChannelSelectItem
    {
        public ulong Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Position { get; set; }
        public string Type { get; set; } = "Text";
        public bool IsSelected { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing assistant settings page for guild {GuildId}", guildId);

        // Get guild info
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        Guild = new GuildViewModel
        {
            Id = guild.Id,
            Name = guild.Name,
            IconUrl = guild.IconUrl
        };

        // Populate guild layout ViewModels
        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, Url = $"/Guilds/Details/{guild.Id}" },
                new() { Label = "AI Assistant Settings", IsCurrent = true }
            }
        };

        Header = new GuildHeaderViewModel
        {
            GuildId = guild.Id,
            GuildName = guild.Name,
            GuildIconUrl = guild.IconUrl,
            PageTitle = "AI Assistant Settings",
            PageDescription = $"Configure AI assistant for {guild.Name}",
            Actions = new List<HeaderAction>
            {
                new()
                {
                    Label = "View Metrics",
                    Url = $"/Guilds/AssistantMetrics/{guildId}",
                    Style = HeaderActionStyle.Link,
                    Icon = "M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z"
                }
            }
        };

        Navigation = new GuildNavBarViewModel
        {
            GuildId = guild.Id,
            ActiveTab = "assistant",
            Tabs = GuildNavigationConfig.GetTabs().ToList()
        };

        // Get assistant settings
        var settings = await _settingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);
        var allowedChannels = settings.GetAllowedChannelIdsList();

        // Get available channels
        AvailableChannels = GetTextChannels(guildId, allowedChannels);

        // Load configuration defaults
        DefaultRateLimit = _assistantOptions.Value.DefaultRateLimit;
        RateLimitWindowMinutes = _assistantOptions.Value.RateLimitWindowMinutes;

        // Read GloballyEnabled from settings service (respects runtime changes from Settings page)
        GloballyEnabled = await _globalSettingsService.GetSettingValueAsync<bool>("Assistant:GloballyEnabled", cancellationToken);

        // Populate form
        Input = new InputModel
        {
            GuildId = guildId,
            IsEnabled = settings.IsEnabled,
            AllowedChannelIds = allowedChannels.Select(id => id.ToString()).ToList(),
            RateLimitOverride = settings.RateLimitOverride
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("POST received for assistant settings - GuildId={GuildId}, IsEnabled={IsEnabled}",
            Input.GuildId, Input.IsEnabled);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState is invalid for guild {GuildId}. Errors: {Errors}",
                Input.GuildId,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            await LoadViewModelAsync(Input.GuildId, cancellationToken);
            return Page();
        }

        // Get current settings
        var settings = await _settingsService.GetOrCreateSettingsAsync(Input.GuildId, cancellationToken);

        // Update settings
        settings.IsEnabled = Input.IsEnabled;
        settings.RateLimitOverride = Input.RateLimitOverride;

        // Parse channel IDs
        var channelIds = new List<ulong>();
        foreach (var channelIdStr in Input.AllowedChannelIds ?? new List<string>())
        {
            if (ulong.TryParse(channelIdStr, out var channelId))
            {
                channelIds.Add(channelId);
            }
        }
        settings.SetAllowedChannelIdsList(channelIds);

        // Save settings
        await _settingsService.UpdateSettingsAsync(settings, cancellationToken);

        _logger.LogInformation("Successfully updated assistant settings for guild {GuildId}", Input.GuildId);
        SuccessMessage = "Assistant settings saved successfully.";

        return RedirectToPage("AssistantSettings", new { guildId = Input.GuildId });
    }

    /// <summary>
    /// Gets text channels from the Discord guild.
    /// </summary>
    private List<ChannelSelectItem> GetTextChannels(ulong guildId, List<ulong> selectedChannelIds)
    {
        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogWarning("Could not fetch Discord guild {GuildId} from client", guildId);
            return new List<ChannelSelectItem>();
        }

        var channels = new List<ChannelSelectItem>();

        // Add text channels
        foreach (var channel in guild.TextChannels.Where(c => c != null))
        {
            var type = channel is SocketNewsChannel ? "Announcement" : "Text";
            channels.Add(new ChannelSelectItem
            {
                Id = channel.Id,
                Name = channel.Name,
                Position = channel.Position,
                Type = type,
                IsSelected = selectedChannelIds.Contains(channel.Id)
            });
        }

        return channels.OrderBy(c => c.Position).ToList();
    }

    /// <summary>
    /// Loads the view model for redisplay after validation error.
    /// </summary>
    private async Task LoadViewModelAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild != null)
        {
            Guild = new GuildViewModel
            {
                Id = guild.Id,
                Name = guild.Name,
                IconUrl = guild.IconUrl
            };
        }

        var settings = await _settingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);
        var allowedChannels = settings.GetAllowedChannelIdsList();
        AvailableChannels = GetTextChannels(guildId, allowedChannels);

        DefaultRateLimit = _assistantOptions.Value.DefaultRateLimit;
        RateLimitWindowMinutes = _assistantOptions.Value.RateLimitWindowMinutes;

        // Read GloballyEnabled from settings service (respects runtime changes from Settings page)
        GloballyEnabled = await _globalSettingsService.GetSettingValueAsync<bool>("Assistant:GloballyEnabled", cancellationToken);
    }
}
