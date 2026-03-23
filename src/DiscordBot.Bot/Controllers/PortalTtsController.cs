using System.Collections.Concurrent;
using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Tracing;
using DiscordBot.Core.Configuration;
using Elastic.Apm;
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
    private readonly IUserTtsPresetRepository _userTtsPresetRepository;
    private readonly ITtsMessageHistoryRepository _ttsMessageHistoryRepository;
    private readonly ILogger<PortalTtsController> _logger;

    // Track current TTS message being played per guild
    private static readonly ConcurrentDictionary<ulong, string> _currentMessages = new();

    // Track whether TTS is currently playing per guild
    private static readonly ConcurrentDictionary<ulong, bool> _ttsPlaybackState = new();

    // Track active playback cancellation tokens per guild for stop support
    private static readonly ConcurrentDictionary<ulong, CancellationTokenSource> _playbackCancellationTokens = new();

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
    /// <param name="userTtsPresetRepository">The user TTS preset repository.</param>
    /// <param name="ttsMessageHistoryRepository">The TTS message history repository.</param>
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
        IUserTtsPresetRepository userTtsPresetRepository,
        ITtsMessageHistoryRepository ttsMessageHistoryRepository,
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
        _userTtsPresetRepository = userTtsPresetRepository;
        _ttsMessageHistoryRepository = ttsMessageHistoryRepository;
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "audio_disabled"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "tts_not_enabled"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "not_connected"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "empty_message"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "message_too_long"
            });
        }

        // Check rate limit
        var userId = User.GetDiscordUserId();

        // Enrich APM transaction for Kibana visibility
        try
        {
            var transaction = Elastic.Apm.Agent.Tracer.CurrentTransaction;
            if (transaction != null)
            {
                transaction.Name = "portal.tts.send";
                transaction.SetLabel("guild_id", guildId.ToString());
                transaction.SetLabel("voice", request.Voice ?? "default");
                transaction.SetLabel("text_length", request.Message?.Length ?? 0);
                transaction.SetLabel("synthesis_mode", !string.IsNullOrWhiteSpace(request.Ssml) ? "ssml" : !string.IsNullOrWhiteSpace(request.Style) ? "style" : "plain");
                transaction.SetLabel("user_id", userId.ToString());
            }
        }
        catch { /* APM not available */ }

        if (await _ttsSettingsService.IsUserRateLimitedAsync(guildId, userId, cancellationToken))
        {
            _logger.LogWarning("User {UserId} rate limited for TTS in guild {GuildId}", userId, guildId);
            return StatusCode(StatusCodes.Status429TooManyRequests, new ApiErrorDto
            {
                Message = "Rate limit exceeded",
                Detail = $"You have exceeded the rate limit of {settings.RateLimitPerMinute} messages per minute. Please wait before sending more messages.",
                StatusCode = StatusCodes.Status429TooManyRequests,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "rate_limited"
            });
        }

        // Synthesize speech based on request parameters
        Stream audioStream;
        try
        {
            audioStream = await SynthesizeFromRequestAsync(request, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "TTS service not configured for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "TTS service not available",
                Detail = "The text-to-speech service is not properly configured.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "tts_not_configured"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "ssml_validation_failed"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "invalid_request"
            });
        }

        // Update current message tracking (truncate to MaxDisplayMessageLength characters)
        var truncatedMessage = request.Message.Length > MaxDisplayMessageLength
            ? request.Message.Substring(0, MaxDisplayMessageLength)
            : request.Message;
        _currentMessages.AddOrUpdate(guildId, truncatedMessage, (k, v) => truncatedMessage);

        // Mark TTS as playing
        _ttsPlaybackState.AddOrUpdate(guildId, true, (k, v) => true);

        // Create a cancellation token that can be triggered by the stop endpoint
        // Link it with the request token so both HTTP disconnect and stop button work
        // Do NOT use 'using' — lifetime is managed explicitly via TryRemove in finally/StopPlayback
        var playbackCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        // Atomically swap in the new CTS, capturing any previous one for disposal
        CancellationTokenSource? previousCts = null;
        _playbackCancellationTokens.AddOrUpdate(guildId, playbackCts, (_, existing) =>
        {
            previousCts = existing;
            return playbackCts;
        });
        if (previousCts != null)
        {
            await previousCts.CancelAsync();
            previousCts.Dispose();
        }

        // Play the audio using the TTS playback service
        TtsPlaybackResult playbackResult;
        try
        {
            playbackResult = await _ttsPlaybackService.PlayAsync(
                guildId,
                userId,
                User.FindFirst("discord:username")?.Value ?? "Portal User",
                request.Message,
                request.Voice,
                audioStream,
                playbackCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Cancelled by the stop endpoint, not by HTTP disconnect — return success
            _logger.LogInformation("TTS playback was stopped by user for guild {GuildId}", guildId);
            return Ok(new { Message = "Playback stopped" });
        }
        finally
        {
            // Whoever wins TryRemove owns disposal — prevents double-dispose with StopPlayback
            if (_playbackCancellationTokens.TryRemove(guildId, out var removedCts))
                removedCts.Dispose();
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "play_failed"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "guild_not_found"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "audio_disabled"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "tts_not_enabled"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "channel_not_found"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "not_connected"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "leave_failed"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "not_connected"
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

        // Cancel active TTS playback if in progress
        if (_playbackCancellationTokens.TryRemove(guildId, out var cts))
        {
            await cts.CancelAsync();
            cts.Dispose();
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "ssml_empty"
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

        // Enrich APM transaction for Kibana visibility
        try
        {
            var transaction = Elastic.Apm.Agent.Tracer.CurrentTransaction;
            if (transaction != null)
            {
                transaction.Name = "portal.tts.synthesize_ssml";
                transaction.SetLabel("guild_id", guildId.ToString());
                transaction.SetLabel("ssml_length", request.Ssml?.Length ?? 0);
                transaction.SetLabel("play_in_voice_channel", request.PlayInVoiceChannel);
            }
        }
        catch { /* APM not available */ }

        // Check if SSML is empty
        if (string.IsNullOrWhiteSpace(request.Ssml))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "SSML cannot be empty",
                Detail = "Please provide SSML markup to synthesize.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "ssml_empty"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "ssml_not_enabled"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "audio_disabled"
            });
        }

        // Validate SSML
        var validationResult = _ssmlValidator.Validate(request.Ssml);

        // Add voice count label to APM transaction
        try
        {
            var transaction = Elastic.Apm.Agent.Tracer.CurrentTransaction;
            transaction?.SetLabel("voice_count", validationResult.DetectedVoices.Count);
        }
        catch { /* APM not available */ }

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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "ssml_validation_failed"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "ssml_complexity_exceeded"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "tts_not_configured"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "ssml_validation_failed"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "invalid_request"
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
                        TraceId = HttpContext.GetCorrelationId(),
                        ErrorCode = "not_connected"
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
                        User.FindFirst("discord:username")?.Value ?? "Portal User",
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
                        TraceId = HttpContext.GetCorrelationId(),
                        ErrorCode = "play_failed"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "invalid_request"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "invalid_request"
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
    /// Gets the authenticated user's custom TTS presets.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of the user's custom presets.</returns>
    [HttpGet("presets/custom")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCustomPresets(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst("discord:user_id")?.Value;
        if (userIdClaim == null || !ulong.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var presets = await _userTtsPresetRepository.GetByUserIdAsync(userId, cancellationToken);

        var result = presets.Select(p => new
        {
            p.Id,
            p.Name,
            p.VoiceName,
            p.Style,
            Speed = (double)p.Speed,
            Pitch = (double)p.Pitch,
            p.Icon,
            p.CreatedAt
        });

        return Ok(result);
    }

    /// <summary>
    /// Creates a new custom TTS preset for the authenticated user.
    /// Enforces a maximum of 20 presets per user.
    /// </summary>
    /// <param name="request">The preset data to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created preset.</returns>
    [HttpPost("presets/custom")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateCustomPreset(
        [FromBody] CreateCustomPresetRequest request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst("discord:user_id")?.Value;
        if (userIdClaim == null || !ulong.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Preset name is required",
                Detail = "Please provide a name for the preset.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "invalid_request"
            });
        }

        if (request.Name.Length > 50)
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Preset name too long",
                Detail = "Preset name must be 50 characters or fewer.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "invalid_request"
            });
        }

        if (string.IsNullOrWhiteSpace(request.VoiceName))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Voice name is required",
                Detail = "Please select a voice for the preset.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "invalid_request"
            });
        }

        // Enforce maximum 20 presets per user
        var currentCount = await _userTtsPresetRepository.GetCountByUserIdAsync(userId, cancellationToken);
        if (currentCount >= 20)
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Maximum presets reached",
                Detail = "You can have at most 20 custom presets. Please delete an existing preset first.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "preset_limit_reached"
            });
        }

        var preset = new UserTtsPreset
        {
            UserId = userId,
            Name = request.Name.Trim(),
            VoiceName = request.VoiceName.Trim(),
            Style = string.IsNullOrWhiteSpace(request.Style) ? null : request.Style.Trim(),
            Speed = (decimal)Math.Clamp(request.Speed, 0.5, 2.0),
            Pitch = (decimal)Math.Clamp(request.Pitch, 0.5, 2.0),
            Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        var created = await _userTtsPresetRepository.AddAsync(preset, cancellationToken);

        _logger.LogInformation("User {UserId} created custom TTS preset '{PresetName}' (ID: {PresetId})",
            userId, created.Name, created.Id);

        return StatusCode(StatusCodes.Status201Created, new
        {
            created.Id,
            created.Name,
            created.VoiceName,
            created.Style,
            Speed = (double)created.Speed,
            Pitch = (double)created.Pitch,
            created.Icon,
            created.CreatedAt
        });
    }

    /// <summary>
    /// Deletes a custom TTS preset. Verifies the authenticated user owns the preset.
    /// </summary>
    /// <param name="id">The preset ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("presets/custom/{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCustomPreset(int id, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst("discord:user_id")?.Value;
        if (userIdClaim == null || !ulong.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var preset = await _userTtsPresetRepository.GetByIdAsync(id, cancellationToken);
        if (preset == null || preset.UserId != userId)
        {
            return NotFound();
        }

        await _userTtsPresetRepository.DeleteAsync(preset, cancellationToken);

        _logger.LogInformation("User {UserId} deleted custom TTS preset '{PresetName}' (ID: {PresetId})",
            userId, preset.Name, preset.Id);

        return NoContent();
    }

    /// <summary>
    /// Request model for creating a custom TTS preset.
    /// </summary>
    public class CreateCustomPresetRequest
    {
        /// <summary>User-defined name for the preset.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Azure TTS voice name.</summary>
        public string VoiceName { get; set; } = string.Empty;

        /// <summary>Optional speaking style.</summary>
        public string? Style { get; set; }

        /// <summary>Speech rate multiplier (0.5 to 2.0).</summary>
        public double Speed { get; set; } = 1.0;

        /// <summary>Pitch adjustment multiplier (0.5 to 2.0).</summary>
        public double Pitch { get; set; } = 1.0;

        /// <summary>Optional icon identifier.</summary>
        public string? Icon { get; set; }
    }

    /// <summary>
    /// Previews a TTS message by synthesizing speech and returning WAV audio for browser playback.
    /// Does not require a voice channel connection and does not save to TTS history.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The TTS request containing message and voice settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>WAV audio file for browser playback.</returns>
    [HttpPost("preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewTts(
        ulong guildId,
        [FromBody] SendTtsRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Preview TTS request for guild {GuildId}, voice {Voice}", guildId, request.Voice);

        // Check if audio is globally enabled at the bot level
        if (!await IsAudioGloballyEnabledAsync())
        {
            _logger.LogWarning("Audio features globally disabled - rejecting PreviewTts for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Audio features disabled",
                Detail = "Audio features have been disabled by an administrator.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "audio_disabled"
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
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "tts_not_enabled"
            });
        }

        // Validate message is not empty
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            _logger.LogWarning("Empty TTS message provided for preview in guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Message cannot be empty",
                Detail = "Please provide a message to preview.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "empty_message"
            });
        }

        // Validate message length against guild settings
        if (request.Message.Length > settings.MaxMessageLength)
        {
            _logger.LogWarning("TTS preview message too long for guild {GuildId} (length: {Length}, max: {Max})",
                guildId, request.Message.Length, settings.MaxMessageLength);
            return BadRequest(new ApiErrorDto
            {
                Message = "Message too long",
                Detail = $"Message length ({request.Message.Length}) exceeds the maximum allowed ({settings.MaxMessageLength}).",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "message_too_long"
            });
        }

        // Synthesize speech
        Stream audioStream;
        try
        {
            audioStream = await SynthesizeFromRequestAsync(request, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "TTS service not configured for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "TTS service not available",
                Detail = "The text-to-speech service is not properly configured.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "tts_not_configured"
            });
        }
        catch (SsmlValidationException ex)
        {
            _logger.LogWarning(ex, "SSML validation failed for preview in guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "SSML validation failed",
                Detail = string.Join("; ", ex.Errors),
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "ssml_validation_failed"
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid TTS preview request for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid TTS request",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "invalid_request"
            });
        }

        // Wrap raw PCM as WAV for browser playback
        using (audioStream)
        {
            var wavStream = WrapPcmAsWav(audioStream);
            _logger.LogInformation("Successfully generated TTS preview for guild {GuildId}, WAV size: {Size} bytes",
                guildId, wavStream.Length);
            return File(wavStream, "audio/wav", "tts-preview.wav");
        }
    }

    /// <summary>
    /// Synthesizes speech from a TTS request, handling SSML, style, and plain text modes.
    /// </summary>
    /// <param name="request">The TTS request containing message and voice settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A stream containing the synthesized PCM audio.</returns>
    private async Task<Stream> SynthesizeFromRequestAsync(SendTtsRequest request, CancellationToken cancellationToken)
    {
        // If SSML is provided, use SSML synthesis directly
        if (!string.IsNullOrWhiteSpace(request.Ssml))
        {
            _logger.LogDebug("Using SSML synthesis");
            return await _ttsService.SynthesizeSpeechAsync(request.Ssml, null, SynthesisMode.Ssml, cancellationToken);
        }

        // If Style is provided, use SSML builder to wrap message with style
        if (!string.IsNullOrWhiteSpace(request.Style))
        {
            _logger.LogDebug("Using style '{Style}' with intensity {Intensity}",
                request.Style, request.StyleIntensity ?? 1.0m);

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
            return await _ttsService.SynthesizeSpeechAsync(ssml, null, SynthesisMode.Ssml, cancellationToken);
        }

        // Otherwise, use standard TTS synthesis
        _logger.LogDebug("Using standard TTS synthesis");
        var options = new TtsOptions
        {
            Voice = request.Voice,
            Speed = request.Speed,
            Pitch = request.Pitch
        };
        return await _ttsService.SynthesizeSpeechAsync(request.Message, options, cancellationToken);
    }

    /// <summary>
    /// Wraps raw PCM audio data in a WAV container for browser playback.
    /// </summary>
    /// <param name="pcmStream">The raw PCM audio stream.</param>
    /// <param name="sampleRate">Sample rate in Hz (default: 48000).</param>
    /// <param name="bitsPerSample">Bits per sample (default: 16).</param>
    /// <param name="channels">Number of audio channels (default: 2 for stereo).</param>
    /// <returns>A MemoryStream containing valid WAV data.</returns>
    private static MemoryStream WrapPcmAsWav(Stream pcmStream, int sampleRate = 48000, int bitsPerSample = 16, int channels = 2)
    {
        var pcmData = new MemoryStream();
        pcmStream.CopyTo(pcmData);
        var dataLength = (int)pcmData.Length;

        var wav = new MemoryStream(44 + dataLength);
        using var writer = new BinaryWriter(wav, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);                                           // PCM chunk size
        writer.Write((short)1);                                     // Audio format (PCM)
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * bitsPerSample / 8);    // Byte rate
        writer.Write((short)(channels * bitsPerSample / 8));        // Block align
        writer.Write((short)bitsPerSample);
        writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        pcmData.Position = 0;
        pcmData.CopyTo(wav);
        wav.Position = 0;
        return wav;
    }

    /// <summary>
    /// Gets recent TTS message history for the current user.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="limit">Maximum number of entries to return (default 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of recent history entries.</returns>
    [HttpGet("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(ulong guildId, [FromQuery] int limit = 20, CancellationToken cancellationToken = default)
    {
        var userId = User.GetDiscordUserId();
        if (userId == 0)
        {
            return Unauthorized();
        }

        var entries = await _ttsMessageHistoryRepository.GetRecentAsync(userId, guildId, Math.Clamp(limit, 1, 50), cancellationToken);

        return Ok(entries.Select(e => new
        {
            id = e.Id,
            message = e.Message,
            voiceName = e.VoiceName,
            style = e.Style,
            speed = e.Speed,
            pitch = e.Pitch,
            isFavorite = e.IsFavorite,
            playedAt = e.PlayedAt
        }));
    }

    /// <summary>
    /// Saves a new TTS message history entry.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The history entry to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created history entry.</returns>
    [HttpPost("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveHistory(
        ulong guildId,
        [FromBody] SaveTtsHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetDiscordUserId();
        if (userId == 0)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Message cannot be empty",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "empty_message"
            });
        }

        if (string.IsNullOrWhiteSpace(request.VoiceName))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Voice name is required",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "missing_voice"
            });
        }

        var entry = new TtsMessageHistory
        {
            GuildId = guildId,
            UserId = userId,
            Message = request.Message,
            VoiceName = request.VoiceName,
            Style = request.Style,
            Speed = (decimal)request.Speed,
            Pitch = (decimal)request.Pitch,
            IsFavorite = false,
            PlayedAt = DateTime.UtcNow
        };

        await _ttsMessageHistoryRepository.AddAsync(entry, cancellationToken);

        _logger.LogInformation("Saved TTS history entry {Id} for user {UserId} in guild {GuildId}",
            entry.Id, userId, guildId);

        return Ok(new
        {
            id = entry.Id,
            message = entry.Message,
            voiceName = entry.VoiceName,
            style = entry.Style,
            speed = entry.Speed,
            pitch = entry.Pitch,
            isFavorite = entry.IsFavorite,
            playedAt = entry.PlayedAt
        });
    }

    /// <summary>
    /// Replays a TTS message from history with its original settings.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="id">The history entry ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Playback result.</returns>
    [HttpPost("history/{id}/replay")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReplayHistory(
        ulong guildId,
        int id,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetDiscordUserId();
        if (userId == 0)
        {
            return Unauthorized();
        }

        var entry = await _ttsMessageHistoryRepository.GetByIdAsync(id, cancellationToken);
        if (entry == null)
        {
            return NotFound(new ApiErrorDto
            {
                Message = "History entry not found",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "entry_not_found"
            });
        }

        // Verify ownership
        if (entry.UserId != userId || entry.GuildId != guildId)
        {
            return Forbid();
        }

        // Replay by calling SendTts with the original settings
        var request = new SendTtsRequest
        {
            Message = entry.Message,
            Voice = entry.VoiceName,
            Style = entry.Style,
            Speed = (double)entry.Speed,
            Pitch = (double)entry.Pitch
        };

        return await SendTts(guildId, request, cancellationToken);
    }

    /// <summary>
    /// Toggles the favorite status of a TTS history entry.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="id">The history entry ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated favorite status.</returns>
    [HttpPut("history/{id}/favorite")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ToggleTtsHistoryFavorite(ulong guildId, int id, CancellationToken cancellationToken = default)
    {
        var userId = User.GetDiscordUserId();
        if (userId == 0)
        {
            return Unauthorized();
        }

        var entry = await _ttsMessageHistoryRepository.GetByIdAsync(id, cancellationToken);
        if (entry == null)
        {
            return NotFound(new ApiErrorDto
            {
                Message = "History entry not found",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "entry_not_found"
            });
        }

        // Verify ownership
        if (entry.UserId != userId || entry.GuildId != guildId)
        {
            return Forbid();
        }

        var newFavoriteStatus = !entry.IsFavorite;
        await _ttsMessageHistoryRepository.SetFavoriteAsync(id, newFavoriteStatus, cancellationToken);

        return Ok(new { id, isFavorite = newFavoriteStatus });
    }

    /// <summary>
    /// Deletes a TTS history entry.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="id">The history entry ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("history/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteTtsHistoryEntry(ulong guildId, int id, CancellationToken cancellationToken = default)
    {
        var userId = User.GetDiscordUserId();
        if (userId == 0)
        {
            return Unauthorized();
        }

        var entry = await _ttsMessageHistoryRepository.GetByIdAsync(id, cancellationToken);
        if (entry == null)
        {
            return NotFound(new ApiErrorDto
            {
                Message = "History entry not found",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "entry_not_found"
            });
        }

        // Verify ownership
        if (entry.UserId != userId || entry.GuildId != guildId)
        {
            return Forbid();
        }

        await _ttsMessageHistoryRepository.DeleteAsync(entry, cancellationToken);

        return Ok(new { success = true });
    }

    /// <summary>
    /// Request model for saving a TTS history entry.
    /// </summary>
    public class SaveTtsHistoryRequest
    {
        /// <summary>
        /// Gets or sets the TTS message text.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the voice name used.
        /// </summary>
        public string VoiceName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional voice style.
        /// </summary>
        public string? Style { get; set; }

        /// <summary>
        /// Gets or sets the speech speed multiplier.
        /// </summary>
        public double Speed { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets the pitch adjustment.
        /// </summary>
        public double Pitch { get; set; } = 1.0;
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
