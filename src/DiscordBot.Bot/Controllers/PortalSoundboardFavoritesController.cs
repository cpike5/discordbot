using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for member portal soundboard favorites: listing, adding, and
/// removing a user's favorited sounds.
/// </summary>
[ApiController]
[Route("api/portal/soundboard/{guildId}")]
[Authorize(Policy = "PortalGuildMember")]
public class PortalSoundboardFavoritesController : PortalSoundboardControllerBase
{
    private readonly IUserSoundFavoriteRepository _favoriteRepository;
    private readonly ISoundService _soundService;
    private readonly ILogger<PortalSoundboardFavoritesController> _logger;

    public PortalSoundboardFavoritesController(
        IUserSoundFavoriteRepository favoriteRepository,
        ISoundService soundService,
        ISettingsService settingsService,
        ILogger<PortalSoundboardFavoritesController> logger)
        : base(settingsService)
    {
        _favoriteRepository = favoriteRepository;
        _soundService = soundService;
        _logger = logger;
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
}
