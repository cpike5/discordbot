using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Page model for managing welcome configuration for a guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class WelcomeModel : PageModel
{
    private readonly IWelcomeService _welcomeService;
    private readonly IGuildService _guildService;
    private readonly IDiscordChannelResolver _channelResolver;
    private readonly ILogger<WelcomeModel> _logger;

    public WelcomeModel(
        IWelcomeService welcomeService,
        IGuildService guildService,
        IDiscordChannelResolver channelResolver,
        ILogger<WelcomeModel> logger)
    {
        _welcomeService = welcomeService;
        _guildService = guildService;
        _channelResolver = channelResolver;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// View model for display-only properties (guild info, available channels).
    /// </summary>
    public WelcomeConfigurationViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// Guild layout breadcrumb ViewModel.
    /// </summary>
    public GuildBreadcrumbViewModel Breadcrumb { get; set; } = new();

    /// <summary>
    /// Guild layout header ViewModel.
    /// </summary>
    public GuildHeaderViewModel Header { get; set; } = new();

    /// <summary>
    /// Guild layout navigation ViewModel.
    /// </summary>
    public GuildNavBarViewModel Navigation { get; set; } = new();

    /// <summary>
    /// Error message to display on the page.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Success message from TempData.
    /// </summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// List of available text channels in the guild.
    /// </summary>
    public List<ChannelSelectItem> AvailableChannels { get; set; } = new();

    /// <summary>
    /// Input model for form binding with validation attributes.
    /// </summary>
    public class InputModel
    {
        public ulong GuildId { get; set; }

        [Display(Name = "Enable Welcome Messages")]
        public bool IsEnabled { get; set; }

        [Display(Name = "Welcome Channel")]
        public ulong? WelcomeChannelId { get; set; }

        [StringLength(2000, ErrorMessage = "Welcome message cannot exceed 2000 characters")]
        [Display(Name = "Welcome Message")]
        public string? WelcomeMessage { get; set; }

        [Display(Name = "Include User Avatar")]
        public bool IncludeAvatar { get; set; }

        [Display(Name = "Use Embed")]
        public bool UseEmbed { get; set; }

        [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Embed color must be a valid hex color (e.g., #5865F2)")]
        [Display(Name = "Embed Color")]
        public string? EmbedColor { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing welcome configuration page for guild {GuildId}", guildId);

        // Get guild info from service
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        // Get welcome configuration (may be null if not configured yet)
        var welcomeConfig = await _welcomeService.GetConfigurationAsync(guildId, cancellationToken);

        // Get available text channels from Discord
        AvailableChannels = _channelResolver.GetTextChannels(guildId)
            .Select(ChannelSelectItem.FromChannelInfo).ToList();

        // If no configuration exists, create default values
        if (welcomeConfig == null)
        {
            _logger.LogDebug("No welcome configuration found for guild {GuildId}, using defaults", guildId);
            welcomeConfig = new WelcomeConfigurationDto
            {
                GuildId = guildId,
                IsEnabled = false,
                WelcomeMessage = "Welcome to {server}, {user}! You are member #{memberCount}.",
                IncludeAvatar = true,
                UseEmbed = true,
                EmbedColor = "#5865F2"
            };
        }

        // Populate view model
        ViewModel = WelcomeConfigurationViewModel.FromDto(
            welcomeConfig,
            guild.Name,
            guild.IconUrl,
            AvailableChannels);

        // Populate form input model
        Input = new InputModel
        {
            GuildId = welcomeConfig.GuildId,
            IsEnabled = welcomeConfig.IsEnabled,
            WelcomeChannelId = welcomeConfig.WelcomeChannelId,
            WelcomeMessage = welcomeConfig.WelcomeMessage,
            IncludeAvatar = welcomeConfig.IncludeAvatar,
            UseEmbed = welcomeConfig.UseEmbed,
            EmbedColor = welcomeConfig.EmbedColor
        };

        // Populate guild layout ViewModels
        Breadcrumb = new GuildBreadcrumbViewModel
        {
            Items = new List<BreadcrumbItem>
            {
                new() { Label = "Home", Url = "/" },
                new() { Label = "Servers", Url = "/Guilds" },
                new() { Label = guild.Name, Url = $"/Guilds/Details/{guild.Id}" },
                new() { Label = "Welcome Settings", IsCurrent = true }
            }
        };

        Header = new GuildHeaderViewModel
        {
            GuildId = guild.Id,
            GuildName = guild.Name,
            GuildIconUrl = guild.IconUrl,
            PageTitle = "Welcome Settings",
            PageDescription = $"Configure automatic welcome messages for {guild.Name}"
        };

        Navigation = new GuildNavBarViewModel
        {
            GuildId = guild.Id,
            ActiveTab = "welcome",
            Tabs = GuildNavigationConfig.GetTabs().ToList()
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        _logger.LogWarning("POST RECEIVED - GuildId={GuildId}, IsEnabled={IsEnabled}, ChannelId={ChannelId}, UseEmbed={UseEmbed}",
            Input.GuildId, Input.IsEnabled, Input.WelcomeChannelId, Input.UseEmbed);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState is invalid for guild {GuildId}. Errors: {Errors}",
                Input.GuildId,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            await LoadViewModelAsync(Input.GuildId, cancellationToken);
            return Page();
        }

        // Validate that if enabled, a channel is selected
        if (Input.IsEnabled && !Input.WelcomeChannelId.HasValue)
        {
            ModelState.AddModelError("Input.WelcomeChannelId", "A welcome channel must be selected when welcome messages are enabled.");
            await LoadViewModelAsync(Input.GuildId, cancellationToken);
            return Page();
        }

        // Validate that if UseEmbed is true and EmbedColor is provided, it's a valid hex color
        if (Input.UseEmbed && !string.IsNullOrWhiteSpace(Input.EmbedColor))
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(Input.EmbedColor, @"^#[0-9A-Fa-f]{6}$"))
            {
                ModelState.AddModelError("Input.EmbedColor", "Embed color must be a valid hex color (e.g., #5865F2)");
                await LoadViewModelAsync(Input.GuildId, cancellationToken);
                return Page();
            }
        }

        // Create the update request
        var updateRequest = new WelcomeConfigurationUpdateDto
        {
            IsEnabled = Input.IsEnabled,
            WelcomeChannelId = Input.WelcomeChannelId,
            WelcomeMessage = Input.WelcomeMessage,
            IncludeAvatar = Input.IncludeAvatar,
            UseEmbed = Input.UseEmbed,
            EmbedColor = Input.EmbedColor
        };

        var result = await _welcomeService.UpdateConfigurationAsync(Input.GuildId, updateRequest, cancellationToken);

        if (result == null)
        {
            _logger.LogWarning("Failed to update welcome configuration for guild {GuildId} - guild not found", Input.GuildId);
            ErrorMessage = "Guild not found. It may have been removed.";
            await LoadViewModelAsync(Input.GuildId, cancellationToken);
            return Page();
        }

        _logger.LogInformation("Successfully updated welcome configuration for guild {GuildId}", Input.GuildId);
        SuccessMessage = "Welcome configuration saved successfully.";

        return RedirectToPage("Welcome", new { guildId = Input.GuildId });
    }

    /// <summary>
    /// Loads the view model for redisplay after validation error.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task LoadViewModelAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild != null)
        {
            AvailableChannels = _channelResolver.GetTextChannels(guildId)
            .Select(ChannelSelectItem.FromChannelInfo).ToList();

            // Get current configuration or use defaults
            var welcomeConfig = await _welcomeService.GetConfigurationAsync(guildId, cancellationToken);
            if (welcomeConfig == null)
            {
                welcomeConfig = new WelcomeConfigurationDto
                {
                    GuildId = guildId,
                    IsEnabled = false,
                    WelcomeMessage = "Welcome to {server}, {user}! You are member #{memberCount}.",
                    IncludeAvatar = true,
                    UseEmbed = true,
                    EmbedColor = "#5865F2"
                };
            }

            ViewModel = WelcomeConfigurationViewModel.FromDto(
                welcomeConfig,
                guild.Name,
                guild.IconUrl,
                AvailableChannels);

            // Preserve form input values for redisplay
            ViewModel.IsEnabled = Input.IsEnabled;
            ViewModel.WelcomeChannelId = Input.WelcomeChannelId;
            ViewModel.WelcomeMessage = Input.WelcomeMessage ?? string.Empty;
            ViewModel.IncludeAvatar = Input.IncludeAvatar;
            ViewModel.UseEmbed = Input.UseEmbed;
            ViewModel.EmbedColor = Input.EmbedColor;
        }
    }
}
