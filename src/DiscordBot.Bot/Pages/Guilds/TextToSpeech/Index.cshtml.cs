using Discord.WebSocket;
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
public class IndexModel : PageModel
{
    private readonly ITtsHistoryService _ttsHistoryService;
    private readonly ITtsSettingsService _ttsSettingsService;
    private readonly ITtsService _ttsService;
    private readonly IAudioService _audioService;
    private readonly DiscordSocketClient _discordClient;
    private readonly IGuildService _guildService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ITtsHistoryService ttsHistoryService,
        ITtsSettingsService ttsSettingsService,
        ITtsService ttsService,
        IAudioService audioService,
        DiscordSocketClient discordClient,
        IGuildService guildService,
        ILogger<IndexModel> logger)
    {
        _ttsHistoryService = ttsHistoryService;
        _ttsSettingsService = ttsSettingsService;
        _ttsService = ttsService;
        _audioService = audioService;
        _discordClient = discordClient;
        _guildService = guildService;
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
            // Get guild info from service
            var guild = await _guildService.GetGuildByIdAsync(guildId, cancellationToken);
            if (guild == null)
            {
                _logger.LogWarning("Guild {GuildId} not found", guildId);
                return NotFound();
            }

            // Get TTS settings (creates defaults if not found)
            var settings = await _ttsSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);

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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the index page.</returns>
    public async Task<IActionResult> OnPostUpdateSettingsAsync(
        ulong guildId,
        [FromForm] string defaultVoice,
        [FromForm] double defaultSpeed,
        [FromForm] double defaultPitch,
        [FromForm] double defaultVolume,
        [FromForm] bool autoPlayOnSend,
        [FromForm] bool announceJoinsLeaves,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to update TTS settings for guild {GuildId}", guildId);

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

            await _ttsSettingsService.UpdateSettingsAsync(settings, cancellationToken);

            _logger.LogInformation("Successfully updated TTS settings for guild {GuildId}", guildId);
            SuccessMessage = "TTS settings updated successfully.";

            return RedirectToPage("Index", new { guildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating TTS settings for guild {GuildId}", guildId);
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
    /// <returns>Redirect to the index page.</returns>
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

            if (deleted)
            {
                _logger.LogInformation("Successfully deleted TTS message {MessageId}", messageId);
                SuccessMessage = "Message deleted successfully.";
            }
            else
            {
                _logger.LogWarning("TTS message {MessageId} not found", messageId);
                ErrorMessage = "Message not found.";
            }

            return RedirectToPage("Index", new { guildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting TTS message {MessageId} for guild {GuildId}",
                messageId, guildId);
            ErrorMessage = "An error occurred while deleting the message. Please try again.";
            return RedirectToPage("Index", new { guildId });
        }
    }

    /// <summary>
    /// Handles POST requests to send a TTS message to the voice channel.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID from route parameter.</param>
    /// <param name="message">The message text to synthesize and play.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Redirect to the index page.</returns>
    public async Task<IActionResult> OnPostSendMessageAsync(
        ulong guildId,
        [FromForm] string message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("User attempting to send TTS message for guild {GuildId}", guildId);

        if (string.IsNullOrWhiteSpace(message))
        {
            ErrorMessage = "Message cannot be empty.";
            return RedirectToPage("Index", new { guildId });
        }

        if (!_ttsService.IsConfigured)
        {
            ErrorMessage = "TTS service is not configured. Please configure Azure Speech settings.";
            return RedirectToPage("Index", new { guildId });
        }

        if (!_audioService.IsConnected(guildId))
        {
            ErrorMessage = "Bot is not connected to a voice channel. Please join a voice channel first.";
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

            // Synthesize the speech
            using var audioStream = await _ttsService.SynthesizeSpeechAsync(message, options, cancellationToken);

            // Get the PCM stream for playback
            var pcmStream = _audioService.GetOrCreatePcmStream(guildId);
            if (pcmStream == null)
            {
                ErrorMessage = "Failed to get audio stream. Please try reconnecting to the voice channel.";
                return RedirectToPage("Index", new { guildId });
            }

            // Stream the audio to Discord
            await audioStream.CopyToAsync(pcmStream, cancellationToken);
            await pcmStream.FlushAsync(cancellationToken);

            // Update activity to prevent auto-leave
            _audioService.UpdateLastActivity(guildId);

            // Record in history
            var ttsMessage = new TtsMessage
            {
                Id = Guid.NewGuid(),
                GuildId = guildId,
                UserId = User.GetDiscordUserId(),
                Username = User.Identity?.Name ?? "Admin UI",
                Message = message,
                Voice = options.Voice ?? string.Empty,
                DurationSeconds = CalculateAudioDuration(audioStream),
                CreatedAt = DateTime.UtcNow
            };
            await _ttsHistoryService.LogMessageAsync(ttsMessage, cancellationToken);

            _logger.LogInformation("Successfully played TTS message for guild {GuildId}", guildId);
            SuccessMessage = "Message sent to voice channel.";

            return RedirectToPage("Index", new { guildId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "TTS service error for guild {GuildId}: {Message}", guildId, ex.Message);
            ErrorMessage = ex.Message;
            return RedirectToPage("Index", new { guildId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending TTS message for guild {GuildId}", guildId);
            ErrorMessage = "An error occurred while sending the message. Please try again.";
            return RedirectToPage("Index", new { guildId });
        }
    }

    /// <summary>
    /// Calculates the duration of PCM audio stream in seconds.
    /// </summary>
    /// <param name="audioStream">The audio stream (48kHz, 16-bit, stereo PCM).</param>
    /// <returns>Duration in seconds.</returns>
    /// <remarks>
    /// PCM format: 48kHz sample rate, 16-bit (2 bytes per sample), stereo (2 channels).
    /// Bytes per second = 48000 samples/sec * 2 bytes/sample * 2 channels = 192000 bytes/sec.
    /// </remarks>
    private static double CalculateAudioDuration(Stream audioStream)
    {
        const int sampleRate = 48000;
        const int bytesPerSample = 2; // 16-bit
        const int channels = 2; // stereo
        const int bytesPerSecond = sampleRate * bytesPerSample * channels; // 192000

        return audioStream.Length / (double)bytesPerSecond;
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
            AvailableChannels = availableChannels
            // NowPlaying and Queue will be populated via SignalR in real-time
        };
    }
}
