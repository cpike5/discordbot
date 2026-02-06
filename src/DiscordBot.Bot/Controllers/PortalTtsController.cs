using System.Collections.Concurrent;
using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.Portal;
using DiscordBot.Core.DTOs.Tts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Exceptions;
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
    private readonly ITtsPlaybackService _ttsPlaybackService;
    private readonly ISettingsService _settingsService;
    private readonly DiscordSocketClient _discordClient;
    private readonly AzureSpeechOptions _azureSpeechOptions;
    private readonly IVoiceCapabilityProvider _voiceCapabilityProvider;
    private readonly IStylePresetProvider _stylePresetProvider;
    private readonly ISsmlValidator _ssmlValidator;
    private readonly ISsmlBuilder _ssmlBuilder;
    private readonly ILogger<PortalTtsController> _logger;

    // Track current TTS message being played per guild
    private static readonly ConcurrentDictionary<ulong, string> _currentMessages = new();

    // Track whether TTS is currently playing per guild
    private static readonly ConcurrentDictionary<ulong, bool> _ttsPlaybackState = new();

    private const int MaxDisplayMessageLength = 50;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalTtsController"/> class.
    /// </summary>
    /// <param name="ttsService">The TTS service for speech synthesis.</param>
    /// <param name="ttsSettingsService">The TTS settings service.</param>
    /// <param name="ttsMessageRepository">The TTS message repository.</param>
    /// <param name="audioService">The audio service for voice connections.</param>
    /// <param name="playbackService">The playback service for audio control.</param>
    /// <param name="ttsPlaybackService">The TTS playback service for orchestrating playback.</param>
    /// <param name="settingsService">The bot-level settings service.</param>
    /// <param name="discordClient">The Discord socket client.</param>
    /// <param name="azureSpeechOptions">The Azure Speech configuration options.</param>
    /// <param name="voiceCapabilityProvider">The voice capability provider.</param>
    /// <param name="stylePresetProvider">The style preset provider.</param>
    /// <param name="ssmlValidator">The SSML validator.</param>
    /// <param name="ssmlBuilder">The SSML builder.</param>
    /// <param name="logger">The logger.</param>
    public PortalTtsController(
        ITtsService ttsService,
        ITtsSettingsService ttsSettingsService,
        ITtsMessageRepository ttsMessageRepository,
        IAudioService audioService,
        IPlaybackService playbackService,
        ITtsPlaybackService ttsPlaybackService,
        ISettingsService settingsService,
        DiscordSocketClient discordClient,
        IOptions<AzureSpeechOptions> azureSpeechOptions,
        IVoiceCapabilityProvider voiceCapabilityProvider,
        IStylePresetProvider stylePresetProvider,
        ISsmlValidator ssmlValidator,
        ISsmlBuilder ssmlBuilder,
        ILogger<PortalTtsController> logger)
    {
        _ttsService = ttsService;
        _ttsSettingsService = ttsSettingsService;
        _ttsMessageRepository = ttsMessageRepository;
        _audioService = audioService;
        _playbackService = playbackService;
        _ttsPlaybackService = ttsPlaybackService;
        _settingsService = settingsService;
        _discordClient = discordClient;
        _azureSpeechOptions = azureSpeechOptions.Value;
        _voiceCapabilityProvider = voiceCapabilityProvider;
        _stylePresetProvider = stylePresetProvider;
        _ssmlValidator = ssmlValidator;
        _ssmlBuilder = ssmlBuilder;
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

        // Synthesize speech based on request parameters
        Stream audioStream;
        try
        {
            // If SSML is provided, use SSML synthesis directly
            if (!string.IsNullOrWhiteSpace(request.Ssml))
            {
                _logger.LogDebug("Using SSML synthesis for guild {GuildId}", guildId);
                audioStream = await _ttsService.SynthesizeSpeechAsync(request.Ssml, null, SynthesisMode.Ssml, cancellationToken);
            }
            // If Style is provided, use SSML builder to wrap message with style
            else if (!string.IsNullOrWhiteSpace(request.Style))
            {
                _logger.LogDebug("Using style '{Style}' with intensity {Intensity} for guild {GuildId}",
                    request.Style, request.StyleIntensity ?? 1.0m, guildId);

                // Build SSML with style using the ISsmlBuilder
                var styleIntensity = request.StyleIntensity ?? 1.0m;
                var builder = _ssmlBuilder.Reset()
                    .BeginDocument("en-US")
                    .WithVoice(request.Voice)
                    .WithStyle(request.Style, (double)styleIntensity);

                // Apply prosody adjustments (speed/pitch) if different from defaults
                if (Math.Abs(request.Speed - 1.0) > 0.01 || Math.Abs(request.Pitch - 1.0) > 0.01)
                {
                    builder.WithProsody(rate: request.Speed, pitch: request.Pitch);
                    builder.AddText(request.Message);
                    builder.EndProsody();
                }
                else
                {
                    builder.AddText(request.Message);
                }

                builder.EndStyle().EndVoice();
                var ssml = builder.Build();

                _logger.LogDebug("Built SSML with style: {SsmlLength} characters", ssml.Length);
                audioStream = await _ttsService.SynthesizeSpeechAsync(ssml, null, SynthesisMode.Ssml, cancellationToken);
            }
            // Otherwise, use standard TTS synthesis
            else
            {
                _logger.LogDebug("Using standard TTS synthesis for guild {GuildId}", guildId);
                var options = new TtsOptions
                {
                    Voice = request.Voice,
                    Speed = request.Speed,
                    Pitch = request.Pitch
                };
                audioStream = await _ttsService.SynthesizeSpeechAsync(request.Message, options, cancellationToken);
            }
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
        catch (SsmlValidationException ex)
        {
            _logger.LogWarning(ex, "SSML validation failed for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "SSML validation failed",
                Detail = string.Join("; ", ex.Errors),
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

        // Update current message tracking (truncate to MaxDisplayMessageLength characters)
        var truncatedMessage = request.Message.Length > MaxDisplayMessageLength
            ? request.Message.Substring(0, MaxDisplayMessageLength)
            : request.Message;
        _currentMessages.AddOrUpdate(guildId, truncatedMessage, (k, v) => truncatedMessage);

        // Mark TTS as playing
        _ttsPlaybackState.AddOrUpdate(guildId, true, (k, v) => true);

        // Play the audio using the TTS playback service
        TtsPlaybackResult playbackResult;
        try
        {
            playbackResult = await _ttsPlaybackService.PlayAsync(
                guildId,
                userId,
                User.Identity?.Name ?? "Portal User",
                request.Message,
                request.Voice,
                audioStream,
                cancellationToken);
        }
        finally
        {
            // Clear TTS playback state and message tracking after streaming completes
            _ttsPlaybackState.TryRemove(guildId, out _);
            _currentMessages.TryRemove(guildId, out _);
        }

        if (!playbackResult.Success)
        {
            _logger.LogWarning("TTS playback failed for guild {GuildId}: {ErrorMessage}", guildId, playbackResult.ErrorMessage);
            return BadRequest(new ApiErrorDto
            {
                Message = "Failed to play TTS",
                Detail = playbackResult.ErrorMessage ?? "An error occurred while streaming audio to Discord.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Successfully sent TTS message for guild {GuildId}", guildId);
        return Ok(new { Message = "TTS message sent successfully", DurationSeconds = playbackResult.DurationSeconds });
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
    /// Validates SSML markup without synthesizing audio.
    /// </summary>
    /// <param name="request">The validation request containing SSML markup.</param>
    /// <returns>Validation result with errors, warnings, and detected voices.</returns>
    [HttpPost("/api/portal/tts/validate-ssml")]
    [ProducesResponseType(typeof(SsmlValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [AllowAnonymous]
    public IActionResult ValidateSsml([FromBody] Core.DTOs.Tts.SsmlValidationRequest request)
    {
        _logger.LogDebug("Validate SSML request, length: {Length}", request.Ssml.Length);

        if (string.IsNullOrWhiteSpace(request.Ssml))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "SSML cannot be empty",
                Detail = "Please provide SSML markup to validate.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var validationResult = _ssmlValidator.Validate(request.Ssml);

        _logger.LogInformation("SSML validation completed. Valid: {IsValid}, Errors: {ErrorCount}, Warnings: {WarningCount}",
            validationResult.IsValid, validationResult.Errors.Count, validationResult.Warnings.Count);

        return Ok(validationResult);
    }

    /// <summary>
    /// Synthesizes SSML markup to audio. Optionally plays it in the bot's current voice channel.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The synthesis request containing SSML markup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Synthesis result with audio ID, duration, and voices used.</returns>
    [HttpPost("synthesize-ssml")]
    [Authorize(Policy = "ModeratorAccess")]
    [ProducesResponseType(typeof(SsmlSynthesisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SynthesizeSsml(
        ulong guildId,
        [FromBody] Core.DTOs.Tts.SsmlSynthesisRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Synthesize SSML request for guild {GuildId}, PlayInVoiceChannel: {PlayInVoiceChannel}",
            guildId, request.PlayInVoiceChannel);

        // Check if SSML is empty
        if (string.IsNullOrWhiteSpace(request.Ssml))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "SSML cannot be empty",
                Detail = "Please provide SSML markup to synthesize.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Get guild TTS settings
        var settings = await _ttsSettingsService.GetOrCreateSettingsAsync(guildId, cancellationToken);

        // Check if SSML is enabled for this guild
        if (!settings.SsmlEnabled)
        {
            _logger.LogWarning("SSML not enabled for guild {GuildId}", guildId);
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorDto
            {
                Message = "SSML is not enabled for this guild",
                Detail = "Contact a server administrator to enable SSML features in guild TTS settings.",
                StatusCode = StatusCodes.Status403Forbidden,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Check if audio is globally enabled when PlayInVoiceChannel is true
        if (request.PlayInVoiceChannel && !await IsAudioGloballyEnabledAsync())
        {
            _logger.LogWarning("Audio features globally disabled - rejecting SynthesizeSsml with PlayInVoiceChannel for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Audio features disabled",
                Detail = "Audio features have been disabled by an administrator.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Validate SSML
        var validationResult = _ssmlValidator.Validate(request.Ssml);

        // If strict validation is enabled and SSML is invalid, reject
        if (settings.StrictSsmlValidation && !validationResult.IsValid)
        {
            _logger.LogWarning("SSML validation failed for guild {GuildId}. Errors: {Errors}",
                guildId, string.Join(", ", validationResult.Errors));
            return BadRequest(new ApiErrorDto
            {
                Message = "SSML validation failed",
                Detail = $"Validation errors: {string.Join("; ", validationResult.Errors)}",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Check SSML complexity
        var complexity = CalculateSsmlComplexity(request.Ssml);
        if (complexity > settings.MaxSsmlComplexity)
        {
            _logger.LogWarning("SSML complexity {Complexity} exceeds limit {MaxComplexity} for guild {GuildId}",
                complexity, settings.MaxSsmlComplexity, guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "SSML complexity exceeds limit",
                Detail = $"The SSML complexity ({complexity}) exceeds the guild limit ({settings.MaxSsmlComplexity}). Simplify the markup or contact an administrator to increase the limit.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Synthesize the SSML
        Stream audioStream;
        try
        {
            audioStream = await _ttsService.SynthesizeSpeechAsync(request.Ssml, null, SynthesisMode.Ssml, cancellationToken);
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
        catch (SsmlValidationException ex)
        {
            _logger.LogWarning(ex, "SSML validation failed for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "SSML validation failed",
                Detail = string.Join("; ", ex.Errors),
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid SSML for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid SSML",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        try
        {
            var audioId = Guid.NewGuid();
            double durationSeconds;

            // If PlayInVoiceChannel is true, stream to voice channel
            if (request.PlayInVoiceChannel)
            {
                // Check if bot is connected to voice
                if (!_audioService.IsConnected(guildId))
                {
                    _logger.LogWarning("Bot not connected to voice channel in guild {GuildId}", guildId);
                    return BadRequest(new ApiErrorDto
                    {
                        Message = "Not connected to voice channel",
                        Detail = "The bot must be connected to a voice channel to play SSML audio.",
                        StatusCode = StatusCodes.Status400BadRequest,
                        TraceId = HttpContext.GetCorrelationId()
                    });
                }

                // Extract plain text for display and tracking
                var plainText = _ssmlValidator.ExtractPlainText(request.Ssml);
                var truncatedMessage = plainText.Length > MaxDisplayMessageLength
                    ? plainText.Substring(0, MaxDisplayMessageLength)
                    : plainText;
                _currentMessages.AddOrUpdate(guildId, truncatedMessage, (k, v) => truncatedMessage);
                _ttsPlaybackState.AddOrUpdate(guildId, true, (k, v) => true);

                // Reset stream position if seekable, otherwise copy to MemoryStream
                if (audioStream.CanSeek)
                {
                    audioStream.Position = 0;
                }
                else
                {
                    // Stream is not seekable, copy to MemoryStream and dispose original
                    var memoryStream = new MemoryStream();
                    await audioStream.CopyToAsync(memoryStream, cancellationToken);
                    memoryStream.Position = 0;
                    await audioStream.DisposeAsync();
                    audioStream = memoryStream;
                }

                // Play the audio using the TTS playback service
                var userId = User.GetDiscordUserId();
                TtsPlaybackResult playbackResult;
                try
                {
                    playbackResult = await _ttsPlaybackService.PlayAsync(
                        guildId,
                        userId,
                        User.Identity?.Name ?? "Portal User",
                        plainText,
                        validationResult.DetectedVoices.FirstOrDefault() ?? "SSML (multiple voices)",
                        audioStream,
                        cancellationToken);
                }
                finally
                {
                    _ttsPlaybackState.TryRemove(guildId, out _);
                    _currentMessages.TryRemove(guildId, out _);
                }

                if (!playbackResult.Success)
                {
                    _logger.LogWarning("SSML playback failed for guild {GuildId}: {ErrorMessage}", guildId, playbackResult.ErrorMessage);
                    return BadRequest(new ApiErrorDto
                    {
                        Message = "Failed to play SSML audio",
                        Detail = playbackResult.ErrorMessage ?? "An error occurred while streaming audio to Discord.",
                        StatusCode = StatusCodes.Status400BadRequest,
                        TraceId = HttpContext.GetCorrelationId()
                    });
                }

                // Use duration from playback result
                durationSeconds = playbackResult.DurationSeconds;
            }
            else
            {
                // Calculate duration for non-playback response (48kHz, 16-bit, stereo PCM)
                durationSeconds = audioStream.Length / 192000.0;
            }

            var response = new SsmlSynthesisResponse
            {
                AudioId = audioId,
                DurationSeconds = durationSeconds,
                VoicesUsed = validationResult.DetectedVoices.ToList()
            };

            _logger.LogInformation("SSML synthesis completed for guild {GuildId}. Audio ID: {AudioId}, Duration: {Duration}s",
                guildId, audioId, durationSeconds);

            return Ok(response);
        }
        finally
        {
            // Dispose the audio stream when done
            audioStream.Dispose();
        }
    }

    /// <summary>
    /// Builds SSML markup from structured segments.
    /// </summary>
    /// <param name="request">The build request containing segments and elements.</param>
    /// <returns>Built SSML with validation results.</returns>
    [HttpPost("/api/portal/tts/build-ssml")]
    [ProducesResponseType(typeof(Core.DTOs.Tts.SsmlBuildResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [AllowAnonymous]
    public IActionResult BuildSsml([FromBody] Core.DTOs.Tts.SsmlBuildRequest request)
    {
        _logger.LogDebug("Build SSML request, language: {Language}, segments: {SegmentCount}",
            request.Language, request.Segments.Count);

        if (request.Segments.Count == 0)
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "No segments provided",
                Detail = "Please provide at least one SSML segment to build.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        try
        {
            // Build SSML using the builder
            var builder = _ssmlBuilder.Reset().BeginDocument(request.Language);

            foreach (var segment in request.Segments)
            {
                // Add voice if specified
                if (!string.IsNullOrWhiteSpace(segment.Voice))
                {
                    builder.WithVoice(segment.Voice);
                }

                // Add style if specified
                if (!string.IsNullOrWhiteSpace(segment.Style))
                {
                    builder.WithStyle(segment.Style);
                }

                // Add prosody if specified
                if (segment.Rate.HasValue || segment.Pitch.HasValue)
                {
                    builder.WithProsody(rate: segment.Rate, pitch: segment.Pitch);
                }

                // Add plain text if specified
                if (!string.IsNullOrWhiteSpace(segment.Text))
                {
                    builder.AddText(segment.Text);
                }

                // Add elements
                foreach (var element in segment.Elements)
                {
                    switch (element.Type.ToLowerInvariant())
                    {
                        case "text":
                            if (!string.IsNullOrEmpty(element.Text))
                            {
                                builder.AddText(element.Text);
                            }
                            break;

                        case "break":
                            var duration = element.Attributes.GetValueOrDefault("duration", "medium");
                            builder.AddBreak(duration);
                            break;

                        case "emphasis":
                            var text = element.Text ?? "";
                            if (string.IsNullOrWhiteSpace(text))
                                break;
                            var level = element.Attributes.GetValueOrDefault("level", "moderate");
                            builder.AddEmphasis(text, level);
                            break;

                        case "say-as":
                            var sayAsText = element.Text ?? "";
                            var interpretAs = element.Attributes.GetValueOrDefault("interpret-as", "");
                            var format = element.Attributes.GetValueOrDefault("format");
                            builder.AddSayAs(sayAsText, interpretAs, format);
                            break;

                        case "phoneme":
                            var phonemeText = element.Text ?? "";
                            var alphabet = element.Attributes.GetValueOrDefault("alphabet", "ipa");
                            var ph = element.Attributes.GetValueOrDefault("ph", "");
                            builder.AddPhoneme(phonemeText, alphabet, ph);
                            break;

                        case "sub":
                        case "substitution":
                            var alias = element.Attributes.GetValueOrDefault("alias", "");
                            var subText = element.Text ?? "";
                            builder.AddSubstitution(alias, subText);
                            break;

                        default:
                            _logger.LogWarning("Unknown SSML element type: {Type}", element.Type);
                            break;
                    }
                }

                // Close prosody if it was opened
                if (segment.Rate.HasValue || segment.Pitch.HasValue)
                {
                    builder.EndProsody();
                }

                // Close style if it was opened
                if (!string.IsNullOrWhiteSpace(segment.Style))
                {
                    builder.EndStyle();
                }

                // Close voice if it was opened
                if (!string.IsNullOrWhiteSpace(segment.Voice))
                {
                    builder.EndVoice();
                }
            }

            var ssml = builder.Build();

            // Validate the built SSML
            var validationResult = _ssmlValidator.Validate(ssml);

            var response = new Core.DTOs.Tts.SsmlBuildResponse
            {
                Ssml = ssml,
                IsValid = validationResult.IsValid,
                Errors = validationResult.Errors,
                Warnings = validationResult.Warnings
            };

            _logger.LogInformation("SSML build completed. Valid: {IsValid}, Length: {Length}",
                response.IsValid, ssml.Length);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build SSML");
            return BadRequest(new ApiErrorDto
            {
                Message = "Failed to build SSML",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Calculates the complexity of SSML by counting XML elements.
    /// </summary>
    /// <param name="ssml">SSML markup to analyze.</param>
    /// <returns>Complexity score.</returns>
    private static int CalculateSsmlComplexity(string ssml)
    {
        // Count opening tags as a rough approximation of complexity
        return ssml.Split('<').Length - 1;
    }

    /// <summary>
    /// Gets the capabilities of a specific TTS voice.
    /// </summary>
    /// <param name="voiceName">The voice name (e.g., "en-US-JennyNeural").</param>
    /// <returns>Voice capabilities including supported styles.</returns>
    [HttpGet("/api/portal/tts/voices/{voiceName}/capabilities")]
    [ProducesResponseType(typeof(VoiceCapabilities), StatusCodes.Status200OK)]
    [AllowAnonymous]
    public IActionResult GetVoiceCapabilities(string voiceName)
    {
        _logger.LogDebug("Get voice capabilities request for voice {VoiceName}", voiceName);

        var capabilities = _voiceCapabilityProvider.GetCapabilities(voiceName);
        if (capabilities == null)
        {
            _logger.LogWarning("Voice not found in registry, returning fallback capabilities: {VoiceName}", voiceName);
            capabilities = new VoiceCapabilities
            {
                VoiceName = voiceName,
                DisplayName = voiceName,
                Locale = "unknown",
                Gender = "Unknown",
                SupportedStyles = Array.Empty<string>(),
                SupportedRoles = Array.Empty<string>(),
            };
        }

        return Ok(capabilities);
    }

    /// <summary>
    /// Gets all available style presets.
    /// </summary>
    /// <param name="category">Optional category filter (e.g., "Emotional", "Professional").</param>
    /// <returns>List of style presets.</returns>
    [HttpGet("/api/portal/tts/presets")]
    [ProducesResponseType(typeof(IReadOnlyList<StylePreset>), StatusCodes.Status200OK)]
    [AllowAnonymous]
    public IActionResult GetPresets([FromQuery] string? category = null)
    {
        _logger.LogDebug("Get presets request, category filter: {Category}", category ?? "(none)");

        IReadOnlyList<StylePreset> presets;

        if (!string.IsNullOrWhiteSpace(category))
        {
            presets = _stylePresetProvider.GetPresetsByCategory(category);
            _logger.LogDebug("Returning {Count} presets for category {Category}", presets.Count, category);
        }
        else
        {
            presets = _stylePresetProvider.GetAllPresets();
            _logger.LogDebug("Returning all {Count} presets", presets.Count);
        }

        return Ok(presets);
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
