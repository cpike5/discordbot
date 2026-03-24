using DiscordBot.Bot.Extensions;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for managing user preferences per guild.
/// Provides CRUD operations for the unified user preferences system.
/// </summary>
[ApiController]
[Route("api/portal/preferences/{guildId:long}")]
[Authorize(Policy = "PortalGuildMember")]
public class UserPreferencesController : ApiControllerBase
{
    private readonly IUserPreferenceRepository _repository;
    private readonly ILogger<UserPreferencesController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserPreferencesController"/> class.
    /// </summary>
    /// <param name="repository">The user preference repository.</param>
    /// <param name="logger">The logger.</param>
    public UserPreferencesController(
        IUserPreferenceRepository repository,
        ILogger<UserPreferencesController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <summary>
    /// Gets all preferences for the current user in the specified guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary of preference key-value pairs.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(ulong guildId, CancellationToken cancellationToken)
    {
        var userId = User.GetDiscordUserId();
        if (userId == 0)
        {
            return Unauthorized();
        }

        _logger.LogDebug(
            "Getting all preferences for user {UserId} in guild {GuildId}",
            userId, guildId);

        var preferences = await _repository.GetAllAsync(userId, guildId, cancellationToken);

        var result = preferences.ToDictionary(p => p.Key, p => p.Value);
        return Ok(result);
    }

    /// <summary>
    /// Gets a single preference by key for the current user in the specified guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="key">The preference key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The preference value, or 404 if not found.</returns>
    [HttpGet("{key}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(ulong guildId, string key, CancellationToken cancellationToken)
    {
        var userId = User.GetDiscordUserId();
        if (userId == 0)
        {
            return Unauthorized();
        }

        _logger.LogDebug(
            "Getting preference {Key} for user {UserId} in guild {GuildId}",
            key, userId, guildId);

        var preference = await _repository.GetAsync(userId, guildId, key, cancellationToken);
        if (preference == null)
        {
            return NotFoundError("Preference not found", $"No preference found with key '{key}'");
        }

        return Ok(new { key = preference.Key, value = preference.Value });
    }

    /// <summary>
    /// Sets (creates or updates) a preference for the current user in the specified guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="key">The preference key.</param>
    /// <param name="request">The preference value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpPut("{key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Set(
        ulong guildId,
        string key,
        [FromBody] SetPreferenceRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetDiscordUserId();
        if (userId == 0)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Value))
        {
            return BadRequestError("Value is required");
        }

        if (key.Length > 100)
        {
            return BadRequestError("Key must not exceed 100 characters");
        }

        if (request.Value.Length > 2000)
        {
            return BadRequestError("Value must not exceed 2000 characters");
        }

        _logger.LogDebug(
            "Setting preference {Key} for user {UserId} in guild {GuildId}",
            key, userId, guildId);

        await _repository.SetAsync(userId, guildId, key, request.Value, cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Deletes a preference for the current user in the specified guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="key">The preference key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>204 No Content on success.</returns>
    [HttpDelete("{key}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Delete(ulong guildId, string key, CancellationToken cancellationToken)
    {
        var userId = User.GetDiscordUserId();
        if (userId == 0)
        {
            return Unauthorized();
        }

        _logger.LogDebug(
            "Deleting preference {Key} for user {UserId} in guild {GuildId}",
            key, userId, guildId);

        await _repository.DeleteAsync(userId, guildId, key, cancellationToken);

        return NoContent();
    }
}

/// <summary>
/// Request body for setting a user preference.
/// </summary>
public class SetPreferenceRequest
{
    /// <summary>
    /// The preference value.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
