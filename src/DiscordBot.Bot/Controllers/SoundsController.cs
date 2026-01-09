using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for soundboard sound operations including file downloads.
/// </summary>
[ApiController]
[Route("api/guilds/{guildId}/sounds")]
[Authorize(Policy = "RequireViewer")]
public class SoundsController : ControllerBase
{
    private readonly ISoundService _soundService;
    private readonly ISoundFileService _soundFileService;
    private readonly ILogger<SoundsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoundsController"/> class.
    /// </summary>
    /// <param name="soundService">The sound service for metadata operations.</param>
    /// <param name="soundFileService">The sound file service for file operations.</param>
    /// <param name="logger">The logger.</param>
    public SoundsController(
        ISoundService soundService,
        ISoundFileService soundFileService,
        ILogger<SoundsController> logger)
    {
        _soundService = soundService;
        _soundFileService = soundFileService;
        _logger = logger;
    }

    /// <summary>
    /// Downloads a sound file from the guild's soundboard.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="soundId">The sound's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sound file for download.</returns>
    [HttpGet("{soundId}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadSound(
        ulong guildId,
        Guid soundId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Download sound request for sound {SoundId} in guild {GuildId}", soundId, guildId);

        // Get sound metadata with guild validation
        var sound = await _soundService.GetByIdAsync(soundId, guildId, cancellationToken);
        if (sound == null)
        {
            _logger.LogWarning("Sound {SoundId} not found in guild {GuildId}", soundId, guildId);
            return NotFound(new ApiErrorDto
            {
                Message = "Sound not found",
                Detail = "The requested sound does not exist or does not belong to this guild.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Get physical file path
        var filePath = _soundFileService.GetSoundFilePath(guildId, sound.FileName);

        // Verify file exists on disk
        if (!_soundFileService.SoundFileExists(guildId, sound.FileName))
        {
            _logger.LogError("Sound file missing: {FilePath} for sound {SoundId}", filePath, soundId);
            return NotFound(new ApiErrorDto
            {
                Message = "Sound file not found",
                Detail = "The sound exists in the database but the file is missing from storage.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Determine MIME type from file extension
        var extension = Path.GetExtension(sound.FileName).ToLowerInvariant();
        var contentType = extension switch
        {
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".m4a" => "audio/mp4",
            _ => "application/octet-stream"
        };

        // Build download filename from sound name
        var downloadFileName = $"{sound.Name}{extension}";

        _logger.LogInformation("Serving sound file {FileName} as {DownloadFileName}", sound.FileName, downloadFileName);

        // Return file with range request support for audio playback
        return PhysicalFile(filePath, contentType, downloadFileName, enableRangeProcessing: true);
    }
}
