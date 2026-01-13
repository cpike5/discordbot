using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
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
    private readonly IAudioService _audioService;
    private readonly IPlaybackService _playbackService;
    private readonly IGuildAudioSettingsService _audioSettingsService;
    private readonly IAudioNotifier _audioNotifier;
    private readonly DiscordSocketClient _discordClient;
    private readonly ILogger<PortalSoundboardController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PortalSoundboardController"/> class.
    /// </summary>
    /// <param name="soundService">The sound service for metadata operations.</param>
    /// <param name="soundFileService">The sound file service for file operations.</param>
    /// <param name="audioService">The audio service for voice connections.</param>
    /// <param name="playbackService">The playback service for audio control.</param>
    /// <param name="audioSettingsService">The audio settings service.</param>
    /// <param name="audioNotifier">The audio notifier for real-time updates.</param>
    /// <param name="discordClient">The Discord socket client.</param>
    /// <param name="logger">The logger.</param>
    public PortalSoundboardController(
        ISoundService soundService,
        ISoundFileService soundFileService,
        IAudioService audioService,
        IPlaybackService playbackService,
        IGuildAudioSettingsService audioSettingsService,
        IAudioNotifier audioNotifier,
        DiscordSocketClient discordClient,
        ILogger<PortalSoundboardController> logger)
    {
        _soundService = soundService;
        _soundFileService = soundFileService;
        _audioService = audioService;
        _playbackService = playbackService;
        _audioSettingsService = audioSettingsService;
        _audioNotifier = audioNotifier;
        _discordClient = discordClient;
        _logger = logger;
    }

    /// <summary>
    /// Gets all sounds for the specified guild with play counts.
    /// Sounds are returned in alphabetical order by name.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of sounds with play counts.</returns>
    [HttpGet("sounds")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSounds(ulong guildId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Get sounds request for guild {GuildId}", guildId);

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
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var sounds = await _soundService.GetAllByGuildAsync(guildId, cancellationToken);

        var response = new
        {
            sounds = sounds.Select(s => new
            {
                id = s.Id.ToString(),
                name = s.Name,
                playCount = s.PlayCount
            }).ToList(),
            totalCount = sounds.Count
        };

        _logger.LogInformation("Returning {Count} sounds for guild {GuildId}", sounds.Count, guildId);
        return Ok(response);
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
        _logger.LogInformation("Upload sound request for guild {GuildId}, name {SoundName}", guildId, name);

        // Check if audio is enabled for this guild
        var audioSettings = await _audioSettingsService.GetSettingsAsync(guildId, cancellationToken);
        if (audioSettings == null || !audioSettings.AudioEnabled)
        {
            _logger.LogWarning("Audio not enabled for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Audio is not enabled for this guild",
                Detail = "Enable audio in the guild settings before uploading sounds.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Validate file is provided
        if (file == null || file.Length == 0)
        {
            _logger.LogWarning("No file provided for sound upload in guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "No file provided",
                Detail = "Please select an audio file to upload.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
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
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Validate audio format
        if (!_soundFileService.IsValidAudioFormat(file.FileName))
        {
            _logger.LogWarning("Invalid audio format {FileName} for guild {GuildId}", file.FileName, guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid audio format",
                Detail = "Supported formats: .mp3, .wav, .ogg, .m4a",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Check sound count limit
        if (!await _soundService.ValidateSoundCountLimitAsync(guildId, cancellationToken))
        {
            var currentCount = await _soundService.GetSoundCountAsync(guildId, cancellationToken);
            _logger.LogWarning("Sound count limit reached for guild {GuildId} (current: {CurrentCount}, max: {MaxSounds})",
                guildId, currentCount, audioSettings.MaxSoundsPerGuild);
            return BadRequest(new ApiErrorDto
            {
                Message = "Sound limit reached",
                Detail = $"This guild has reached the maximum number of sounds ({audioSettings.MaxSoundsPerGuild}). Please delete some sounds before adding new ones.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Check storage limit
        if (!await _soundService.ValidateStorageLimitAsync(guildId, file.Length, cancellationToken))
        {
            var currentStorage = await _soundService.GetStorageUsedAsync(guildId, cancellationToken);
            var maxStorageMB = audioSettings.MaxStorageBytes / (1024 * 1024);
            var currentStorageMB = currentStorage / (1024.0 * 1024.0);
            _logger.LogWarning("Storage limit would be exceeded for guild {GuildId} (current: {CurrentMB:F2} MB, file: {FileMB:F2} MB, max: {MaxMB} MB)",
                guildId, currentStorageMB, file.Length / (1024.0 * 1024.0), maxStorageMB);
            return BadRequest(new ApiErrorDto
            {
                Message = "Storage limit exceeded",
                Detail = $"Adding this file would exceed the storage limit of {maxStorageMB} MB. Current usage: {currentStorageMB:F2} MB.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Check for duplicate name
        var existingSound = await _soundService.GetByNameAsync(name, guildId, cancellationToken);
        if (existingSound != null)
        {
            _logger.LogWarning("Duplicate sound name {SoundName} for guild {GuildId}", name, guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Duplicate sound name",
                Detail = $"A sound with the name '{name}' already exists in this guild.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Generate unique filename with extension
        var extension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid()}{extension}";

        // Save file to disk
        await _soundFileService.EnsureGuildDirectoryExistsAsync(guildId, cancellationToken);
        using (var stream = file.OpenReadStream())
        {
            await _soundFileService.SaveSoundFileAsync(guildId, fileName, stream, cancellationToken);
        }

        // Get audio duration
        var filePath = _soundFileService.GetSoundFilePath(guildId, fileName);
        var duration = await _soundFileService.GetAudioDurationAsync(filePath, cancellationToken);

        // Create sound entity
        var sound = new Sound
        {
            GuildId = guildId,
            Name = name,
            FileName = fileName,
            FileSizeBytes = file.Length,
            DurationSeconds = duration,
            UploadedAt = DateTime.UtcNow
        };

        var createdSound = await _soundService.CreateSoundAsync(sound, cancellationToken);

        _logger.LogInformation("Successfully uploaded sound {SoundName} ({SoundId}) for guild {GuildId}",
            createdSound.Name, createdSound.Id, guildId);

        // Broadcast to other portal viewers via SignalR
        await _audioNotifier.NotifySoundUploadedAsync(
            guildId,
            createdSound.Id,
            createdSound.Name,
            createdSound.PlayCount,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetSounds),
            new { guildId },
            new
            {
                id = createdSound.Id.ToString(),
                name = createdSound.Name,
                playCount = createdSound.PlayCount
            });
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
        _logger.LogInformation("Play sound request for sound {SoundId} in guild {GuildId}", soundId, guildId);

        // Check if audio is enabled for this guild
        var audioSettings = await _audioSettingsService.GetSettingsAsync(guildId, cancellationToken);
        if (audioSettings == null || !audioSettings.AudioEnabled)
        {
            _logger.LogWarning("Audio not enabled for guild {GuildId}", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Audio is not enabled for this guild",
                Detail = "Enable audio in the guild settings before playing sounds.",
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
                Detail = "The bot must be connected to a voice channel before playing sounds.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Get sound metadata
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

        // Verify file exists on disk
        if (!_soundFileService.SoundFileExists(guildId, sound.FileName))
        {
            _logger.LogError("Sound file missing for sound {SoundId} in guild {GuildId}", soundId, guildId);
            return NotFound(new ApiErrorDto
            {
                Message = "Sound file not found",
                Detail = "The sound exists in the database but the file is missing from storage.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Play the sound (queueEnabled: false for immediate playback)
        try
        {
            await _playbackService.PlayAsync(guildId, sound, queueEnabled: false, cancellationToken: cancellationToken);
            _logger.LogInformation("Successfully started playback of sound {SoundName} ({SoundId}) in guild {GuildId}",
                sound.Name, sound.Id, guildId);
            return Ok(new { Message = "Playing sound", SoundName = sound.Name, SoundId = soundId });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to play sound {SoundId} in guild {GuildId}", soundId, guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Failed to play sound",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
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
                TraceId = HttpContext.GetCorrelationId()
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
