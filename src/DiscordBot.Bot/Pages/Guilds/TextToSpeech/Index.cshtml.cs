using Discord.WebSocket;
using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DiscordBot.Bot.Pages.Guilds.TextToSpeech;

/// <summary>
/// Page model for the Text-to-Speech management page.
/// Displays TTS settings, statistics, and recent messages for a guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : PageModel
{
    private readonly ITtsHistoryService _ttsHistoryService;
    private readonly ITtsSettingsService _ttsSettingsService;
    private readonly ITtsService _ttsService;
    private readonly IAudioService _audioService;
    private readonly ITtsPlaybackService _ttsPlaybackService;
    private readonly DiscordSocketClient _discordClient;
    private readonly IGuildService _guildService;
    private readonly ISettingsService _settingsService;
    private readonly IGuildAudioSettingsRepository _audioSettingsRepository;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ITtsHistoryService ttsHistoryService,
        ITtsSettingsService ttsSettingsService,
        ITtsService ttsService,
        IAudioService audioService,
        ITtsPlaybackService ttsPlaybackService,
        DiscordSocketClient discordClient,
        IGuildService guildService,
        ISettingsService settingsService,
        IGuildAudioSettingsRepository audioSettingsRepository,
        ILogger<IndexModel> logger)
    {
        _ttsHistoryService = ttsHistoryService;
        _ttsSettingsService = ttsSettingsService;
        _ttsService = ttsService;
        _audioService = audioService;
        _ttsPlaybackService = ttsPlaybackService;
        _discordClient = discordClient;
        _guildService = guildService;
        _settingsService = settingsService;
        _audioSettingsRepository = audioSettingsRepository;
        _logger = logger;
    }

    /// <summary>
    /// View model for display properties.
    /// </summary>
    public TtsIndexViewModel ViewModel { get; set; } = new();

    public GuildBreadcrumbViewModel Breadcrumb { get; set; } = new();
    public GuildHeaderViewModel Header { get; set; } = new();
    public GuildNavBarViewModel Navigation { get; set; } = new();

    /// <summary>
    /// View model for the voice channel control panel.
    /// </summary>
    public VoiceChannelPanelViewModel? VoiceChannelPanel { get; set; }

    /// <summary>
    /// Success message from TempData.
    /// </summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>
    /// Error message from TempData.
    /// </summary>
    [TempData]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets whether audio features are globally disabled at the bot level.
    /// </summary>
    public bool IsAudioGloballyDisabled { get; set; }

    /// <summary>
    /// Gets whether the member portal is enabled for this guild.
    /// </summary>
    public bool IsMemberPortalEnabled { get; set; }

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
    /// Handles GET requests to display the TTS management page.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The page result.</returns>
    public async Task<IActionResult> OnGetAsync(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User accessing TTS management for guild {GuildId}", guildId);

        try
        {
            // Check if audio is globally disabled
            var isGloballyEnabled = await _settingsService.GetSettingValueAsync<bool?>("Features:AudioEnabled") ?? true;
            IsAudioGloballyDisabled = !isGloballyEnabled;

            // Get guild info from service
            var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found", guildId);
                return NotFound();
            }

            // Get TTS settings (creates defaults if not found)
            var settings = await _ttsSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);

            // Get audio settings to check if member portal is enabled
            var audioSettings = await _audioSettingsRepository.GetOrCreateAsync(guildId, cancellationToken);
            IsMemberPortalEnabled = audioSettings.EnableMemberPortal;

            // Populate guild layout ViewModels
            Breadcrumb = new GuildBreadcrumbViewModel
            {
                Items = new List<BreadcrumbItem>
                {
                    new() { Label = "Home", Url = "/" },
                    new() { Label = "Servers", Url = "/Guilds" },
                    new() { Label = guild.Name, Url = $"/Guilds/Details/{guild.Id}" },
                    new() { Label = "Audio", Url = $"/Guilds/Soundboard/{guild.Id}" },
                    new() { Label = "TTS", IsCurrent = true }
                }
            };

            var headerActions = new List<HeaderAction>();
            if (IsMemberPortalEnabled)
            {
                headerActions.Add(new()
                {
                    Label = "Open Member Portal",
                    Url = $"/Portal/TTS/{guildId}",
                    Style = HeaderActionStyle.Secondary,
                    Icon = "M10 6H6a2 2 0 00-2 2v10a2 2 0 002 2h10a2 2 0 002-2v-4M14 4h6m0 0v6m0-6L10 14",
                    OpenInNewTab = true
                });
            }

            Header = new GuildHeaderViewModel
            {
                GuildId = guild.Id,
                GuildName = guild.Name,
                GuildIconUrl = guild.IconUrl,
                PageTitle = "Audio",
                PageDescription = $"Manage audio settings and TTS for {guild.Name}",
                Actions = headerActions
            };

            Navigation = new GuildNavBarViewModel
            {
                GuildId = guild.Id,
                ActiveTab = "audio",
                Tabs = GuildNavigationConfig.GetTabs().ToList()
            };

            // Get TTS statistics
            var stats = await _ttsHistoryService.GetStatsAsync(guildId, cancellationToken);

            // Get recent TTS messages
            var recentMessages = await _ttsHistoryService.GetRecentMessagesAsync(guildId, 10, cancellationToken);

            _logger.LogDebug("Retrieved TTS data for guild {GuildId}: {MessagesToday} messages today, {TotalMessages} recent messages",
                guildId, stats.MessagesToday, recentMessages.Count());

            // Build view model
            ViewModel = TtsIndexViewModel.Create(
                guildId,
                guild.Name,
                guild.IconUrl,
                stats,
                recentMessages,
                settings);

            // Build voice channel panel view model
            VoiceChannelPanel = BuildVoiceChannelPanelViewModel(guildId);

            // Build SSML component view models
            BuildSsmlComponentViewModels(settings);

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load TTS page for guild {GuildId}", guildId);
            ErrorMessage = "Failed to load TTS page. Please try again.";

            // Set fallback voice channel panel
            VoiceChannelPanel = new VoiceChannelPanelViewModel { GuildId = guildId };

            return Page();
        }
    }

    /// <summary>
    /// Handles POST requests to update TTS settings.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="defaultVoice">The default voice identifier.</param>
    /// <param name="defaultSpeed">The default speech speed.</param>
    /// <param name="defaultPitch">The default pitch adjustment.</param>
    /// <param name="defaultVolume">The default volume level.</param>
    /// <param name="autoPlayOnSend">Whether to auto-play TTS on send.</param>
    /// <param name="announceJoinsLeaves">Whether to announce joins/leaves.</param>
    /// <param name="rateLimitPerMinute">The rate limit for TTS messages per user per minute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the index page or JSON for AJAX requests.</returns>
    public async Task<IActionResult> OnPostUpdateSettingsAsync(
        ulong guildId,
        [FromForm] string defaultVoice,
        [FromForm] double defaultSpeed,
        [FromForm] double defaultPitch,
        [FromForm] double defaultVolume,
        [FromForm] bool autoPlayOnSend,
        [FromForm] bool announceJoinsLeaves,
        [FromForm] int rateLimitPerMinute,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to update TTS settings for guild {GuildId}", guildId);

        // Check if this is an AJAX request
        var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        try
        {
            // Get current settings
            var settings = await _ttsSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);

            // Update settings
            settings.DefaultVoice = defaultVoice ?? string.Empty;
            settings.DefaultSpeed = Math.Clamp(defaultSpeed, 0.5, 2.0);
            settings.DefaultPitch = Math.Clamp(defaultPitch, 0.5, 2.0);
            settings.DefaultVolume = Math.Clamp(defaultVolume, 0.0, 1.0);
            settings.AutoPlayOnSend = autoPlayOnSend;
            settings.AnnounceJoinsLeaves = announceJoinsLeaves;
            settings.RateLimitPerMinute = Math.Clamp(rateLimitPerMinute, 1, 60);

            await _ttsSettingsService.UpdateSettingsAsync(settings, cancellationToken);

            _logger.LogInformation("Successfully updated TTS settings for guild {GuildId}", guildId);

            if (isAjax)
            {
                return new JsonResult(new
                {
                    success = true,
                    message = "TTS settings updated successfully."
                });
            }

            SuccessMessage = "TTS settings updated successfully.";
            return RedirectToPage("Index", new { guildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating TTS settings for guild {GuildId}", guildId);

            if (isAjax)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "An error occurred while updating settings. Please try again."
                })
                {
                    StatusCode = 400
                };
            }

            ErrorMessage = "An error occurred while updating settings. Please try again.";
            return RedirectToPage("Index", new { guildId });
        }
    }

    /// <summary>
    /// Handles POST requests to delete a TTS message from history.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="messageId">The TTS message ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the index page or JSON for AJAX requests.</returns>
    public async Task<IActionResult> OnPostDeleteMessageAsync(
        ulong guildId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to delete TTS message {MessageId} for guild {GuildId}",
            messageId, guildId);

        // Check if this is an AJAX request
        var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        try
        {
            var deleted = await _ttsHistoryService.DeleteMessageAsync(messageId, cancellationToken);

            if (deleted)
            {
                _logger.LogInformation("Successfully deleted TTS message {MessageId}", messageId);

                if (isAjax)
                {
                    return new JsonResult(new
                    {
                        success = true,
                        message = "Message deleted successfully.",
                        messageId = messageId.ToString()
                    });
                }

                SuccessMessage = "Message deleted successfully.";
            }
            else
            {
                _logger.LogWarning("TTS message {MessageId} not found", messageId);

                if (isAjax)
                {
                    return new JsonResult(new
                    {
                        success = false,
                        message = "Message not found."
                    })
                    {
                        StatusCode = 400
                    };
                }

                ErrorMessage = "Message not found.";
            }

            return isAjax
                ? new JsonResult(new { success = false, message = "An unexpected error occurred." }) { StatusCode = 400 }
                : RedirectToPage("Index", new { guildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting TTS message {MessageId} for guild {GuildId}",
                messageId, guildId);

            if (isAjax)
            {
                return new JsonResult(new
                {
                    success = false,
                    message = "An error occurred while deleting the message. Please try again."
                })
                {
                    StatusCode = 400
                };
            }

            ErrorMessage = "An error occurred while deleting the message. Please try again.";
            return RedirectToPage("Index", new { guildId });
        }
    }

    /// <summary>
    /// Handles POST requests to send a TTS message to the voice channel.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="message">The message text to synthesize and play.</param>
    /// <param name="style">Optional voice style (e.g., "cheerful", "angry").</param>
    /// <param name="styleIntensity">Optional style intensity (0.01 to 2.0).</param>
    /// <param name="ssml">Optional raw SSML markup (Pro mode only).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the index page or JSON for AJAX requests.</returns>
    public async Task<IActionResult> OnPostSendMessageAsync(
        ulong guildId,
        [FromForm] string message,
        [FromForm] string? style = null,
        [FromForm] decimal? styleIntensity = null,
        [FromForm] string? ssml = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to send TTS message for guild {GuildId}", guildId);

        // Check if this is an AJAX request
        var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";

        if (string.IsNullOrWhiteSpace(message))
        {
            var errorMsg = "Message cannot be empty.";
            if (isAjax)
            {
                return new JsonResult(new { success = false, message = errorMsg }) { StatusCode = 400 };
            }
            ErrorMessage = errorMsg;
            return RedirectToPage("Index", new { guildId });
        }

        if (!_ttsService.IsConfigured)
        {
            var errorMsg = "TTS service is not configured. Please configure Azure Speech settings.";
            if (isAjax)
            {
                return new JsonResult(new { success = false, message = errorMsg }) { StatusCode = 400 };
            }
            ErrorMessage = errorMsg;
            return RedirectToPage("Index", new { guildId });
        }

        if (!_audioService.IsConnected(guildId))
        {
            var errorMsg = "Bot is not connected to a voice channel. Please join a voice channel first.";
            if (isAjax)
            {
                return new JsonResult(new { success = false, message = errorMsg }) { StatusCode = 400 };
            }
            ErrorMessage = errorMsg;
            return RedirectToPage("Index", new { guildId });
        }

        try
        {
            // Get TTS settings for voice options
            var settings = await _ttsSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);

            // Defensive validation: ensure DefaultVoice is never empty
            var voice = string.IsNullOrWhiteSpace(settings.DefaultVoice)
                ? "en-US-JennyNeural"
                : settings.DefaultVoice;

            var options = new TtsOptions
            {
                Voice = voice,
                Speed = settings.DefaultSpeed,
                Pitch = settings.DefaultPitch,
                Volume = settings.DefaultVolume
            };

            // TODO: Integrate style and SSML parameters with TTS synthesis when backend support is added
            // For now, we just log these values to confirm form submission works
            if (!string.IsNullOrEmpty(style))
            {
                _logger.LogDebug("TTS style parameter received: {Style} with intensity {Intensity}", style, styleIntensity ?? 1.0m);
            }
            if (!string.IsNullOrEmpty(ssml))
            {
                _logger.LogDebug("TTS SSML parameter received: {SsmlLength} characters", ssml.Length);
            }

            // Synthesize the speech
            using var audioStream = await _ttsService.SynthesizeSpeechAsync(message, options, cancellationToken);

            // Play the audio using the TTS playback service
            var playbackResult = await _ttsPlaybackService.PlayAsync(
                guildId,
                User.GetDiscordUserId(),
                User.Identity?.Name ?? "Admin UI",
                message,
                options.Voice ?? string.Empty,
                audioStream,
                cancellationToken);

            if (!playbackResult.Success)
            {
                _logger.LogWarning("TTS playback failed for guild {GuildId}: {ErrorMessage}", guildId, playbackResult.ErrorMessage);
                if (isAjax)
                {
                    return new JsonResult(new { success = false, message = playbackResult.ErrorMessage }) { StatusCode = 400 };
                }
                ErrorMessage = playbackResult.ErrorMessage;
                return RedirectToPage("Index", new { guildId });
            }

            var ttsMessage = playbackResult.LoggedMessage!;
            _logger.LogInformation("Successfully played TTS message for guild {GuildId}", guildId);

            if (isAjax)
            {
                // Get updated stats for AJAX response
                var stats = await _ttsHistoryService.GetStatsAsync(guildId, cancellationToken);

                // Build recent message DTO for client-side rendering
                var recentMessage = new
                {
                    id = ttsMessage.Id.ToString(),
                    userId = ttsMessage.UserId.ToString(),
                    username = ttsMessage.Username,
                    message = ttsMessage.Message,
                    voice = ttsMessage.Voice,
                    durationFormatted = FormatDuration(ttsMessage.DurationSeconds)
                };

                return new JsonResult(new
                {
                    success = true,
                    message = "Message sent to voice channel.",
                    stats = new
                    {
                        messagesToday = stats.MessagesToday,
                        totalPlaybackFormatted = FormatPlaybackTime(stats.TotalPlaybackSeconds),
                        uniqueUsers = stats.UniqueUsers
                    },
                    recentMessage
                });
            }

            SuccessMessage = "Message sent to voice channel.";
            return RedirectToPage("Index", new { guildId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "TTS service error for guild {GuildId}: {Message}", guildId, ex.Message);
            if (isAjax)
            {
                return new JsonResult(new { success = false, message = ex.Message }) { StatusCode = 400 };
            }
            ErrorMessage = ex.Message;
            return RedirectToPage("Index", new { guildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending TTS message for guild {GuildId}", guildId);
            var errorMsg = "An error occurred while sending the message. Please try again.";
            if (isAjax)
            {
                return new JsonResult(new { success = false, message = errorMsg }) { StatusCode = 400 };
            }
            ErrorMessage = errorMsg;
            return RedirectToPage("Index", new { guildId });
        }
    }

    /// <summary>
    /// Formats a duration in seconds to a human-readable string.
    /// </summary>
    /// <param name="durationSeconds">Duration in seconds.</param>
    /// <returns>Formatted duration string (e.g., "1m 23s", "45s").</returns>
    private static string FormatDuration(double durationSeconds)
    {
        var timeSpan = TimeSpan.FromSeconds(durationSeconds);
        if (timeSpan.TotalMinutes >= 1)
        {
            return $"{(int)timeSpan.TotalMinutes}m {timeSpan.Seconds}s";
        }
        return $"{(int)timeSpan.TotalSeconds}s";
    }

    /// <summary>
    /// Formats total playback time in seconds to a human-readable string.
    /// </summary>
    /// <param name="totalSeconds">Total seconds of playback.</param>
    /// <returns>Formatted playback time (e.g., "2h 15m", "45m", "30s").</returns>
    private static string FormatPlaybackTime(double totalSeconds)
    {
        var timeSpan = TimeSpan.FromSeconds(totalSeconds);
        if (timeSpan.TotalHours >= 1)
        {
            return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
        }
        if (timeSpan.TotalMinutes >= 1)
        {
            return $"{(int)timeSpan.TotalMinutes}m";
        }
        return $"{(int)timeSpan.TotalSeconds}s";
    }

    /// <summary>
    /// Builds the voice channel panel view model with current connection status and available channels.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <returns>The voice channel panel view model.</returns>
    private VoiceChannelPanelViewModel BuildVoiceChannelPanelViewModel(ulong guildId)
    {
        var socketGuild = _discordClient.GetGuild(guildId);
        var isConnected = _audioService.IsConnected(guildId);
        var connectedChannelId = _audioService.GetConnectedChannelId(guildId);

        // Build available channels list
        var availableChannels = new List<VoiceChannelInfo>();
        if (socketGuild != null)
        {
            foreach (var channel in socketGuild.VoiceChannels.Where(c => c != null).OrderBy(c => c.Position))
            {
                availableChannels.Add(new VoiceChannelInfo
                {
                    Id = channel.Id,
                    Name = channel.Name,
                    MemberCount = channel.ConnectedUsers.Count
                });
            }
        }

        // Get connected channel info if connected
        string? connectedChannelName = null;
        int? channelMemberCount = null;
        if (isConnected && connectedChannelId.HasValue && socketGuild != null)
        {
            var connectedChannel = socketGuild.GetVoiceChannel(connectedChannelId.Value);
            if (connectedChannel != null)
            {
                connectedChannelName = connectedChannel.Name;
                channelMemberCount = connectedChannel.ConnectedUsers.Count;
            }
        }

        return new VoiceChannelPanelViewModel
        {
            GuildId = guildId,
            IsConnected = isConnected,
            ConnectedChannelId = connectedChannelId,
            ConnectedChannelName = connectedChannelName,
            ChannelMemberCount = channelMemberCount,
            AvailableChannels = availableChannels,
            ShowNowPlaying = false
            // NowPlaying and Queue will be populated via SignalR in real-time
        };
    }

    /// <summary>
    /// Builds the SSML component view models for the TTS page.
    /// </summary>
    /// <param name="settings">The guild's TTS settings.</param>
    private void BuildSsmlComponentViewModels(GuildTtsSettings settings)
    {
        // Build mode switcher
        ModeSwitcher = new ModeSwitcherViewModel
        {
            CurrentMode = TtsMode.Standard,
            ContainerId = "modeSwitcher",
            OnModeChange = "handleModeChange"
        };

        // Build preset bar with 8 default presets
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
            ContainerId = "presetBar",
            OnPresetApply = "handlePresetApply"
        };

        // Build style selector with default styles
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
            ContainerId = "styleSelector",
            OnStyleChange = "handleStyleChange",
            OnIntensityChange = "handleIntensityChange"
        };

        // Build emphasis toolbar (Pro mode only)
        EmphasisToolbar = new EmphasisToolbarViewModel
        {
            TargetTextareaId = "messageInput",
            ContainerId = "emphasisToolbar",
            OnFormatChange = "handleFormatChange",
            ShowKeyboardShortcuts = true
        };

        // Build SSML preview (Pro mode only)
        SsmlPreview = new SsmlPreviewViewModel
        {
            ContainerId = "ssmlPreview",
            InitialSsml = null,
            StartCollapsed = true,
            OnCopy = "handleSsmlCopy",
            ShowCharacterCount = true
        };
    }
}
