using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
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
    private readonly ITtsSettingsService _ttsSettingsService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IGuildService guildService,
        DiscordSocketClient discordClient,
        IAudioService audioService,
        ITtsService ttsService,
        ISettingsService settingsService,
        ITtsSettingsService ttsSettingsService,
        UserManager<ApplicationUser> userManager,
        ILogger<IndexModel> logger)
        : base(guildService, discordClient, userManager, logger)
    {
        _audioService = audioService;
        _ttsService = ttsService;
        _settingsService = settingsService;
        _ttsSettingsService = ttsSettingsService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the voice channel panel view model.
    /// </summary>
    public VoiceChannelPanelViewModel? VoicePanel { get; set; }

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
    /// View model for the mode switcher component.
    /// </summary>
    public ModeSwitcherViewModel ModeSwitcher { get; set; } = new();

    /// <summary>
    /// View model for the preset bar component.
    /// </summary>
    public PresetBarViewModel PresetBar { get; set; } = new();

    /// <summary>
    /// View model for the style selector component.
    /// </summary>
    public StyleSelectorViewModel StyleSelector { get; set; } = new();

    /// <summary>
    /// View model for the emphasis toolbar component (Pro mode only).
    /// </summary>
    public EmphasisToolbarViewModel EmphasisToolbar { get; set; } = new();

    /// <summary>
    /// View model for the SSML preview component (Pro mode only).
    /// </summary>
    public SsmlPreviewViewModel SsmlPreview { get; set; } = new();

    /// <summary>
    /// View model for the pause insertion modal (Pro mode only).
    /// </summary>
    public PauseModalViewModel PauseModal { get; set; } = new();

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

            // Get TTS settings and build SSML component view models
            var settings = await _ttsSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);
            BuildSsmlComponentViewModels(settings);

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

    /// <summary>
    /// Builds the SSML component view models for the TTS portal page.
    /// </summary>
    /// <param name="settings">The guild's TTS settings.</param>
    private void BuildSsmlComponentViewModels(GuildTtsSettings settings)
    {
        // Build mode switcher with portal-specific callback
        ModeSwitcher = new ModeSwitcherViewModel
        {
            CurrentMode = TtsMode.Standard,
            ContainerId = "portalModeSwitcher",
            OnModeChange = "portalHandleModeChange"
        };

        // Build preset bar with 8 default presets (portal-specific callback)
        PresetBar = new PresetBarViewModel
        {
            Presets = new List<PresetButtonViewModel>
            {
                new() { Id = "excited", Name = "Excited", Icon = "sparkles", VoiceName = "en-US-JennyNeural", Style = "cheerful", Speed = 1.2m, Pitch = 1.1m, Description = "High energy, cheerful tone" },
                new() { Id = "announcer", Name = "Announcer", Icon = "megaphone", VoiceName = "en-US-GuyNeural", Style = "newscast", Speed = 1.0m, Pitch = 0.9m, Description = "Professional announcer voice" },
                new() { Id = "robot", Name = "Robot", Icon = "computer-desktop", VoiceName = "en-US-AriaNeural", Style = null, Speed = 1.0m, Pitch = 0.7m, Description = "Robotic, monotone delivery" },
                new() { Id = "friendly", Name = "Friendly", Icon = "face-smile", VoiceName = "en-US-JennyNeural", Style = "friendly", Speed = 1.0m, Pitch = 1.0m, Description = "Warm, approachable tone" },
                new() { Id = "angry", Name = "Angry", Icon = "fire", VoiceName = "en-US-GuyNeural", Style = "angry", Speed = 1.1m, Pitch = 1.2m, Description = "Aggressive, high pitch" },
                new() { Id = "narrator", Name = "Narrator", Icon = "microphone", VoiceName = "en-US-DavisNeural", Style = "narration-professional", Speed = 0.9m, Pitch = 1.0m, Description = "Professional narration" },
                new() { Id = "whisper", Name = "Whisper", Icon = "speaker-x-mark", VoiceName = "en-US-JennyNeural", Style = "whispering", Speed = 0.8m, Pitch = 0.95m, Description = "Quiet, intimate tone" },
                new() { Id = "shouting", Name = "Shouting", Icon = "speaker-wave", VoiceName = "en-US-GuyNeural", Style = "shouting", Speed = 1.15m, Pitch = 1.3m, Description = "Loud, forceful delivery" }
            },
            ContainerId = "portalPresetBar",
            OnPresetApply = "portalHandlePresetApply"
        };

        // Build style selector with default styles (portal-specific callbacks)
        StyleSelector = new StyleSelectorViewModel
        {
            SelectedVoice = settings.DefaultVoice,
            SelectedStyle = string.Empty,
            StyleIntensity = 1.0m,
            AvailableStyles = new List<StyleOption>
            {
                new() { Value = "", Label = "(None)", Icon = "", Description = "Natural speech", Example = "" },
                new() { Value = "cheerful", Label = "Cheerful", Icon = "face-smile", Description = "Happy, energetic", Example = "I'm so excited to see you!" },
                new() { Value = "excited", Label = "Excited", Icon = "sparkles", Description = "Very enthusiastic", Example = "We won the championship!" },
                new() { Value = "friendly", Label = "Friendly", Icon = "hand-raised", Description = "Warm, approachable", Example = "Hey, great to meet you!" },
                new() { Value = "sad", Label = "Sad", Icon = "face-frown", Description = "Sorrowful", Example = "I'm sorry for your loss." },
                new() { Value = "angry", Label = "Angry", Icon = "fire", Description = "Frustrated", Example = "I can't believe this happened!" },
                new() { Value = "whispering", Label = "Whispering", Icon = "speaker-x-mark", Description = "Quiet, intimate", Example = "Don't tell anyone..." },
                new() { Value = "shouting", Label = "Shouting", Icon = "speaker-wave", Description = "Loud, urgent", Example = "Watch out!" },
                new() { Value = "newscast", Label = "Newscast", Icon = "newspaper", Description = "Professional reporter", Example = "Breaking news tonight..." }
            },
            ContainerId = "portalStyleSelector",
            OnStyleChange = "portalHandleStyleChange",
            OnIntensityChange = "portalHandleIntensityChange"
        };

        // Build emphasis toolbar (Pro mode only, portal-specific callback)
        EmphasisToolbar = new EmphasisToolbarViewModel
        {
            TargetTextareaId = "ttsMessage",
            ContainerId = "portalEmphasisToolbar",
            OnFormatChange = "portalHandleFormatChange",
            ShowKeyboardShortcuts = true
        };

        // Build SSML preview (Pro mode only, portal-specific callback)
        SsmlPreview = new SsmlPreviewViewModel
        {
            ContainerId = "portalSsmlPreview",
            InitialSsml = null,
            StartCollapsed = true,
            OnCopy = "portalHandleSsmlCopy",
            ShowCharacterCount = true
        };

        // Build pause modal (Pro mode only, portal-specific callback)
        PauseModal = new PauseModalViewModel
        {
            Id = "portalPauseModal",
            DefaultDuration = 500,
            OnInsertCallback = "portalHandlePauseInsert"
        };
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
