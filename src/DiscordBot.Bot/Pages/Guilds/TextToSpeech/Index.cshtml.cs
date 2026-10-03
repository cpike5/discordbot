using Discord.WebSocket;
using DiscordBot.Bot.Configuration;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Tts;
using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Exceptions;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Pages.Guilds.TextToSpeech;

/// <summary>
/// Page model for the Text-to-Speech management page.
/// Displays TTS settings, statistics, and recent messages for a guild.
/// </summary>
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class IndexModel : GuildPageModelBase
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
    private readonly ISsmlBuilder _ssmlBuilder;
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
        ISsmlBuilder ssmlBuilder,
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
        _ssmlBuilder = ssmlBuilder;
        _logger = logger;
    }

    /// <summary>
    /// View model for display properties.
    /// </summary>
    public TtsIndexViewModel ViewModel { get; set; } = new();

    /// <summary>
    /// View model for the voice channel control panel.
    /// </summary>
    public VoiceChannelPanelViewModel? VoiceChannelPanel { get; set; }

    /// <summary>
    /// Gets whether audio features are globally disabled at the bot level.
    /// </summary>
    public bool IsAudioGloballyDisabled { get; set; }

    /// <summary>
    /// Gets whether the member portal is enabled for this guild.
    /// </summary>
    public bool IsMemberPortalEnabled { get; set; }

    /// <summary>
    /// Gets whether the bot has Azure Speech set up. Without it nothing can be synthesized, so the
    /// page says so up front instead of failing on the first Send.
    /// </summary>
    public bool IsTtsConfigured { get; set; }

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

            IsTtsConfigured = _ttsService.IsConfigured;

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

            Header = BuildHeader(guild.Id, guild.Name, guild.IconUrl,
                "Audio", $"Manage audio settings and TTS for {guild.Name}");
            Header.Actions = headerActions;

            Navigation = BuildNavigation(guild.Id, "audio");

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
            ErrorMessage = "The text-to-speech page could not be loaded. Try again in a moment.";
            ViewModel = new TtsIndexViewModel { GuildId = guildId };

            // Set fallback voice channel panel
            VoiceChannelPanel = new VoiceChannelPanelViewModel { GuildId = guildId };

            return Page();
        }
    }

    /// <summary>
    /// Handles POST requests to save the server's default TTS settings. Answers JSON.
    /// </summary>
    public async Task<IActionResult> OnPostUpdateSettingsAsync(
        ulong guildId,
        [FromBody] UpdateTtsSettingsDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to update TTS settings for guild {GuildId}", guildId);

        if (request == null)
        {
            return Failure("The settings could not be read. Reload the page and try again.");
        }

        if (request.RateLimitPerMinute < 1 || request.RateLimitPerMinute > 60)
        {
            return Failure("The rate limit must be a whole number from 1 to 60 messages a minute.", field: "rateLimitPerMinute");
        }

        try
        {
            var settings = await _ttsSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);

            settings.DefaultVoice = string.IsNullOrWhiteSpace(request.DefaultVoice) ? settings.DefaultVoice : request.DefaultVoice;
            settings.DefaultSpeed = Math.Clamp(request.DefaultSpeed, 0.5, 2.0);
            settings.DefaultPitch = Math.Clamp(request.DefaultPitch, 0.5, 2.0);
            settings.DefaultVolume = Math.Clamp(request.DefaultVolume, 0.0, 1.0);
            settings.AutoPlayOnSend = request.AutoPlayOnSend;
            settings.AnnounceJoinsLeaves = request.AnnounceJoinsLeaves;
            settings.RateLimitPerMinute = request.RateLimitPerMinute;

            await _ttsSettingsService.UpdateSettingsAsync(settings, cancellationToken);

            _logger.LogInformation("Successfully updated TTS settings for guild {GuildId}", guildId);
            return new JsonResult(new { success = true, message = "Saved as the server's default voice settings." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating TTS settings for guild {GuildId}", guildId);
            return Failure("The settings could not be saved. Try again.", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Handles POST requests to delete a TTS message from history. Answers JSON.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteMessageAsync(
        ulong guildId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to delete TTS message {MessageId} for guild {GuildId}",
            messageId, guildId);

        try
        {
            var deleted = await _ttsHistoryService.DeleteMessageAsync(messageId, cancellationToken);
            if (!deleted)
            {
                return Failure("That message no longer exists. Reload the page to see the latest.", StatusCodes.Status404NotFound);
            }

            var stats = await _ttsHistoryService.GetStatsAsync(guildId, cancellationToken);
            return new JsonResult(new
            {
                success = true,
                message = "Message deleted.",
                messageId = messageId.ToString(),
                stats = BuildStatsPayload(stats)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting TTS message {MessageId} for guild {GuildId}",
                messageId, guildId);
            return Failure("The message could not be deleted. Try again.", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Handles POST requests to send a TTS message to the voice channel. The voice, speed, pitch,
    /// volume and style are the ones on screen; any left out fall back to the server's saved
    /// defaults. Raw SSML (Pro mode) takes precedence over everything else.
    /// </summary>
    public async Task<IActionResult> OnPostSendMessageAsync(
        ulong guildId,
        [FromBody] TtsSendDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to send TTS message for guild {GuildId}", guildId);

        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return Failure("Type a message to send.", field: "message");
        }

        if (!_ttsService.IsConfigured)
        {
            return Failure("Text-to-speech is not set up on this bot yet. Add the Azure Speech settings first.");
        }

        if (!_audioService.IsConnected(guildId))
        {
            return Failure("The bot is not in a voice channel. Join one with the voice panel first.", code: "not_connected");
        }

        try
        {
            var settings = await _ttsSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);

            if (request.Message.Length > settings.MaxMessageLength)
            {
                return Failure($"That message is {request.Message.Length} characters; the limit is {settings.MaxMessageLength}.", field: "message");
            }

            var options = ResolveOptions(request, settings);

            using var audioStream = await SynthesizeAsync(request, options, cancellationToken);

            var playbackResult = await _ttsPlaybackService.PlayAsync(
                guildId,
                User.GetDiscordUserId(),
                User.FindFirst("discord:username")?.Value ?? "Admin UI",
                request.Message,
                options.Voice ?? string.Empty,
                audioStream,
                cancellationToken);

            if (!playbackResult.Success)
            {
                _logger.LogWarning("TTS playback failed for guild {GuildId}: {ErrorMessage}", guildId, playbackResult.ErrorMessage);
                return Failure(playbackResult.ErrorMessage ?? "The message could not be played in the voice channel.");
            }

            var ttsMessage = playbackResult.LoggedMessage!;
            _logger.LogInformation("Successfully played TTS message for guild {GuildId}", guildId);

            var stats = await _ttsHistoryService.GetStatsAsync(guildId, cancellationToken);
            return new JsonResult(new
            {
                success = true,
                message = "Message sent to the voice channel.",
                stats = BuildStatsPayload(stats),
                recentMessage = new
                {
                    id = ttsMessage.Id.ToString(),
                    userId = ttsMessage.UserId.ToString(),
                    username = ttsMessage.Username,
                    message = ttsMessage.Message,
                    voice = ttsMessage.Voice,
                    durationFormatted = FormatDuration(ttsMessage.DurationSeconds)
                }
            });
        }
        catch (Exception ex) when (ex is TtsUpstreamUnavailableException or SsmlValidationException or InvalidOperationException or ArgumentException)
        {
            return SynthesisFailure(ex, guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending TTS message for guild {GuildId}", guildId);
            return Failure("The message could not be sent. Try again.", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Handles POST requests to preview a message in the browser. Same settings handling as Send,
    /// but nothing is played in Discord and nothing is saved to history.
    /// </summary>
    public async Task<IActionResult> OnPostPreviewAsync(
        ulong guildId,
        [FromBody] TtsSendDto request,
        CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return Failure("Type a message to preview.", field: "message");
        }

        if (!_ttsService.IsConfigured)
        {
            return Failure("Text-to-speech is not set up on this bot yet. Add the Azure Speech settings first.");
        }

        try
        {
            var settings = await _ttsSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);

            if (request.Message.Length > settings.MaxMessageLength)
            {
                return Failure($"That message is {request.Message.Length} characters; the limit is {settings.MaxMessageLength}.", field: "message");
            }

            var options = ResolveOptions(request, settings);
            using var audioStream = await SynthesizeAsync(request, options, cancellationToken);
            return File(WavAudio.WrapPcm(audioStream), "audio/wav", "tts-preview.wav");
        }
        catch (Exception ex) when (ex is TtsUpstreamUnavailableException or SsmlValidationException or InvalidOperationException or ArgumentException)
        {
            return SynthesisFailure(ex, guildId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing TTS message for guild {GuildId}", guildId);
            return Failure("The preview could not be made. Try again.", StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// The voice settings for one message: what was on screen, else the saved defaults, always in range.
    /// </summary>
    internal static TtsOptions ResolveOptions(TtsSendDto request, GuildTtsSettings settings)
    {
        var voice = !string.IsNullOrWhiteSpace(request.Voice)
            ? request.Voice
            : string.IsNullOrWhiteSpace(settings.DefaultVoice) ? "en-US-JennyNeural" : settings.DefaultVoice;

        return new TtsOptions
        {
            Voice = voice,
            Speed = Math.Clamp(request.Speed ?? settings.DefaultSpeed, 0.5, 2.0),
            Pitch = Math.Clamp(request.Pitch ?? settings.DefaultPitch, 0.5, 2.0),
            Volume = Math.Clamp(request.Volume ?? settings.DefaultVolume, 0.0, 1.0)
        };
    }

    private async Task<Stream> SynthesizeAsync(TtsSendDto request, TtsOptions options, CancellationToken cancellationToken)
    {
        // Pro mode: the SSML on screen is sent as written
        if (!string.IsNullOrWhiteSpace(request.Ssml))
        {
            return await _ttsService.SynthesizeSpeechAsync(request.Ssml, null, SynthesisMode.Ssml, cancellationToken);
        }

        // A style needs SSML around the message
        if (!string.IsNullOrWhiteSpace(request.Style))
        {
            var intensity = Math.Clamp(request.StyleIntensity ?? 1.0m, 0.01m, 2.0m);
            var builder = _ssmlBuilder.Reset()
                .BeginDocument("en-US")
                .WithVoice(options.Voice ?? "en-US-JennyNeural")
                .WithStyle(request.Style, (double)intensity);

            if (Math.Abs(options.Speed - 1.0) > 0.01 || Math.Abs(options.Pitch - 1.0) > 0.01)
            {
                builder.WithProsody(rate: options.Speed, pitch: options.Pitch);
                builder.AddText(request.Message);
                builder.EndProsody();
            }
            else
            {
                builder.AddText(request.Message);
            }

            builder.EndStyle().EndVoice();
            return await _ttsService.SynthesizeSpeechAsync(builder.Build(), null, SynthesisMode.Ssml, cancellationToken);
        }

        return await _ttsService.SynthesizeSpeechAsync(request.Message, options, cancellationToken);
    }

    /// <summary>
    /// Plain-language answer for a synthesis failure. The exception text goes to the log, never to the page.
    /// </summary>
    private JsonResult SynthesisFailure(Exception ex, ulong guildId)
    {
        switch (ex)
        {
            case TtsUpstreamUnavailableException unavailable:
                _logger.LogError(ex, "Azure Speech unreachable for guild {GuildId} after {Attempts} attempt(s)", guildId, unavailable.Attempts);
                return Failure("The speech service could not be reached. This is usually temporary; try again in a moment.", StatusCodes.Status503ServiceUnavailable, code: "tts_upstream_unavailable");
            case SsmlValidationException invalid:
                _logger.LogWarning(ex, "SSML validation failed for guild {GuildId}", guildId);
                return Failure("The SSML is not valid: " + string.Join("; ", invalid.Errors), field: "message");
            case ArgumentException:
                _logger.LogError(ex, "Invalid TTS request for guild {GuildId}", guildId);
                return Failure("That message or those voice settings could not be used. Check them and try again.");
            default:
                _logger.LogWarning(ex, "TTS service error for guild {GuildId}", guildId);
                return Failure("The text-to-speech service could not process this message. Try again.");
        }
    }

    private static JsonResult Failure(string message, int status = StatusCodes.Status400BadRequest, string? field = null, string? code = null)
        => new(new { success = false, message, field, code }) { StatusCode = status };

    private static object BuildStatsPayload(TtsStatsDto stats) => new
    {
        messagesToday = stats.MessagesToday,
        totalPlaybackFormatted = FormatPlaybackTime(stats.TotalPlaybackSeconds),
        uniqueUsers = stats.UniqueUsers
    };

    /// <summary>The settings form's body.</summary>
    public class UpdateTtsSettingsDto
    {
        public string? DefaultVoice { get; set; }
        public double DefaultSpeed { get; set; } = 1.0;
        public double DefaultPitch { get; set; } = 1.0;
        public double DefaultVolume { get; set; } = 1.0;
        public bool AutoPlayOnSend { get; set; }
        public bool AnnounceJoinsLeaves { get; set; }
        public int RateLimitPerMinute { get; set; } = 10;
    }

    /// <summary>One message to send or preview, with the voice settings on screen.</summary>
    public class TtsSendDto
    {
        public string Message { get; set; } = string.Empty;
        public string? Voice { get; set; }
        public double? Speed { get; set; }
        public double? Pitch { get; set; }
        public double? Volume { get; set; }
        public string? Style { get; set; }
        public decimal? StyleIntensity { get; set; }
        public string? Ssml { get; set; }
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
            // Now playing and Stop come over SignalR as the bot speaks. A spoken message has no
            // known length up front, so show "Playing..." rather than a progress bar.
            ShowProgress = false
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
