using DiscordBot.Bot.Extensions;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for member portal soundboard sound categories: listing, creating,
/// updating, deleting categories, and assigning a sound to a category.
/// </summary>
[ApiController]
[Route("api/portal/soundboard/{guildId}")]
[Authorize(Policy = "PortalGuildMember")]
public class PortalSoundboardCategoriesController : PortalSoundboardControllerBase
{
    private readonly ISoundCategoryRepository _categoryRepository;
    private readonly ISoundRepository _soundRepository;
    private readonly ISoundService _soundService;
    private readonly IGuildMembershipService _guildMembershipService;
    private readonly ILogger<PortalSoundboardCategoriesController> _logger;

    public PortalSoundboardCategoriesController(
        ISoundCategoryRepository categoryRepository,
        ISoundRepository soundRepository,
        ISoundService soundService,
        IGuildMembershipService guildMembershipService,
        ISettingsService settingsService,
        ILogger<PortalSoundboardCategoriesController> logger)
        : base(settingsService)
    {
        _categoryRepository = categoryRepository;
        _soundRepository = soundRepository;
        _soundService = soundService;
        _guildMembershipService = guildMembershipService;
        _logger = logger;
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
