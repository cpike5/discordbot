using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs.Tts;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Exceptions;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for member portal SSML synthesis and validation: validating markup,
/// synthesizing SSML to audio (optionally playing it live), building SSML from
/// structured segments, and looking up per-voice capabilities.
/// </summary>
[ApiController]
[Route("api/portal/tts/{guildId}")]
[Authorize(Policy = "PortalGuildMember")]
public class PortalTtsSynthesisController : PortalTtsControllerBase
{
    private readonly ISsmlBuilder _ssmlBuilder;
    private readonly ITtsSettingsService _ttsSettingsService;
    private readonly IAudioService _audioService;
    private readonly ITtsPlaybackService _ttsPlaybackService;
    private readonly ITtsService _ttsService;
    private readonly ISsmlValidator _ssmlValidator;
    private readonly IVoiceCapabilityProvider _voiceCapabilityProvider;
    private readonly ILogger<PortalTtsSynthesisController> _logger;

    public PortalTtsSynthesisController(
        ITtsSendPipeline sendPipeline,
        ISsmlBuilder ssmlBuilder,
        ITtsSettingsService ttsSettingsService,
        IAudioService audioService,
        ITtsPlaybackService ttsPlaybackService,
        ITtsService ttsService,
        ISsmlValidator ssmlValidator,
        IVoiceCapabilityProvider voiceCapabilityProvider,
        ILogger<PortalTtsSynthesisController> logger)
        : base(sendPipeline)
    {
        _ssmlBuilder = ssmlBuilder;
        _ttsSettingsService = ttsSettingsService;
        _audioService = audioService;
        _ttsPlaybackService = ttsPlaybackService;
        _ttsService = ttsService;
        _ssmlValidator = ssmlValidator;
        _voiceCapabilityProvider = voiceCapabilityProvider;
        _logger = logger;
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
        if (request.PlayInVoiceChannel && !await _sendPipeline.IsAudioGloballyEnabledAsync())
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
                var truncatedMessage = plainText.Length > _sendPipeline.MaxDisplayMessageLength
                    ? plainText.Substring(0, _sendPipeline.MaxDisplayMessageLength)
                    : plainText;
                _sendPipeline.CurrentMessages.AddOrUpdate(guildId, truncatedMessage, (k, v) => truncatedMessage);
                _sendPipeline.PlaybackState.AddOrUpdate(guildId, true, (k, v) => true);

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
                    _sendPipeline.PlaybackState.TryRemove(guildId, out _);
                    _sendPipeline.CurrentMessages.TryRemove(guildId, out _);
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
}
