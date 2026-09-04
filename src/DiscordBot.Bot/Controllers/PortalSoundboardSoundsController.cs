using Discord;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for member portal soundboard sound management: listing sounds,
/// downloading sound audio, uploading, and deleting.
/// </summary>
[ApiController]
[Route("api/portal/soundboard/{guildId}")]
[Authorize(Policy = "PortalGuildMember")]
public class PortalSoundboardSoundsController : PortalSoundboardControllerBase
{
    private readonly ISoundService _soundService;
    private readonly ISoundFileService _soundFileService;
    private readonly ISoundboardOrchestrationService _orchestrationService;
    private readonly IGuildAudioSettingsService _audioSettingsService;
    private readonly ILogger<PortalSoundboardSoundsController> _logger;

    public PortalSoundboardSoundsController(
        ISoundService soundService,
        ISoundFileService soundFileService,
        ISoundboardOrchestrationService orchestrationService,
        IGuildAudioSettingsService audioSettingsService,
        ISettingsService settingsService,
        ILogger<PortalSoundboardSoundsController> logger)
        : base(settingsService)
    {
        _soundService = soundService;
        _soundFileService = soundFileService;
        _orchestrationService = orchestrationService;
        _audioSettingsService = audioSettingsService;
        _logger = logger;
    }


    /// <summary>
    /// Gets all sounds for the specified guild with play counts.
    /// Sounds are returned in alphabetical order by name.
    /// Optionally filters by category ID. Use categoryId=0 for uncategorized sounds.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="categoryId">Optional category ID to filter by. Use 0 for uncategorized sounds only.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of sounds with play counts.</returns>
    [HttpGet("sounds")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSounds(ulong guildId, [FromQuery] int? categoryId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Get sounds request for guild {GuildId}, categoryId={CategoryId}", guildId, categoryId);

        // Check if audio is globally enabled at the bot level
        if (!await IsAudioGloballyEnabledAsync())
        {
            _logger.LogWarning("Audio features globally disabled - rejecting GetSounds for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Audio features disabled",
                Detail = "Audio features have been disabled by an administrator.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "audio_disabled"
            });
        }

        // Check if audio is enabled for this guild
        var audioSettings = await _audioSettingsService.GetSettingsAsync(guildId, cancellationToken);
        if (audioSettings == null || !audioSettings.AudioEnabled)
        {
            _logger.LogWarning("Audio not enabled for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Audio is not enabled for this guild",
                Detail = "Enable audio in the guild settings before using soundboard features.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "audio_not_enabled"
            });
        }

        var sounds = await _soundService.GetAllByGuildAsync(guildId, cancellationToken);

        // Apply category filter if specified
        IEnumerable<Sound> filteredSounds = sounds;
        if (categoryId.HasValue)
        {
            if (categoryId.Value == 0)
            {
                // Filter to uncategorized sounds only
                filteredSounds = sounds.Where(s => s.CategoryId == null);
            }
            else
            {
                filteredSounds = sounds.Where(s => s.CategoryId == categoryId.Value);
            }
        }

        var soundList = filteredSounds.ToList();

        var response = new
        {
            sounds = soundList.Select(s => new
            {
                id = s.Id.ToString(),
                name = s.Name,
                playCount = s.PlayCount,
                durationSeconds = s.DurationSeconds,
                uploadedById = s.UploadedById?.ToString(),
                uploadedAt = s.UploadedAt,
                categoryId = s.CategoryId,
                categoryName = s.Category?.Name
            }).ToList(),
            totalCount = soundList.Count
        };

