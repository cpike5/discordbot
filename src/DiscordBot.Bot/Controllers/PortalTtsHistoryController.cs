using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.DTOs.Portal;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for member portal TTS message history: listing, saving, replaying,
/// favoriting, and deleting a user's past TTS messages for a guild.
/// </summary>
[ApiController]
[Route("api/portal/tts/{guildId}")]
[Authorize(Policy = "PortalGuildMember")]
public class PortalTtsHistoryController : PortalTtsControllerBase
{
    private readonly ITtsMessageHistoryRepository _ttsMessageHistoryRepository;
    private readonly ILogger<PortalTtsHistoryController> _logger;

    public PortalTtsHistoryController(
        ITtsSendPipeline sendPipeline,
        ITtsMessageHistoryRepository ttsMessageHistoryRepository,
        ILogger<PortalTtsHistoryController> logger)
        : base(sendPipeline)
    {
        _ttsMessageHistoryRepository = ttsMessageHistoryRepository;
        _logger = logger;
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
    /// Saves a new TTS history entry after a successful send.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The history entry data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created history entry.</returns>
    [HttpPost("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveHistory(ulong guildId, [FromBody] SaveTtsHistoryRequest request, CancellationToken cancellationToken = default)
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

        var entry = new TtsMessageHistory
        {
            GuildId = guildId,
            UserId = userId,
            Message = request.Message,
            VoiceName = request.VoiceName ?? string.Empty,
            Style = request.Style,
            Speed = (decimal)request.Speed,
            Pitch = (decimal)request.Pitch,
            IsFavorite = false,
            PlayedAt = DateTime.UtcNow
        };

        await _ttsMessageHistoryRepository.AddAsync(entry, cancellationToken);

        _logger.LogInformation("Saved TTS history entry {Id} for user {UserId} in guild {GuildId}", entry.Id, userId, guildId);

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
    /// Replays a TTS history entry with its original settings.
    /// Reuses the existing send pipeline but with settings from the history entry.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="id">The history entry ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success status.</returns>
    [HttpPost("history/{id}/replay")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReplayHistory(ulong guildId, int id, CancellationToken cancellationToken = default)
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

        // Build a SendTtsRequest from the history entry's settings
        var request = new SendTtsRequest
        {
            Message = entry.Message,
            Voice = entry.VoiceName,
            Speed = (double)entry.Speed,
            Pitch = (double)entry.Pitch,
            Style = entry.Style
        };

        // Delegate to the existing send endpoint logic
        return await _sendPipeline.SendTtsCoreAsync(HttpContext, guildId, request, cancellationToken);
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
    public async Task<IActionResult> ToggleFavorite(ulong guildId, int id, CancellationToken cancellationToken = default)
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
    public async Task<IActionResult> DeleteHistoryEntry(ulong guildId, int id, CancellationToken cancellationToken = default)
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
}
