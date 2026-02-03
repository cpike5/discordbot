using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for audio operations including voice channel connection and playback control.
/// </summary>
[ApiController]
[Route("api/guilds/{guildId}/audio")]
[Authorize(Policy = "RequireViewer")]
public class AudioController : ControllerBase
{
    private readonly IAudioService _audioService;
    private readonly IPlaybackService _playbackService;
    private readonly IGuildAudioSettingsService _audioSettingsService;
    private readonly ILogger<AudioController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AudioController"/> class.
    /// </summary>
    /// <param name="audioService">The audio service for voice connections.</param>
    /// <param name="playbackService">The playback service for audio control.</param>
    /// <param name="audioSettingsService">The audio settings service.</param>
    /// <param name="logger">The logger.</param>
    public AudioController(
        IAudioService audioService,
        IPlaybackService playbackService,
        IGuildAudioSettingsService audioSettingsService,
        ILogger<AudioController> logger)
    {
        _audioService = audioService;
        _playbackService = playbackService;
        _audioSettingsService = audioSettingsService;
        _logger = logger;
    }

    /// <summary>
    /// Joins a voice channel in the specified guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="channelId">The voice channel's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpPost("join/{channelId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> JoinChannel(
        ulong guildId,
        ulong channelId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Join channel request for guild {GuildId}, channel {ChannelId}", guildId, channelId);

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

        var audioClient = await _audioService.JoinChannelAsync(guildId, channelId, cancellationToken);
        if (audioClient == null)
        {
            _logger.LogWarning("Failed to join channel {ChannelId} in guild {GuildId}", channelId, guildId);
            return NotFound(new ApiErrorDto
            {
                Message = "Failed to join voice channel",
                Detail = "The guild or voice channel was not found, or the bot lacks permission to join.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Successfully joined channel {ChannelId} in guild {GuildId}", channelId, guildId);
        return Ok(new { Message = "Joined voice channel", GuildId = guildId.ToString(), ChannelId = channelId.ToString() });
    }

    /// <summary>
    /// Leaves the voice channel in the specified guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpPost("leave")]
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
        return Ok(new { Message = "Left voice channel", GuildId = guildId.ToString() });
    }

    /// <summary>
    /// Stops the current playback and clears the queue in the specified guild.
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
            _logger.LogDebug("Not connected to voice in guild {GuildId}, nothing to stop", guildId);
            return BadRequest(new ApiErrorDto
            {
                Message = "Not connected to voice",
                Detail = "The bot is not currently connected to a voice channel in this guild.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        await _playbackService.StopAsync(guildId, cancellationToken);

        _logger.LogInformation("Successfully stopped playback in guild {GuildId}", guildId);
        return Ok(new { Message = "Playback stopped", GuildId = guildId.ToString() });
    }

    /// <summary>
    /// Removes an item from the playback queue at the specified position.
    /// Position 0 skips the currently playing sound.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="position">Zero-based queue position to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpDelete("queue/{position}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveFromQueue(
        ulong guildId,
        int position,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Remove from queue request for guild {GuildId}, position {Position}", guildId, position);

        if (position < 0)
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid queue position",
                Detail = "Queue position must be a non-negative integer.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

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

        var success = await _playbackService.RemoveFromQueueAsync(guildId, position, cancellationToken);
        if (!success)
        {
            _logger.LogWarning("Failed to remove item at position {Position} from queue in guild {GuildId}", position, guildId);
            return NotFound(new ApiErrorDto
            {
                Message = "Queue position not found",
                Detail = $"No item exists at position {position} in the queue.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Successfully removed item at position {Position} from queue in guild {GuildId}", position, guildId);
        return Ok(new { Message = position == 0 ? "Skipped current sound" : "Removed from queue", GuildId = guildId.ToString(), Position = position });
    }
}
