using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Portal.TTS;

/// <summary>
/// Page model for the TTS (Text-to-Speech) Guild Member Portal.
/// Shows a landing page for unauthenticated users, or the full TTS interface
/// for authenticated guild members.
/// </summary>
[AllowAnonymous]
public class IndexModel : PortalPageModelBase
{
    private readonly IAudioService _audioService;
    private readonly ITtsService _ttsService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IGuildService guildService,
        DiscordSocketClient discordClient,
        IAudioService audioService,
        ITtsService ttsService,
        ISettingsService settingsService,
        UserManager<ApplicationUser> userManager,
        ILogger<IndexModel> logger)
        : base(guildService, discordClient, userManager, logger)
    {
        _audioService = audioService;
        _ttsService = ttsService;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the list of voice channels in the guild.
    /// </summary>
    public List<VoiceChannelInfo> VoiceChannels { get; set; } = new();

    /// <summary>
    /// Gets the ID of the voice channel the bot is currently connected to.
    /// </summary>
    public ulong? CurrentChannelId { get; set; }

    /// <summary>
    /// Gets whether the bot is connected to a voice channel in this guild.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets the list of available TTS voices grouped by locale.
    /// </summary>
    public List<TtsVoiceInfo> AvailableVoices { get; set; } = new();

    /// <summary>
    /// Gets the maximum message length allowed for TTS.
    /// </summary>
    public int MaxMessageLength { get; set; } = 200;

    /// <summary>
    /// Gets whether audio features are globally disabled at the bot level.
    /// </summary>
    public bool IsAudioGloballyDisabled { get; set; }

    /// <summary>
    /// Handles GET requests to display the TTS Portal page.
    /// Shows a landing page for unauthenticated users.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if audio is globally disabled
            var isGloballyEnabled = await _settingsService.GetSettingValueAsync<bool?>("Features:AudioEnabled") ?? true;
            IsAudioGloballyDisabled = !isGloballyEnabled;

            // Perform common portal authorization check
            var (authResult, context) = await CheckPortalAuthorizationAsync(guildId, "TTS", cancellationToken);

            // Handle auth failures
            var actionResult = GetAuthResultAction(authResult);
            if (actionResult != null)
            {
                return actionResult;
            }

            // Always populate voices (needed for landing page too)
            PopulateAvailableVoices();

            // For landing page, we're done
            if (authResult == PortalAuthResult.ShowLandingPage)
            {
                return Page();
            }

            // User is authorized - load full TTS interface
            VoiceChannels = BuildVoiceChannelList(context!.SocketGuild);
            CurrentChannelId = _audioService.GetConnectedChannelId(guildId);
            IsConnected = _audioService.IsConnected(guildId);

            _logger.LogDebug("Loaded TTS Portal for guild {GuildId}", guildId);

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load TTS Portal for guild {GuildId}", guildId);
            return StatusCode(500);
        }
    }

    /// <summary>
    /// Populates the list of available TTS voices.
    /// Uses the curated list from ITtsService for consistency with the /tts command.
    /// </summary>
    private void PopulateAvailableVoices()
    {
        // Use curated list from TTS service (same list as /tts command autocomplete)
        AvailableVoices = _ttsService.GetCuratedVoices()
            .Select(v => new TtsVoiceInfo
            {
                Name = v.ShortName,
                DisplayName = $"{v.DisplayName} ({v.Gender})",
                Locale = v.Locale
            })
            .ToList();
        _logger.LogDebug("Loaded {Count} curated voices", AvailableVoices.Count);
    }
}

/// <summary>
/// DTO for TTS voice information.
/// </summary>
public class TtsVoiceInfo
{
    /// <summary>
    /// Gets or sets the voice identifier (e.g., "en-US-AriaNeural").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the human-readable display name (e.g., "Aria (Female)").
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the locale group (e.g., "English (US)").
    /// </summary>
    public string Locale { get; set; } = string.Empty;
}
