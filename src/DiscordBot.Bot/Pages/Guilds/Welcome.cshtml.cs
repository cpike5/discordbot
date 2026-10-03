using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Bot.Pages.Guilds;

/// <summary>
/// Page model for managing welcome configuration for a guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class WelcomeModel : GuildPageModelBase
{
    /// <summary>The message a new, unconfigured guild starts with.</summary>
    public const string DefaultWelcomeMessage = "Welcome to {server}, {user}! You are member #{memberCount}.";

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
    /// List of available text channels in the guild.
    /// </summary>
    public List<ChannelSelectItem> AvailableChannels { get; set; } = new();

    /// <summary>
    /// Input model for form binding with validation attributes.
    /// </summary>
    public class InputModel
    {
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

        [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Embed color must be a valid hex color (for example #5865F2)")]
        [Display(Name = "Embed Color")]
        public string? EmbedColor { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing welcome configuration page for guild {GuildId}", guildId);

        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound();
        }

        // Get welcome configuration (may be null if not configured yet)
        var welcomeConfig = await _welcomeService.GetConfigurationAsync(guildId, cancellationToken)
                            ?? DefaultConfiguration(guildId);

        Input = new InputModel
        {
            IsEnabled = welcomeConfig.IsEnabled,
            WelcomeChannelId = welcomeConfig.WelcomeChannelId,
            WelcomeMessage = welcomeConfig.WelcomeMessage,
            IncludeAvatar = welcomeConfig.IncludeAvatar,
            UseEmbed = welcomeConfig.UseEmbed,
            EmbedColor = welcomeConfig.EmbedColor
        };

        LoadPage(guild.Id, guild.Name, guild.IconUrl);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Welcome configuration submitted for guild {GuildId}", guildId);

        var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
        if (guild == null)
        {
            return NotFound();
        }

        // Validate that if enabled, a channel is selected
        if (Input.IsEnabled && !Input.WelcomeChannelId.HasValue)
        {
            ModelState.AddModelError("Input.WelcomeChannelId", "Choose a welcome channel before turning welcome messages on.");
        }

        // The embed colour only matters when an embed is sent, but a malformed one is rejected by
        // the attribute either way; clear a blank one so it does not fail the pattern.
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Welcome configuration for guild {GuildId} is invalid. Errors: {Errors}",
                guildId,
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)));

            LoadPage(guild.Id, guild.Name, guild.IconUrl);
            return Page();
        }

        var updateRequest = new WelcomeConfigurationUpdateDto
        {
            IsEnabled = Input.IsEnabled,
            WelcomeChannelId = Input.WelcomeChannelId,
            WelcomeMessage = Input.WelcomeMessage,
            IncludeAvatar = Input.IncludeAvatar,
            UseEmbed = Input.UseEmbed,
            EmbedColor = Input.EmbedColor
        };

        var result = await _welcomeService.UpdateConfigurationAsync(guildId, updateRequest, cancellationToken);

        if (result == null)
        {
            _logger.LogWarning("Failed to update welcome configuration for guild {GuildId} - guild not found", guildId);
            TempData.SetErrorToast("The server was not found. It may have been removed.");
            LoadPage(guild.Id, guild.Name, guild.IconUrl);
            return Page();
        }

        _logger.LogInformation("Successfully updated welcome configuration for guild {GuildId}", guildId);
        TempData.SetSuccessToast("Welcome settings saved.");

        return RedirectToPage("Welcome", new { guildId });
    }

    /// <summary>
    /// Everything the view needs besides <see cref="Input"/>: the layout chrome, the channel list and
    /// the display view model. Shared by GET and every failed POST so a re-rendered form keeps its
    /// header, navigation and breadcrumb.
    /// </summary>
    private void LoadPage(ulong guildId, string guildName, string? guildIconUrl)
    {
        AvailableChannels = _channelResolver.GetTextChannels(guildId)
            .Select(ChannelSelectItem.FromChannelInfo).ToList();

        ViewModel = new WelcomeConfigurationViewModel
        {
            GuildId = guildId,
            GuildName = guildName,
            GuildIconUrl = guildIconUrl,
            IsEnabled = Input.IsEnabled,
            WelcomeChannelId = Input.WelcomeChannelId,
            WelcomeMessage = Input.WelcomeMessage ?? string.Empty,
            IncludeAvatar = Input.IncludeAvatar,
            UseEmbed = Input.UseEmbed,
            EmbedColor = Input.EmbedColor,
            AvailableChannels = AvailableChannels
        };

        PopulateGuildLayout(guildId, guildName, guildIconUrl, "welcome",
            "Welcome Settings", $"Configure automatic welcome messages for {guildName}");
    }

    private static WelcomeConfigurationDto DefaultConfiguration(ulong guildId) => new()
    {
        GuildId = guildId,
        IsEnabled = false,
        WelcomeMessage = DefaultWelcomeMessage,
        IncludeAvatar = true,
        UseEmbed = true,
        EmbedColor = "#5865F2"
    };
}
