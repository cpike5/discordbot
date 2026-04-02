using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Elastic.Apm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for member portal soundboard operations.
/// Provides soundboard functionality for authenticated guild members.
/// </summary>
[ApiController]
[Route("api/portal/soundboard/{guildId}")]
[Authorize(Policy = "PortalGuildMember")]
public class PortalSoundboardController : ControllerBase
{
    private readonly ISoundService _soundService;
    private readonly ISoundFileService _soundFileService;
    private readonly ISoundboardOrchestrationService _orchestrationService;
    private readonly IAudioService _audioService;
    private readonly IPlaybackService _playbackService;
    private readonly IGuildAudioSettingsService _audioSettingsService;
    private readonly ISettingsService _settingsService;
    private readonly IUserSoundFavoriteRepository _favoriteRepository;
    private readonly ISoundCategoryRepository _categoryRepository;
    private readonly ISoundRepository _soundRepository;
    private readonly IGuildMembershipService _guildMembershipService;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<PortalSoundboardController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalSoundboardController"/> class.
    /// </summary>
    public PortalSoundboardController(
        ISoundService soundService,
        ISoundFileService soundFileService,
        ISoundboardOrchestrationService orchestrationService,
        IAudioService audioService,
        IPlaybackService playbackService,
        IGuildAudioSettingsService audioSettingsService,
        ISettingsService settingsService,
        IUserSoundFavoriteRepository favoriteRepository,
        ISoundCategoryRepository categoryRepository,
        ISoundRepository soundRepository,
        IGuildMembershipService guildMembershipService,
        DiscordSocketClient discordClient,
        ILogger<PortalSoundboardController> logger)
    {
        _soundService = soundService;
        _soundFileService = soundFileService;
        _orchestrationService = orchestrationService;
        _audioService = audioService;
        _playbackService = playbackService;
        _audioSettingsService = audioSettingsService;
        _settingsService = settingsService;
        _favoriteRepository = favoriteRepository;
        _categoryRepository = categoryRepository;
        _soundRepository = soundRepository;
        _guildMembershipService = guildMembershipService;
        _discordClient = discordClient;
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
    /// Gets a page of sounds for the specified guild.
    /// Supports search, sort, and category filtering.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Sounds per page (default 40, max 100).</param>
    /// <param name="search">Optional search term (case-insensitive name match).</param>
    /// <param name="sort">Sort order: name-asc (default), name-desc, newest, oldest, most-played.</param>
    /// <param name="categoryId">Optional category filter. Use 0 for uncategorized sounds only.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of sounds with metadata.</returns>
    [HttpGet("sounds")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSounds(
        ulong guildId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 40,
        [FromQuery] string? search = null,
        [FromQuery] string? sort = null,
        [FromQuery] int? categoryId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Get sounds request for guild {GuildId}, page={Page}, pageSize={PageSize}, search={Search}, sort={Sort}, categoryId={CategoryId}",
            guildId, page, pageSize, search, sort, categoryId);

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

        // Clamp pageSize to valid range
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);

        var (sounds, totalCount) = await _soundService.GetByGuildPagedAsync(
            guildId, page, pageSize, search, sort, categoryId, cancellationToken);

        var response = new
        {
            sounds = sounds.Select(s => new
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
            totalCount,
            page,
            pageSize,
            hasMore = (page * pageSize) < totalCount
        };

        _logger.LogInformation(
            "Returning {Count} of {Total} sounds (page {Page}) for guild {GuildId}",
            sounds.Count, totalCount, page, guildId);
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
    [HttpPost("play/{soundId}")]
    // TODO: Add rate limiting [EnableRateLimiting("portal-play")] when policy is configured
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PlaySound(
        ulong guildId,
        Guid soundId,
        CancellationToken cancellationToken)
    {
        // Get audio settings to check queue configuration
        var audioSettings = await _audioSettingsService.GetSettingsAsync(guildId, cancellationToken);
        var queueEnabled = audioSettings?.QueueEnabled ?? false;

        // Get user ID from claims (default to 0 if not found, which indicates portal/API play)
        var userIdClaim = User.FindFirst("discord:user_id")?.Value;
        var userId = userIdClaim != null && ulong.TryParse(userIdClaim, out var parsed) ? parsed : 0UL;

        // Enrich APM transaction for Kibana visibility
        try
        {
            var transaction = Elastic.Apm.Agent.Tracer.CurrentTransaction;
            if (transaction != null)
            {
                transaction.Name = "portal.soundboard.play";
                transaction.SetLabel("guild_id", guildId.ToString());
                transaction.SetLabel("sound_id", soundId.ToString());
                transaction.SetLabel("user_id", userId.ToString());
            }
        }
        catch { /* APM not available */ }

        // Delegate to orchestration service
        var result = await _orchestrationService.PlaySoundAsync(
            guildId,
            soundId,
            userId,
            queueEnabled,
            cancellationToken: cancellationToken);

        if (!result.Success)
        {
            var statusCode = result.ErrorMessage?.Contains("not found") == true
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;

            return StatusCode(statusCode, new ApiErrorDto
            {
                Message = result.Success ? "Playing sound" : "Failed to play sound",
                Detail = result.ErrorMessage ?? "Unknown error occurred during playback.",
                StatusCode = statusCode,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = statusCode == StatusCodes.Status404NotFound ? "sound_not_found" : "play_failed"
            });
        }

        return Ok(new { Message = "Playing sound", SoundName = result.Sound!.Name, SoundId = soundId });
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

        // Check if audio is enabled for this guild
        var audioSettings = await _audioSettingsService.GetSettingsAsync(guildId, cancellationToken);
        if (audioSettings == null || !audioSettings.AudioEnabled)
        {
            _logger.LogWarning("Audio not enabled for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Audio is not enabled for this guild",
                Detail = "Enable audio in the guild settings before using voice features.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "audio_not_enabled"
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
    /// Stops the currently playing sound in the guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpPost("stop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StopPlayback(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stop playback request for guild {GuildId}", guildId);

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

        if (!_playbackService.IsPlaying(guildId))
        {
            _logger.LogDebug("Nothing playing in guild {GuildId}", guildId);
            return Ok(new { Message = "Nothing playing" });
        }

        await _playbackService.StopAsync(guildId, cancellationToken);
        _logger.LogInformation("Successfully stopped playback in guild {GuildId}", guildId);
        return Ok(new { Message = "Playback stopped" });
    }

    /// <summary>
    /// Gets the bot's current connection status and now playing information.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <returns>Connection status and now playing details.</returns>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetStatus(ulong guildId)
    {
        _logger.LogDebug("Get status request for guild {GuildId}", guildId);

        var isConnected = _audioService.IsConnected(guildId);
        var channelId = _audioService.GetConnectedChannelId(guildId);
        string? channelName = null;

        if (channelId.HasValue)
        {
            var guild = _discordClient.GetGuild(guildId);
            var channel = guild?.GetVoiceChannel(channelId.Value);
            channelName = channel?.Name;
        }

        // Note: PlaybackService does not expose CurrentSound publicly, so we cannot return now playing
        // TODO: Add GetCurrentSound method to IPlaybackService or use IsPlaying with state tracking
        var isPlaying = _playbackService.IsPlaying(guildId);

        var response = new
        {
            isConnected,
            channelId = channelId?.ToString(),
            channelName,
            nowPlaying = (string?)null, // Cannot determine currently playing sound without public accessor
            isPlaying
        };

        return Ok(response);
    }

    /// <summary>
    /// Gets the authenticated user's favorited sound IDs for the specified guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of favorited sound IDs.</returns>
    [HttpGet("favorites")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFavorites(ulong guildId, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst("discord:user_id")?.Value;
        if (userIdClaim == null || !ulong.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        _logger.LogDebug("Get favorites request for user {UserId} in guild {GuildId}", userId, guildId);

        var favoriteIds = await _favoriteRepository.GetFavoriteSoundIdsAsync(userId, guildId, cancellationToken);
        return Ok(new { favorites = favoriteIds });
    }

    /// <summary>
    /// Adds a sound to the authenticated user's favorites for the specified guild.
    /// Idempotent: returns success if the sound is already favorited.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="soundId">The sound's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpPost("favorites/{soundId}")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddFavorite(ulong guildId, Guid soundId, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst("discord:user_id")?.Value;
        if (userIdClaim == null || !ulong.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        _logger.LogDebug("Add favorite request for user {UserId}, sound {SoundId} in guild {GuildId}",
            userId, soundId, guildId);

        // Validate that the sound exists in this guild
        var sound = await _soundService.GetByIdAsync(soundId, guildId, cancellationToken);
        if (sound == null)
            return NotFound(new { message = "Sound not found in this guild" });

        var favorite = new UserSoundFavorite
        {
            UserId = userId,
            GuildId = guildId,
            SoundId = soundId,
            FavoritedAt = DateTime.UtcNow
        };

        try
        {
            await _favoriteRepository.AddAsync(favorite, cancellationToken);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException)
        {
            // Unique constraint violation — already favorited (concurrent request or race condition)
            _logger.LogDebug("Sound {SoundId} already favorited by user {UserId} in guild {GuildId}",
                soundId, userId, guildId);
            return Ok(new { message = "Already favorited" });
        }

        _logger.LogInformation("User {UserId} favorited sound {SoundId} in guild {GuildId}",
            userId, soundId, guildId);
        return StatusCode(StatusCodes.Status201Created, new { message = "Favorite added" });
    }

    /// <summary>
    /// Removes a sound from the authenticated user's favorites for the specified guild.
    /// Idempotent: returns success even if the sound was not favorited.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="soundId">The sound's unique identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("favorites/{soundId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RemoveFavorite(ulong guildId, Guid soundId, CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst("discord:user_id")?.Value;
        if (userIdClaim == null || !ulong.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        _logger.LogDebug("Remove favorite request for user {UserId}, sound {SoundId} in guild {GuildId}",
            userId, soundId, guildId);

        await _favoriteRepository.RemoveFavoriteAsync(userId, soundId, guildId, cancellationToken);

        _logger.LogInformation("User {UserId} removed favorite for sound {SoundId} in guild {GuildId}",
            userId, soundId, guildId);
        return Ok(new { message = "Favorite removed" });
    }

    // ─── Category Endpoints ─────────────────────────────────────────────

    /// <summary>
    /// Gets all sound categories for the specified guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of categories ordered by SortOrder then Name.</returns>
    [HttpGet("categories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Get categories request for guild {GuildId}", guildId);

        var categories = await _categoryRepository.GetByGuildAsync(guildId, cancellationToken);

        var response = categories.Select(c => new
        {
            id = c.Id,
            name = c.Name,
            sortOrder = c.SortOrder,
            createdAt = c.CreatedAt
        }).ToList();

        return Ok(new { categories = response });
    }

    /// <summary>
    /// Creates a new sound category for the specified guild. Admin only.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The category creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created category.</returns>
    [HttpPost("categories")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateCategory(
        ulong guildId,
        [FromBody] CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (!await IsGuildAdminAsync())
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Category name is required",
                Detail = "Please provide a name for the category.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "no_name"
            });
        }

        if (request.Name.Length > 50)
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Category name too long",
                Detail = "Category name must be 50 characters or fewer.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "name_too_long"
            });
        }

        // Check for duplicate name in this guild
        var existing = await _categoryRepository.GetByGuildAsync(guildId, cancellationToken);
        if (existing.Any(c => string.Equals(c.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Category already exists",
                Detail = $"A category named '{request.Name.Trim()}' already exists in this guild.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "duplicate_name"
            });
        }

        var category = new SoundCategory
        {
            GuildId = guildId,
            Name = request.Name.Trim(),
            SortOrder = request.SortOrder ?? 0,
            CreatedAt = DateTime.UtcNow
        };

        await _categoryRepository.AddAsync(category, cancellationToken);

        _logger.LogInformation("Created sound category '{CategoryName}' (Id={CategoryId}) in guild {GuildId}",
            category.Name, category.Id, guildId);

        return StatusCode(StatusCodes.Status201Created, new
        {
            id = category.Id,
            name = category.Name,
            sortOrder = category.SortOrder,
            createdAt = category.CreatedAt
        });
    }

    /// <summary>
    /// Updates an existing sound category. Admin only.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="id">The category ID.</param>
    /// <param name="request">The category update request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated category.</returns>
    [HttpPut("categories/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCategory(
        ulong guildId,
        int id,
        [FromBody] UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (!await IsGuildAdminAsync())
            return Forbid();

        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken) as SoundCategory;
        if (category == null || category.GuildId != guildId)
        {
            return NotFound(new ApiErrorDto
            {
                Message = "Category not found",
                Detail = "The requested category was not found in this guild.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "category_not_found"
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            if (request.Name.Length > 50)
            {
                return BadRequest(new ApiErrorDto
                {
                    Message = "Category name too long",
                    Detail = "Category name must be 50 characters or fewer.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId(),
                    ErrorCode = "name_too_long"
                });
            }

            // Check for duplicate name in this guild (excluding current category)
            var existing = await _categoryRepository.GetByGuildAsync(guildId, cancellationToken);
            if (existing.Any(c => c.Id != id && string.Equals(c.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new ApiErrorDto
                {
                    Message = "Category already exists",
                    Detail = $"A category named '{request.Name.Trim()}' already exists in this guild.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId(),
                    ErrorCode = "duplicate_name"
                });
            }

            category.Name = request.Name.Trim();
        }

        if (request.SortOrder.HasValue)
        {
            category.SortOrder = request.SortOrder.Value;
        }

        await _categoryRepository.UpdateAsync(category, cancellationToken);

        _logger.LogInformation("Updated sound category '{CategoryName}' (Id={CategoryId}) in guild {GuildId}",
            category.Name, category.Id, guildId);

        return Ok(new
        {
            id = category.Id,
            name = category.Name,
            sortOrder = category.SortOrder,
            createdAt = category.CreatedAt
        });
    }

    /// <summary>
    /// Deletes a sound category. Sounds in this category become uncategorized. Admin only.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="id">The category ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("categories/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCategory(
        ulong guildId,
        int id,
        CancellationToken cancellationToken)
    {
        if (!await IsGuildAdminAsync())
            return Forbid();

        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken) as SoundCategory;
        if (category == null || category.GuildId != guildId)
        {
            return NotFound(new ApiErrorDto
            {
                Message = "Category not found",
                Detail = "The requested category was not found in this guild.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId(),
                ErrorCode = "category_not_found"
            });
        }

        var categoryName = category.Name;
        await _categoryRepository.DeleteAsync(category, cancellationToken);

        _logger.LogInformation("Deleted sound category '{CategoryName}' (Id={CategoryId}) in guild {GuildId}",
            categoryName, id, guildId);

        return Ok(new { message = "Category deleted", categoryName });
    }

    /// <summary>
    /// Assigns a sound to a category or removes it from its current category.
    /// Pass categoryId: null to uncategorize the sound.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="soundId">The sound's unique identifier.</param>
    /// <param name="request">The category assignment request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpPut("sounds/{soundId:guid}/category")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignSoundCategory(
        ulong guildId,
        Guid soundId,
        [FromBody] AssignCategoryRequest request,
        CancellationToken cancellationToken)
    {
        if (!await IsGuildAdminAsync())
            return Forbid();

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

        // Validate the category exists in this guild (if not null)
        if (request.CategoryId.HasValue)
        {
            var category = await _categoryRepository.GetByIdAsync(request.CategoryId.Value, cancellationToken) as SoundCategory;
            if (category == null || category.GuildId != guildId)
            {
                return BadRequest(new ApiErrorDto
                {
                    Message = "Category not found",
                    Detail = "The specified category does not exist in this guild.",
                    StatusCode = StatusCodes.Status400BadRequest,
                    TraceId = HttpContext.GetCorrelationId(),
                    ErrorCode = "category_not_found"
                });
            }
        }

        sound.CategoryId = request.CategoryId;
        await _soundRepository.UpdateAsync(sound, cancellationToken);

        _logger.LogInformation("Assigned sound {SoundId} to category {CategoryId} in guild {GuildId}",
            soundId, request.CategoryId, guildId);

        return Ok(new { message = "Category assigned", soundId = soundId.ToString(), categoryId = request.CategoryId });
    }

    /// <summary>
    /// Checks if the current user is a guild admin.
    /// </summary>
    private async Task<bool> IsGuildAdminAsync()
    {
        // SuperAdmin and Admin roles bypass guild-level checks
        if (User.IsInRole("SuperAdmin") || User.IsInRole("Admin"))
            return true;

        var applicationUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(applicationUserId))
            return false;

        // Extract guildId from route
        if (!RouteData.Values.TryGetValue("guildId", out var guildIdObj) ||
            !ulong.TryParse(guildIdObj?.ToString(), out var guildId))
            return false;

        return await _guildMembershipService.IsGuildAdminAsync(applicationUserId, guildId);
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

    /// <summary>
    /// Request model for creating a sound category.
    /// </summary>
    public class CreateCategoryRequest
    {
        /// <summary>
        /// Gets or sets the category name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional sort order.
        /// </summary>
        public int? SortOrder { get; set; }
    }

    /// <summary>
    /// Request model for updating a sound category.
    /// </summary>
    public class UpdateCategoryRequest
    {
        /// <summary>
        /// Gets or sets the category name.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the optional sort order.
        /// </summary>
        public int? SortOrder { get; set; }
    }

    /// <summary>
    /// Request model for assigning a sound to a category.
    /// </summary>
    public class AssignCategoryRequest
    {
        /// <summary>
        /// Gets or sets the category ID. Null to uncategorize the sound.
        /// </summary>
        public int? CategoryId { get; set; }
    }
}
