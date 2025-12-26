using Discord.WebSocket;
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
public class WelcomeModel : PageModel
{
    private readonly IWelcomeService _welcomeService;
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<WelcomeModel> _logger;

    public WelcomeModel(
        IWelcomeService welcomeService,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        ILogger<WelcomeModel> logger)
    {
        _welcomeService = welcomeService;
        _guildService = guildService;
        _discordClient = discordClient;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    /// <summary>
    /// View model for display-only properties (guild info, available channels).
    /// </summary>
    public WelcomeConfigurationViewModel ViewModel { get; set; } = new();

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

    public async Task<IActionResult> OnGetAsync(ulong id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("User accessing welcome configuration page for guild {GuildId}", id);

        // Get guild info from service
        var guild = await _guildService.GetGuildByIdAsync(id, cancellationToken);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", id);
            return NotFound();
        }

        // Get welcome configuration (may be null if not configured yet)
        var welcomeConfig = await _welcomeService.GetConfigurationAsync(id, cancellationToken);

        // Get available text channels from Discord
        AvailableChannels = GetTextChannels(id);

        // If no configuration exists, create default values
        if (welcomeConfig == null)
        {
            _logger.LogDebug("No welcome configuration found for guild {GuildId}, using defaults", id);
            welcomeConfig = new WelcomeConfigurationDto
            {
                GuildId = id,
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

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("User submitting welcome configuration for guild {GuildId}", Input.GuildId);

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

        return RedirectToPage("Welcome", new { id = Input.GuildId });
    }

    /// <summary>
    /// Gets the list of text channels for a guild from Discord.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <returns>A list of channel select items sorted by position.</returns>
    private List<ChannelSelectItem> GetTextChannels(ulong guildId)
    {
        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogWarning("Could not fetch Discord guild {GuildId} from client", guildId);
            return new List<ChannelSelectItem>();
        }

        var textChannels = guild.TextChannels
            .Where(c => c != null)
            .OrderBy(c => c.Position)
            .Select(c => new ChannelSelectItem
            {
                Id = c.Id,
                Name = c.Name,
                Position = c.Position
            })
            .ToList();

        _logger.LogDebug("Retrieved {ChannelCount} text channels for guild {GuildId}", textChannels.Count, guildId);

        return textChannels;
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
            AvailableChannels = GetTextChannels(guildId);

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
