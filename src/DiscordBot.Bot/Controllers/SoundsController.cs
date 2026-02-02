using System.IO.Compression;
using System.Text.Json;
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

    /// <summary>
    /// Exports all sounds from a guild as a ZIP archive with manifest.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>ZIP file containing all sound files and manifest.json.</returns>
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExportAllSounds(
        ulong guildId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Export all sounds request for guild {GuildId}", guildId);

        // Get all sounds for the guild
        var sounds = await _soundService.GetAllByGuildAsync(guildId, cancellationToken);

        if (sounds.Count == 0)
        {
            _logger.LogWarning("No sounds found for guild {GuildId}", guildId);
            return NotFound(new ApiErrorDto
            {
                Message = "No sounds to export",
                Detail = "This guild has no sounds configured.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var exportId = Guid.NewGuid().ToString("N");
        string? zipFilePath = null;

        try
        {
            // Track used filenames for duplicate handling
            var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var soundManifestEntries = new List<object>();
            long totalSizeBytes = 0;

            // Create ZIP archive directly
            zipFilePath = Path.Combine(Path.GetTempPath(), $"sounds_export_{exportId}.zip");
            using (var zipArchive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
            {
                // Stream files directly into ZIP
                foreach (var sound in sounds)
                {
                    var sourcePath = _soundFileService.GetSoundFilePath(guildId, sound.FileName);

                    // Verify file exists
                    if (!_soundFileService.SoundFileExists(guildId, sound.FileName))
                    {
                        _logger.LogWarning(
                            "Sound file missing during export: {FilePath} for sound {SoundId}",
                            sourcePath,
                            sound.Id);
                        continue;
                    }

                    // Sanitize sound name to prevent path traversal
                    var sanitizedName = Path.GetInvalidFileNameChars()
                        .Aggregate(sound.Name, (current, c) => current.Replace(c.ToString(), "_"));

                    // Trim and handle empty names
                    sanitizedName = sanitizedName.Trim();
                    if (string.IsNullOrWhiteSpace(sanitizedName))
                    {
                        sanitizedName = $"sound_{sound.Id:N}";
                    }

                    // Handle reserved Windows filenames
                    string[] reservedNames = ["CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"];
                    if (reservedNames.Contains(sanitizedName, StringComparer.OrdinalIgnoreCase))
                    {
                        sanitizedName = $"_{sanitizedName}";
                    }

                    // Build human-readable filename
                    var extension = Path.GetExtension(sound.FileName);
                    var baseFileName = sanitizedName;
                    var exportFileName = $"{baseFileName}{extension}";

                    // Handle duplicate names with collision-safe counter
                    var counter = 1;
                    while (usedFileNames.Contains(exportFileName))
                    {
                        exportFileName = $"{baseFileName}-{counter}{extension}";
                        counter++;
                    }
                    usedFileNames.Add(exportFileName);

                    // Stream file directly into ZIP
                    var entry = zipArchive.CreateEntry(exportFileName, CompressionLevel.Optimal);
                    using var entryStream = entry.Open();
                    using var sourceFileStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await sourceFileStream.CopyToAsync(entryStream, cancellationToken);

                    totalSizeBytes += sound.FileSizeBytes;

                    // Add to manifest
                    soundManifestEntries.Add(new
                    {
                        id = sound.Id.ToString(),
                        name = sound.Name,
                        fileName = exportFileName,
                        originalFileName = sound.FileName,
                        durationSeconds = sound.DurationSeconds,
                        fileSizeBytes = sound.FileSizeBytes,
                        playCount = sound.PlayCount,
                        uploadedById = sound.UploadedById?.ToString(),
                        uploadedAt = sound.UploadedAt.ToString("O")
                    });
                }

                // Check if any sounds were actually exported
                if (soundManifestEntries.Count == 0)
                {
                    _logger.LogWarning("All sound files missing for guild {GuildId}", guildId);
                    return NotFound(new ApiErrorDto
                    {
                        Message = "No sounds to export",
                        Detail = "All sound files are missing from storage.",
                        StatusCode = StatusCodes.Status404NotFound,
                        TraceId = HttpContext.GetCorrelationId()
                    });
                }

                // Create manifest.json
                var manifest = new
                {
                    exportedAt = DateTime.UtcNow.ToString("O"),
                    guildId = guildId.ToString(),
                    guildName = sounds.FirstOrDefault()?.Guild?.Name ?? guildId.ToString(),
                    totalSounds = soundManifestEntries.Count,
                    totalSizeBytes,
                    sounds = soundManifestEntries
                };

                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                var manifestJson = JsonSerializer.Serialize(manifest, jsonOptions);
                var manifestEntry = zipArchive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using var manifestStream = manifestEntry.Open();
                using var manifestWriter = new StreamWriter(manifestStream);
                await manifestWriter.WriteAsync(manifestJson.AsMemory(), cancellationToken);
            }

            _logger.LogInformation(
                "Successfully exported {SoundCount} sounds for guild {GuildId}. Export ID: {ExportId}",
                soundManifestEntries.Count,
                guildId,
                exportId);

            // Stream the ZIP file
            var fileStream = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.DeleteOnClose);
            var downloadFileName = $"{sounds.FirstOrDefault()?.Guild?.Name ?? guildId.ToString()}_sounds_export.zip";

            return File(fileStream, "application/zip", downloadFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to export sounds for guild {GuildId}. Export ID: {ExportId}",
                guildId,
                exportId);

            // Clean up ZIP file if it was created
            if (zipFilePath != null && System.IO.File.Exists(zipFilePath))
            {
                try
                {
                    System.IO.File.Delete(zipFilePath);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to delete incomplete ZIP file: {ZipPath}", zipFilePath);
                }
            }

            return StatusCode(StatusCodes.Status500InternalServerError, new ApiErrorDto
            {
                Message = "Export failed",
                Detail = "An error occurred while creating the export archive.",
                StatusCode = StatusCodes.Status500InternalServerError,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }
}
