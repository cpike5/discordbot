using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Portal;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Portal.Soundboard;

/// <summary>
/// Page model for the Soundboard Guild Member Portal.
/// Shows a landing page for unauthenticated users, or the full soundboard
/// for authenticated guild members.
/// </summary>
[AllowAnonymous]
public class IndexModel : PortalPageModelBase
{
    private readonly ISoundService _soundService;
    private readonly IGuildAudioSettingsRepository _audioSettingsRepository;
    private readonly IAudioService _audioService;
    private readonly IPlaybackService _playbackService;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ISoundService soundService,
        IGuildAudioSettingsRepository audioSettingsRepository,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        IAudioService audioService,
        IPlaybackService playbackService,
        ISettingsService settingsService,
        UserManager<ApplicationUser> userManager,
        ILogger<IndexModel> logger)
        : base(guildService, discordClient, userManager, logger)
    {
        _soundService = soundService;
        _audioSettingsRepository = audioSettingsRepository;
        _audioService = audioService;
        _playbackService = playbackService;
        _settingsService = settingsService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the list of sounds available in this guild.
    /// </summary>
    public IReadOnlyList<PortalSoundViewModel> Sounds { get; set; } = Array.Empty<PortalSoundViewModel>();

    /// <summary>
    /// Gets the voice channel panel view model.
    /// </summary>
    public VoiceChannelPanelViewModel? VoicePanel { get; set; }

    /// <summary>
    /// Gets the maximum number of sounds allowed per guild.
    /// </summary>
    public int MaxSounds { get; set; }

    /// <summary>
    /// Gets the current sound count for this guild.
    /// </summary>
    public int CurrentSoundCount { get; set; }

    /// <summary>
    /// Gets the supported audio formats.
    /// </summary>
    public string SupportedFormats { get; set; } = "MP3, WAV, OGG";

    /// <summary>
    /// Gets the maximum file size in MB.
    /// </summary>
    public int MaxFileSizeMB { get; set; }

    /// <summary>
    /// Gets the maximum duration in seconds.
    /// </summary>
    public int MaxDurationSeconds { get; set; }

    /// <summary>
    /// Gets whether audio features are globally disabled at the bot level.
    /// </summary>
    public bool IsAudioGloballyDisabled { get; set; }

    /// <summary>
    /// Handles GET requests to display the Soundboard Portal page.
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
            // Check if audio is globally disabled at bot level
            var isGloballyEnabled = await _settingsService.GetSettingValueAsync<bool?>("Features:AudioEnabled") ?? true;
            IsAudioGloballyDisabled = !isGloballyEnabled;

            // Check if portal is enabled for this guild first (before auth check)
            var audioSettings = await _audioSettingsRepository.GetByGuildIdAsync(guildId);

            // TODO: Issue #947 will add EnableMemberPortal property
            // For now, we check AudioEnabled as a proxy
            if (audioSettings == null || !audioSettings.AudioEnabled)
            {
                _logger.LogDebug("Portal not enabled for guild {GuildId}", guildId);
                return NotFound();
            }

            // Perform common portal authorization check
            var (authResult, context) = await CheckPortalAuthorizationAsync(guildId, "Soundboard", cancellationToken);

            // Handle auth failures
            var actionResult = GetAuthResultAction(authResult);
            if (actionResult != null)
            {
                return actionResult;
            }

            // For landing page, we're done
            if (authResult == PortalAuthResult.ShowLandingPage)
            {
                return Page();
            }

            // User is authorized - load full soundboard
            var sounds = await _soundService.GetAllByGuildAsync(guildId, cancellationToken);

            // Get audio settings for limits
            var settings = await _audioSettingsRepository.GetOrCreateAsync(guildId, cancellationToken);

            // Map sounds to portal view models
            var soundViewModels = sounds
                .Select(s => new PortalSoundViewModel
                {
                    Id = s.Id,
                    Name = s.Name,
                    PlayCount = s.PlayCount
                })
                .ToList();

            // Build voice channel panel data
            var connectedChannelId = _audioService.GetConnectedChannelId(guildId);
            var isConnected = _audioService.IsConnected(guildId);
            string? connectedChannelName = null;
            int? channelMemberCount = null;

            if (isConnected && connectedChannelId.HasValue)
            {
                var connectedChannel = context!.SocketGuild.GetVoiceChannel(connectedChannelId.Value);
                if (connectedChannel != null)
                {
                    connectedChannelName = connectedChannel.Name;
                    channelMemberCount = connectedChannel.ConnectedUsers.Count(u => !u.IsBot);
                }
            }

            VoicePanel = new VoiceChannelPanelViewModel
            {
                GuildId = guildId,
                IsCompact = true,
                IsConnected = isConnected,
                ConnectedChannelId = connectedChannelId,
                ConnectedChannelName = connectedChannelName,
                ChannelMemberCount = channelMemberCount,
                AvailableChannels = BuildVoiceChannelList(context!.SocketGuild)
                    .Select(c => new DiscordBot.Bot.ViewModels.Components.VoiceChannelInfo
                    {
                        Id = c.Id,
                        Name = c.Name,
                        MemberCount = c.MemberCount
                    }).ToList(),
                NowPlaying = null,
                Queue = []
            };

            // Set remaining view properties
            Sounds = soundViewModels;
            MaxSounds = settings.MaxSoundsPerGuild;
            CurrentSoundCount = sounds.Count;
            SupportedFormats = "MP3, WAV, OGG";
            MaxFileSizeMB = (int)(settings.MaxFileSizeBytes / (1024.0 * 1024.0));
            MaxDurationSeconds = settings.MaxDurationSeconds;

            _logger.LogDebug("Loaded {Count} sounds for guild {GuildId} in portal view",
                sounds.Count, guildId);

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load Soundboard Portal for guild {GuildId}", guildId);
            return StatusCode(500);
        }
    }
}
