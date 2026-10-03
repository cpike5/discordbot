using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Tts;
using DiscordBot.Core.DTOs.Portal;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Exceptions;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for member portal TTS style presets: built-in and user-defined presets,
/// and previewing a TTS message as downloadable WAV audio.
/// </summary>
[ApiController]
[Route("api/portal/tts/{guildId}")]
[Authorize(Policy = "PortalGuildMember")]
public class PortalTtsPresetsController : PortalTtsControllerBase
{
    private readonly ITtsSettingsService _ttsSettingsService;
    private readonly IStylePresetProvider _stylePresetProvider;
    private readonly CustomTtsPresetService _customPresets;
    private readonly ILogger<PortalTtsPresetsController> _logger;

    public PortalTtsPresetsController(
        ITtsSendPipeline sendPipeline,
        ITtsSettingsService ttsSettingsService,
        IStylePresetProvider stylePresetProvider,
        CustomTtsPresetService customPresets,
        ILogger<PortalTtsPresetsController> logger)
        : base(sendPipeline)
    {
        _ttsSettingsService = ttsSettingsService;
        _stylePresetProvider = stylePresetProvider;
        _customPresets = customPresets;
        _logger = logger;
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
        if (!User.TryGetDiscordUserId(out var userId))
            return Unauthorized();

        var presets = await _customPresets.ListAsync(userId, cancellationToken);
        return Ok(presets.Select(CustomTtsPresetService.ToResponse));
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
        if (!User.TryGetDiscordUserId(out var userId))
            return Unauthorized();

        var result = await _customPresets.CreateAsync(userId, request.ToInput(), cancellationToken);
        if (!result.Succeeded)
        {
            return BadRequest(new ApiErrorDto
            {
                Message = result.Message!,
                Detail = result.Detail,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = result.ErrorCode
            });
        }

        return StatusCode(StatusCodes.Status201Created, CustomTtsPresetService.ToResponse(result.Preset!));
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
        if (!User.TryGetDiscordUserId(out var userId))
            return Unauthorized();

        return await _customPresets.DeleteAsync(userId, id, cancellationToken) ? NoContent() : NotFound();
    }

    /// <summary>
    /// Request model for creating a custom TTS preset (member portal and admin endpoints).
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

        /// <summary>The same values as the service takes them.</summary>
        public CustomTtsPresetInput ToInput() => new(Name, VoiceName, Style, Speed, Pitch, Icon);
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
        if (!await _sendPipeline.IsAudioGloballyEnabledAsync())
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
                Message = "TTS is not enabled for this server",
                Detail = "Contact a server administrator to enable TTS in server settings.",
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
            audioStream = await _sendPipeline.SynthesizeFromRequestAsync(request, cancellationToken);
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
                // The exception text is for the log; the person gets what to do about it
                Detail = "That message or those voice settings could not be used for a preview. Check them and try again.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "invalid_request"
            });
        }

        // Wrap raw PCM as WAV for browser playback
        using (audioStream)
        {
            var wavStream = WavAudio.WrapPcm(audioStream);
            _logger.LogInformation("Successfully generated TTS preview for guild {GuildId}, WAV size: {Size} bytes",
                guildId, wavStream.Length);
            return File(wavStream, "audio/wav", "tts-preview.wav");
        }
    }
}
