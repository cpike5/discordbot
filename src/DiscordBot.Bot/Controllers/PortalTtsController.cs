using System.Collections.Concurrent;
using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.Portal;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for member portal TTS operations.
/// Provides text-to-speech functionality for authenticated guild members.
/// </summary>
[ApiController]
[Route("api/portal/tts/{guildId}")]
[Authorize(Policy = "PortalGuildMember")]
public class PortalTtsController : ControllerBase
{
    private readonly ITtsService _ttsService;
    private readonly ITtsSettingsService _ttsSettingsService;
    private readonly ITtsMessageRepository _ttsMessageRepository;
    private readonly IAudioService _audioService;
    private readonly IPlaybackService _playbackService;
    private readonly ISettingsService _settingsService;
    private readonly DiscordSocketClient _discordClient;
    private readonly AzureSpeechOptions _azureSpeechOptions;
    private readonly ILogger<PortalTtsController> _logger;

    // Track current TTS message being played per guild
    private static readonly ConcurrentDictionary<ulong, string> _currentMessages = new();

    // Track whether TTS is currently playing per guild
    private static readonly ConcurrentDictionary<ulong, bool> _ttsPlaybackState = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalTtsController"/> class.
    /// </summary>
    /// <param name="ttsService">The TTS service for speech synthesis.</param>
    /// <param name="ttsSettingsService">The TTS settings service.</param>
    /// <param name="ttsMessageRepository">The TTS message repository.</param>
    /// <param name="audioService">The audio service for voice connections.</param>
    /// <param name="playbackService">The playback service for audio control.</param>
    /// <param name="settingsService">The bot-level settings service.</param>
    /// <param name="discordClient">The Discord socket client.</param>
    /// <param name="azureSpeechOptions">The Azure Speech configuration options.</param>
    /// <param name="logger">The logger.</param>
    public PortalTtsController(
        ITtsService ttsService,
        ITtsSettingsService ttsSettingsService,
        ITtsMessageRepository ttsMessageRepository,
        IAudioService audioService,
        IPlaybackService playbackService,
        ISettingsService settingsService,
        DiscordSocketClient discordClient,
        IOptions<AzureSpeechOptions> azureSpeechOptions,
        ILogger<PortalTtsController> logger)
    {
        _ttsService = ttsService;
        _ttsSettingsService = ttsSettingsService;
        _ttsMessageRepository = ttsMessageRepository;
        _audioService = audioService;
        _playbackService = playbackService;
        _settingsService = settingsService;
        _discordClient = discordClient;
        _azureSpeechOptions = azureSpeechOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Checks if audio features are globally enabled at the bot level.
    /// </summary>
    /// <returns>True if audio is globally enabled, false otherwise.</returns>
    private async Task<bool> IsAudioGloballyEnabledAsync()
    {
        return await _settingsService.GetSettingValueAsync<bool?>("Features:AudioEnabled") ?? true;
    }

    /// <summary>
    /// Gets the bot's current TTS connection status and playback state.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <returns>TTS connection status and current message.</returns>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetStatus(ulong guildId)
    {
        _logger.LogDebug("Get TTS status request for guild {GuildId}", guildId);

        var isConnected = _audioService.IsConnected(guildId);
        var channelId = _audioService.GetConnectedChannelId(guildId);
        string? channelName = null;

        if (channelId.HasValue)
        {
            var guild = _discordClient.GetGuild(guildId);
            var channel = guild?.GetVoiceChannel(channelId.Value);
            channelName = channel?.Name;
        }

        // Check both soundboard and TTS playback
        var isSoundboardPlaying = _playbackService.IsPlaying(guildId);
        var isTtsPlaying = _ttsPlaybackState.TryGetValue(guildId, out var ttsPlaying) && ttsPlaying;
        var isPlaying = isSoundboardPlaying || isTtsPlaying;

        var currentMessage = _currentMessages.TryGetValue(guildId, out var message) ? message : null;

        var response = new TtsStatusResponse
        {
            IsConnected = isConnected,
            ChannelId = channelId,
            ChannelName = channelName,
            IsPlaying = isPlaying,
            CurrentMessage = currentMessage,
            MaxMessageLength = _azureSpeechOptions.MaxTextLength
        };

        return Ok(response);
    }

    /// <summary>
    /// Synthesizes and plays a TTS message in the bot's current voice channel.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The TTS request containing message and voice settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpPost("send")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SendTts(
        ulong guildId,
        [FromBody] SendTtsRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Send TTS request for guild {GuildId}, voice {Voice}", guildId, request.Voice);

        // Check if audio is globally enabled at the bot level
        if (!await IsAudioGloballyEnabledAsync())
        {
            _logger.LogWarning("Audio features globally disabled - rejecting SendTts for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Audio features disabled",
                Detail = "Audio features have been disabled by an administrator.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Check if TTS is enabled for this guild
        var settings = await _ttsSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);
        if (!settings.TtsEnabled)
        {
            _logger.LogWarning("TTS not enabled for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "TTS is not enabled for this guild",
                Detail = "Contact a server administrator to enable TTS in guild settings.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Check if bot is connected to voice
        if (!_audioService.IsConnected(guildId))
        {
            _logger.LogWarning("Bot not connected to voice channel in guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Not connected to voice channel",
                Detail = "The bot must be connected to a voice channel before sending TTS messages.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Validate message is not empty
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            _logger.LogWarning("Empty TTS message provided for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Message cannot be empty",
                Detail = "Please provide a message to synthesize.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Validate message length against guild settings
        if (request.Message.Length > settings.MaxMessageLength)
        {
            _logger.LogWarning("TTS message too long for guild {GuildId} (length: {Length}, max: {Max})",
                guildId, request.Message.Length, settings.MaxMessageLength);
            return BadRequest(new ApiErrorDto
            {
                Message = "Message too long",
                Detail = $"Message length ({request.Message.Length}) exceeds the maximum allowed ({settings.MaxMessageLength}).",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Check rate limit
        var userId = User.GetDiscordUserId();
        if (await _ttsSettingsService.IsUserRateLimitedAsync(guildId, userId, cancellationToken))
        {
            _logger.LogWarning("User {UserId} rate limited for TTS in guild {GuildId}", userId, guildId);
            return StatusCode(StatusCodes.Status429TooManyRequests, new ApiErrorDto
            {
                Message = "Rate limit exceeded",
                Detail = $"You have exceeded the rate limit of {settings.RateLimitPerMinute} messages per minute. Please wait before sending more messages.",
                StatusCode = StatusCodes.Status429TooManyRequests,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Create TTS options
        var options = new TtsOptions
        {
            Voice = request.Voice,
            Speed = request.Speed,
            Pitch = request.Pitch
        };

        // Synthesize speech
        Stream audioStream;
        try
        {
            audioStream = await _ttsService.SynthesizeSpeechAsync(request.Message, options, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "TTS service not configured for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "TTS service not available",
                Detail = "The text-to-speech service is not properly configured.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid TTS request for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid TTS request",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Calculate duration from audio stream
        var durationSeconds = CalculateAudioDuration(audioStream);

        // Update current message tracking (truncate to 50 characters)
        var truncatedMessage = request.Message.Length > 50
            ? request.Message.Substring(0, 50)
            : request.Message;
        _currentMessages.AddOrUpdate(guildId, truncatedMessage, (k, v) => truncatedMessage);

        // Mark TTS as playing
        _ttsPlaybackState.AddOrUpdate(guildId, true, (k, v) => true);

        // Get the PCM stream for playback
        var pcmStream = _audioService.GetOrCreatePcmStream(guildId);
        if (pcmStream == null)
        {
            _logger.LogError("Failed to get PCM stream for guild {GuildId}", guildId);
            _currentMessages.TryRemove(guildId, out _);
            _ttsPlaybackState.TryRemove(guildId, out _);
            return BadRequest(new ApiErrorDto
            {
                Message = "Failed to get audio stream",
                Detail = "Please try reconnecting to the voice channel.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Stream the audio to Discord
        try
        {
            // Start activity for Discord audio streaming
            using var streamActivity = BotActivitySource.StartDiscordAudioStreamActivity(
                guildId: guildId,
                durationSeconds: durationSeconds);

            try
            {
                var bytesWritten = 0L;
                var buffer = new byte[3840]; // Match PlaybackService buffer size (20ms at 48kHz stereo 16-bit)
                int bytesRead;

                while ((bytesRead = await audioStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await pcmStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    bytesWritten += bytesRead;
                }

                await pcmStream.FlushAsync(cancellationToken);

                // Record streaming metrics
                BotActivitySource.RecordAudioStreamMetrics(
                    streamActivity,
                    bytesWritten: bytesWritten,
                    bufferCount: (int)(bytesWritten / 3840));

                // Update activity to prevent auto-leave
                _audioService.UpdateLastActivity(guildId);

                _logger.LogInformation("Successfully played TTS message for guild {GuildId}. Bytes written: {BytesWritten}",
                    guildId, bytesWritten);

                BotActivitySource.SetSuccess(streamActivity);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stream TTS audio for guild {GuildId}", guildId);
                BotActivitySource.RecordException(streamActivity, ex);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stream TTS audio for guild {GuildId}", guildId);
            _currentMessages.TryRemove(guildId, out _);
            _ttsPlaybackState.TryRemove(guildId, out _);
            return BadRequest(new ApiErrorDto
            {
                Message = "Failed to play TTS",
                Detail = "An error occurred while streaming audio to Discord.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
        finally
        {
            // Clear TTS playback state and message tracking after streaming completes
            _ttsPlaybackState.TryRemove(guildId, out _);
            _currentMessages.TryRemove(guildId, out _);
        }

        // Log to database
        var ttsMessage = new TtsMessage
        {
            Id = Guid.NewGuid(),
            GuildId = guildId,
            UserId = userId,
            Username = User.Identity?.Name ?? "Portal User",
            Message = request.Message,
            Voice = request.Voice,
            DurationSeconds = durationSeconds,
            CreatedAt = DateTime.UtcNow
        };
        await _ttsMessageRepository.AddAsync(ttsMessage, cancellationToken);

        _logger.LogInformation("Successfully sent TTS message for guild {GuildId}", guildId);
        return Ok(new { Message = "TTS message sent successfully", DurationSeconds = durationSeconds });
    }

    /// <summary>
    /// Gets all available voice channels in the guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <returns>List of voice channels.</returns>
    [HttpGet("channels")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public IActionResult GetVoiceChannels(ulong guildId)
    {
        _logger.LogInformation("Get voice channels request for guild {GuildId}", guildId);

        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return NotFound(new ApiErrorDto
            {
                Message = "Guild not found",
                Detail = "The requested guild was not found or the bot is not a member.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var voiceChannels = guild.VoiceChannels
            .OrderBy(c => c.Position)
            .Select(c => new
            {
                id = c.Id.ToString(), // Discord snowflake IDs must be strings in JSON
                name = c.Name
            })
            .ToList();

        _logger.LogInformation("Returning {Count} voice channels for guild {GuildId}", voiceChannels.Count, guildId);
        return Ok(voiceChannels);
    }

    /// <summary>
    /// Joins a voice channel in the guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The join request containing the channel ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpPost("channel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> JoinChannel(
        ulong guildId,
        [FromBody] JoinChannelRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Join channel request for guild {GuildId}, channel {ChannelId}", guildId, request.ChannelId);

        // Check if audio is globally enabled at the bot level
        if (!await IsAudioGloballyEnabledAsync())
        {
            _logger.LogWarning("Audio features globally disabled - rejecting JoinChannel for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Audio features disabled",
                Detail = "Audio features have been disabled by an administrator.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Check if TTS is enabled for this guild
        var settings = await _ttsSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);
        if (!settings.TtsEnabled)
        {
            _logger.LogWarning("TTS not enabled for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "TTS is not enabled for this guild",
                Detail = "Contact a server administrator to enable TTS in guild settings.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var audioClient = await _audioService.JoinChannelAsync(guildId, request.ChannelId, cancellationToken);
        if (audioClient == null)
        {
            _logger.LogWarning("Failed to join channel {ChannelId} in guild {GuildId}", request.ChannelId, guildId);
            return NotFound(new ApiErrorDto
            {
                Message = "Failed to join voice channel",
                Detail = "The guild or voice channel was not found, or the bot lacks permission to join.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Successfully joined channel {ChannelId} in guild {GuildId}", request.ChannelId, guildId);
        return Ok(new { Message = "Joined voice channel", ChannelId = request.ChannelId.ToString() });
    }

    /// <summary>
    /// Leaves the current voice channel in the guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("channel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LeaveChannel(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Leave channel request for guild {GuildId}", guildId);

        if (!_audioService.IsConnected(guildId))
        {
            _logger.LogDebug("Not connected to voice in guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Not connected to voice",
                Detail = "The bot is not currently connected to a voice channel in this guild.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Stop any playback first
        await _playbackService.StopAsync(guildId, cancellationToken);

        // Clear TTS playback state and message tracking
        _ttsPlaybackState.TryRemove(guildId, out _);
        _currentMessages.TryRemove(guildId, out _);

        var success = await _audioService.LeaveChannelAsync(guildId, cancellationToken);
        if (!success)
        {
            _logger.LogWarning("Failed to leave channel in guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Failed to leave voice channel",
                Detail = "An error occurred while disconnecting from the voice channel.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Successfully left voice channel in guild {GuildId}", guildId);
        return Ok(new { Message = "Left voice channel" });
    }

    /// <summary>
    /// Stops the currently playing TTS message in the guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpPost("stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StopPlayback(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stop TTS playback request for guild {GuildId}", guildId);

        if (!_audioService.IsConnected(guildId))
        {
            _logger.LogDebug("Not connected to voice in guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Not connected to voice",
                Detail = "The bot is not currently connected to a voice channel in this guild.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Check if anything is playing (soundboard or TTS)
        var isSoundboardPlaying = _playbackService.IsPlaying(guildId);
        var isTtsPlaying = _ttsPlaybackState.TryGetValue(guildId, out var ttsPlaying) && ttsPlaying;

        if (!isSoundboardPlaying && !isTtsPlaying)
        {
            _logger.LogDebug("Nothing playing in guild {GuildId}", guildId);
            return Ok(new { Message = "Nothing playing" });
        }

        // Stop soundboard playback if active
        if (isSoundboardPlaying)
        {
            await _playbackService.StopAsync(guildId, cancellationToken);
        }

        // Clear TTS playback state and message tracking
        _ttsPlaybackState.TryRemove(guildId, out _);
        _currentMessages.TryRemove(guildId, out _);

        _logger.LogInformation("Successfully stopped TTS playback in guild {GuildId}", guildId);
        return Ok(new { Message = "Playback stopped" });
    }

    /// <summary>
    /// Calculates the duration of an audio stream in seconds.
    /// Assumes 48kHz sample rate, 16-bit samples, stereo (Discord standard PCM format).
    /// </summary>
    /// <param name="audioStream">The audio stream to measure.</param>
    /// <returns>Duration in seconds.</returns>
    private static double CalculateAudioDuration(Stream audioStream)
    {
        const int sampleRate = 48000;
        const int bytesPerSample = 2; // 16-bit
        const int channels = 2; // stereo
        const int bytesPerSecond = sampleRate * bytesPerSample * channels; // 192000

        return audioStream.Length / (double)bytesPerSecond;
    }

    /// <summary>
    /// Request model for joining a voice channel.
    /// </summary>
    public class JoinChannelRequest
    {
        /// <summary>
        /// Gets or sets the voice channel ID to join.
        /// </summary>
        public ulong ChannelId { get; set; }
    }
}
