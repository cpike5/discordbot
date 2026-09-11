using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Portal;
using DiscordBot.Core.Constants;
using DiscordBot.Core.DTOs;
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

    /// <summary>
    /// Currency lookup for the price badges, or null when <c>Currency:Enabled</c> is false and
    /// nothing can be priced. Optional so the page still builds with the feature switched off.
    /// </summary>
    private readonly ICurrencyService? _currencyService;

    public IndexModel(
        ISoundService soundService,
        IGuildAudioSettingsRepository audioSettingsRepository,
        IGuildService guildService,
        DiscordSocketClient discordClient,
        IAudioService audioService,
        IPlaybackService playbackService,
        ISettingsService settingsService,
        UserManager<ApplicationUser> userManager,
        ILogger<IndexModel> logger,
        ICurrencyService? currencyService = null)
        : base(guildService, discordClient, userManager, logger)
    {
        _soundService = soundService;
        _audioSettingsRepository = audioSettingsRepository;
        _audioService = audioService;
        _playbackService = playbackService;
        _settingsService = settingsService;
        _logger = logger;
        _currencyService = currencyService;
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
    /// Gets the current authenticated user's Discord ID as a string (for JS snowflake safety).
    /// Null if user ID cannot be determined from claims.
    /// </summary>
    public string? CurrentUserId { get; set; }

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

            // Extract current user ID from claims
            var userIdClaim = User.FindFirst("discord:user_id")?.Value;
            CurrentUserId = userIdClaim;

            // User is authorized - load full soundboard
            var sounds = await _soundService.GetAllByGuildAsync(guildId, cancellationToken);

            // Get audio settings for limits
            var settings = await _audioSettingsRepository.GetOrCreateAsync(guildId, cancellationToken);

            // Prices for the badges. Everything is free until an admin sets a price, so the common
            // case is an empty lookup and no badges at all.
            var prices = await GetSoundPricesAsync(guildId, cancellationToken);

            // Map sounds to portal view models
            var soundViewModels = sounds
                .Select(s =>
                {
                    prices.TryGetValue(CurrencyFeatureKeys.Soundboard(s.Id), out var price);
                    return new PortalSoundViewModel
                    {
                        Id = s.Id,
                        Name = s.Name,
                        PlayCount = s.PlayCount,
                        DurationSeconds = s.DurationSeconds,
                        UploadedById = s.UploadedById?.ToString(),
                        UploadedAt = s.UploadedAt,
                        Price = price?.Amount,
                        CurrencySymbol = price?.CurrencySymbol
                    };
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
                ShowNowPlaying = true,
                ShowProgress = false,
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
                NowPlaying = _playbackService.IsPlaying(guildId)
                    ? new NowPlayingInfo { Name = "Now Playing" }
                    : null,
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

    /// <summary>
    /// Builds the feature-key lookup of active soundboard prices in this guild.
    /// <para>
    /// Returns an empty lookup when the currency feature is off (by configuration or by the
    /// runtime setting), when nothing is priced, and when the lookup fails: a missing badge is not
    /// a reason to fail the page.
    /// </para>
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task<Dictionary<string, PriceEntryDto>> GetSoundPricesAsync(
        ulong guildId,
        CancellationToken cancellationToken)
    {
        if (_currencyService == null)
        {
            return new Dictionary<string, PriceEntryDto>();
        }

        try
        {
            // Badges follow the same runtime switch as the charge: with the feature off nothing is
            // charged, so nothing should be advertised as costing anything either.
            var currencyEnabled = await _settingsService.GetSettingValueAsync<bool?>(
                "Features:CurrencyEnabled", cancellationToken) ?? true;

            if (!currencyEnabled)
            {
                return new Dictionary<string, PriceEntryDto>();
            }

            var prices = await _currencyService.GetPricesForGuildAsync(guildId, cancellationToken);

            return prices
                .Where(p => p.IsActive && p.FeatureKey.StartsWith(
                    CurrencyFeatureKeys.SoundboardArea + ":", StringComparison.Ordinal))
                // A guild entry and a global entry can share a feature key; the guild's wins, the
                // same way the charge seam resolves it.
                .OrderByDescending(p => p.GuildId.HasValue)
                .GroupBy(p => p.FeatureKey, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load soundboard prices for guild {GuildId}", guildId);
            return new Dictionary<string, PriceEntryDto>();
        }
    }
}
