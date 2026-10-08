using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for mod tag operations and management.
/// Provides endpoints for creating, deleting, and managing mod tags and their application to users.
/// </summary>
[ApiController]
[Authorize(Policy = "RequireAdmin")]
[Authorize(Policy = "GuildAccess")]
public class ModTagsController : ControllerBase
{
    private readonly IModTagService _modTagService;
    private readonly ILogger<ModTagsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ModTagsController"/> class.
    /// </summary>
    /// <param name="modTagService">The mod tag service.</param>
    /// <param name="logger">The logger.</param>
    public ModTagsController(
        IModTagService modTagService,
        ILogger<ModTagsController> logger)
    {
        _modTagService = modTagService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all mod tags for a guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of all mod tags for the guild.</returns>
    [HttpGet]
    [Route("api/guilds/{guildId}/tags")]
    [ProducesResponseType(typeof(IEnumerable<ModTagDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ModTagDto>>> GetGuildTags(
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Guild tags requested for guild {GuildId}", guildId);

        var tags = await _modTagService.GetGuildTagsAsync(guildId, cancellationToken);

        _logger.LogTrace("Retrieved {Count} tags for guild {GuildId}", tags.Count(), guildId);

        return Ok(tags);
    }

    /// <summary>
    /// Creates a new mod tag for a guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="request">The tag creation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created mod tag data.</returns>
    [HttpPost]
    [Route("api/guilds/{guildId}/tags")]
    [ProducesResponseType(typeof(ModTagDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ModTagDto>> CreateTag(
        ulong guildId,
        [FromBody] ModTagCreateDto request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mod tag creation requested for guild {GuildId}: {TagName}", guildId, request.Name);

        if (request == null)
        {
            _logger.LogWarning("Invalid mod tag creation request: request body is null");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Request body cannot be null.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            _logger.LogWarning("Invalid mod tag creation request: name is empty");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Tag name is required.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Ensure the GuildId from the route matches the request (override if needed)
        request.GuildId = guildId;

        try
        {
            var tag = await _modTagService.CreateTagAsync(guildId, request, cancellationToken);

            _logger.LogInformation("Mod tag {TagId} ({TagName}) created successfully for guild {GuildId}",
                tag.Id, tag.Name, guildId);

            return CreatedAtAction(
                nameof(GetGuildTags),
                new { guildId },
                tag);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid mod tag creation request for guild {GuildId}", guildId);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }

    /// <summary>
    /// Deletes a mod tag by name.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="tagName">The tag name to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete]
    [Route("api/guilds/{guildId}/tags/{tagName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTag(
        ulong guildId,
        string tagName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mod tag deletion requested for guild {GuildId}: {TagName}", guildId, tagName);

        var success = await _modTagService.DeleteTagAsync(guildId, tagName, cancellationToken);

        if (!success)
        {
            _logger.LogWarning("Mod tag {TagName} not found for deletion in guild {GuildId}", tagName, guildId);

            return NotFound(new ApiErrorDto
            {
                Message = "Mod tag not found",
                Detail = $"No mod tag with name '{tagName}' exists for guild {guildId}.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogInformation("Mod tag {TagName} deleted successfully from guild {GuildId}", tagName, guildId);

        return NoContent();
    }

    /// <summary>
    /// Imports template tags for a guild.
    /// </summary>
    /// <param name="guildId">The guild's Discord snowflake ID.</param>
    /// <param name="templateNames">Array of template names to import.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of tags imported.</returns>
    [HttpPost]
    [Route("api/guilds/{guildId}/tags/import-templates")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<int>> ImportTemplates(
        ulong guildId,
        [FromBody] string[] templateNames,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Template tag import requested for guild {GuildId}: {Count} templates", guildId, templateNames.Length);

        if (templateNames == null || templateNames.Length == 0)
        {
            _logger.LogWarning("Invalid template import request: template names array is empty");

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = "Template names array cannot be empty.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        try
        {
            var count = await _modTagService.ImportTemplateTagsAsync(guildId, templateNames, cancellationToken);

            _logger.LogInformation("{Count} template tags imported successfully for guild {GuildId}", count, guildId);

            return Ok(count);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid template import request for guild {GuildId}", guildId);

            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid request",
                Detail = ex.Message,
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }
    }
}