        _logger.LogInformation("Returning {Count} sounds for guild {GuildId}", soundList.Count, guildId);
        return Ok(response);
    }

    /// <summary>
    /// Streams a sound file for browser-side audio preview.
    /// Does not require voice channel connection.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="soundId">The sound's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The audio file stream.</returns>
    [HttpGet("sounds/{soundId}/audio")]
    [ResponseCache(Duration = 300)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSoundAudio(ulong guildId, Guid soundId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Get sound audio request for sound {SoundId} in guild {GuildId}", soundId, guildId);

        if (!await IsAudioGloballyEnabledAsync())
        {
            _logger.LogWarning("Audio features globally disabled - rejecting GetSoundAudio for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Audio features disabled",
                Detail = "Audio features have been disabled by an administrator.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "audio_disabled"
            });
        }

        var audioSettings = await _audioSettingsService.GetSettingsAsync(guildId, cancellationToken);
        if (audioSettings == null || !audioSettings.AudioEnabled)
        {
            _logger.LogWarning("Audio not enabled for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Audio is not enabled for this guild",
                Detail = "Enable audio in the guild settings.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "audio_not_enabled"
            });
        }

        var sound = await _soundService.GetByIdAsync(soundId, guildId, cancellationToken);
        if (sound == null)
        {
            _logger.LogWarning("Sound {SoundId} not found in guild {GuildId}", soundId, guildId);
            return NotFound(new ApiErrorDto
            {
                Message = "Sound not found",
                Detail = "The requested sound was not found in this guild.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "sound_not_found"
            });
        }

        if (!_soundFileService.SoundFileExists(guildId, sound.FileName))
        {
            _logger.LogWarning("Sound file missing for sound {SoundId} ({FileName}) in guild {GuildId}", soundId, sound.FileName, guildId);
            return NotFound(new ApiErrorDto
            {
                Message = "Sound file not found",
                Detail = "The sound file is missing from storage.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "file_not_found"
            });
        }

        var filePath = Path.GetFullPath(_soundFileService.GetSoundFilePath(guildId, sound.FileName));
        var contentType = Path.GetExtension(sound.FileName).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".m4a" => "audio/mp4",
            _ => "application/octet-stream"
        };

        _logger.LogInformation("Streaming sound file {FileName} for sound {SoundId} in guild {GuildId}", sound.FileName, soundId, guildId);
        return PhysicalFile(filePath, contentType);
    }

    /// <summary>
    /// Uploads a new sound file to the guild's soundboard.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="file">The audio file to upload.</param>
    /// <param name="name">The name for the sound (without extension).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created sound metadata.</returns>
    [HttpPost("sounds")]
    // TODO: Add rate limiting [EnableRateLimiting("portal-upload")] when policy is configured
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadSound(
        ulong guildId,
        [FromForm] IFormFile file,
        [FromForm] string name,
        CancellationToken cancellationToken)
    {
        // Validate file is provided
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("No file provided for sound upload in guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "No file provided",
                Detail = "Please select an audio file to upload.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "no_file"
            });
        }

        // Validate name
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("No name provided for sound upload in guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Sound name is required",
                Detail = "Please provide a name for the sound.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "no_name"
            });
        }

        // Extract uploader's Discord user ID from claims
        var userIdClaim = User.FindFirst("discord:user_id")?.Value;
        ulong? uploadedById = userIdClaim != null && ulong.TryParse(userIdClaim, out var parsedUploaderId)
            ? parsedUploaderId
            : null;

        // Delegate to orchestration service
        using var stream = file.OpenReadStream();
        var result = await _orchestrationService.UploadSoundAsync(
            guildId,
            file.FileName,
            name,
            stream,
            file.Length,
            uploadedById,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Upload failed",
                Detail = result.ErrorMessage ?? "Unknown error occurred during upload.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "upload_failed"
            });
        }

        return CreatedAtAction(
            nameof(GetSounds),
            new { guildId },
            new
            {
                id = result.Sound!.Id.ToString(),
                name = result.Sound.Name,
                playCount = result.Sound.PlayCount,
                durationSeconds = result.Sound.DurationSeconds,
                uploadedById = result.Sound.UploadedById?.ToString(),
                uploadedAt = result.Sound.UploadedAt,
                categoryId = result.Sound.CategoryId,
                categoryName = result.Sound.Category?.Name
            });
    }

    /// <summary>
    /// Deletes a sound that was uploaded by the authenticated user.
    /// Only the original uploader can delete their own sounds via the portal.
    /// Filesystem-discovered sounds (null UploadedById) cannot be deleted via this endpoint.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="soundId">The sound's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("sounds/{soundId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSound(ulong guildId, Guid soundId, CancellationToken cancellationToken)
    {
        // Extract user ID from claims
        var userIdClaim = User.FindFirst("discord:user_id")?.Value;
        if (userIdClaim == null || !ulong.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        _logger.LogInformation("Delete sound request for sound {SoundId} in guild {GuildId} by user {UserId}",
            soundId, guildId, userId);

        // Fetch the sound to verify ownership
        var sound = await _soundService.GetByIdAsync(soundId, guildId, cancellationToken);
        if (sound == null)
        {
            return NotFound(new ApiErrorDto
            {
                Message = "Sound not found",
                Detail = "The requested sound was not found in this guild.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "sound_not_found"
            });
        }

        // Verify ownership: only the original uploader can delete via portal
        if (sound.UploadedById == null || sound.UploadedById != userId)
        {
            _logger.LogWarning("User {UserId} attempted to delete sound {SoundId} they did not upload (UploadedById: {UploadedById})",
                userId, soundId, sound.UploadedById);
            return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorDto
            {
                Message = "Not authorized",
                Detail = "You can only delete sounds that you uploaded.",
                StatusCode = StatusCodes.Status403Forbidden,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "not_owner"
            });
        }

        // Delegate to orchestration service
        var result = await _orchestrationService.DeleteSoundAsync(guildId, soundId, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Delete failed",
                Detail = result.ErrorMessage ?? "Unknown error occurred during deletion.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "delete_failed"
            });
        }

        _logger.LogInformation("User {UserId} successfully deleted sound {SoundId} ({SoundName}) in guild {GuildId}",
            userId, soundId, result.DeletedSoundName, guildId);
        return Ok(new { message = "Sound deleted", soundName = result.DeletedSoundName });
    }

    /// <summary>
    /// Plays a sound in the bot's current voice channel.
    /// The bot must be connected to a voice channel before calling this endpoint.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="soundId">The sound's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
}
