using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// API controller for notification management operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireViewer")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationsController"/> class.
    /// </summary>
    /// <param name="notificationService">The notification service.</param>
    /// <param name="logger">The logger.</param>
    public NotificationsController(
        INotificationService notificationService,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>
    /// Gets paginated notifications with optional filtering.
    /// </summary>
    /// <param name="type">Optional notification type filter.</param>
    /// <param name="isRead">Optional read status filter (true = read only, false = unread only).</param>
    /// <param name="severity">Optional severity filter.</param>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="searchTerm">Optional search term for title/message.</param>
    /// <param name="guildId">Optional guild ID filter.</param>
    /// <param name="page">Page number (1-based, default: 1).</param>
    /// <param name="pageSize">Page size (default: 25).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paginated list of notifications.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponseDto<UserNotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PaginatedResponseDto<UserNotificationDto>>> GetNotifications(
        [FromQuery] NotificationType? type = null,
        [FromQuery] bool? isRead = null,
        [FromQuery] AlertSeverity? severity = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] ulong? guildId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        _logger.LogDebug("User {UserId} requesting notifications: Type={Type}, IsRead={IsRead}, Page={Page}",
            userId, type, isRead, page);

        var query = new NotificationQueryDto
        {
            Type = type,
            IsRead = isRead,
            Severity = severity,
            StartDate = startDate,
            EndDate = endDate,
            SearchTerm = searchTerm,
            GuildId = guildId,
            Page = page,
            PageSize = pageSize
        };

        var result = await _notificationService.GetUserNotificationsPagedAsync(
            userId, query, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Marks a notification as read.
    /// </summary>
    /// <param name="id">The notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success; 404 if not found or not owned.</returns>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        _logger.LogDebug("User {UserId} marking notification {NotificationId} as read", userId, id);

        await _notificationService.MarkAsReadAsync(userId, id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Marks a notification as unread.
    /// </summary>
    /// <param name="id">The notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success; 404 if not found or not owned.</returns>
    [HttpPost("{id:guid}/unread")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsUnread(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        _logger.LogDebug("User {UserId} marking notification {NotificationId} as unread", userId, id);

        var success = await _notificationService.MarkAsUnreadAsync(userId, id, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    /// <summary>
    /// Marks multiple notifications as read.
    /// </summary>
    /// <param name="ids">The notification IDs to mark as read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("mark-read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkMultipleAsRead(
        [FromBody] IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var idList = ids.ToList();
        _logger.LogDebug("User {UserId} marking {Count} notifications as read", userId, idList.Count);

        await _notificationService.MarkMultipleAsReadAsync(userId, idList, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Marks all notifications as read.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpPost("mark-all-read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        _logger.LogDebug("User {UserId} marking all notifications as read", userId);

        await _notificationService.MarkAllAsReadAsync(userId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Deletes a notification.
    /// </summary>
    /// <param name="id">The notification ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success; 404 if not found or not owned.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        _logger.LogDebug("User {UserId} deleting notification {NotificationId}", userId, id);

        var success = await _notificationService.DeleteAsync(userId, id, cancellationToken);
        return success ? NoContent() : NotFound();
    }

    /// <summary>
    /// Deletes multiple notifications.
    /// </summary>
    /// <param name="ids">The notification IDs to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of notifications deleted.</returns>
    [HttpPost("delete")]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<int>> DeleteMultiple(
        [FromBody] IEnumerable<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var idList = ids.ToList();
        _logger.LogDebug("User {UserId} deleting {Count} notifications", userId, idList.Count);

        var deleted = await _notificationService.DeleteMultipleAsync(userId, idList, cancellationToken);
        return Ok(deleted);
    }
}
