using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Bot.Controllers;

/// <summary>
/// Controller for lightweight preview data for user and guild popups.
/// </summary>
[ApiController]
[Route("api/preview")]
[Authorize(Policy = "RequireViewer")]
public class PreviewController : ControllerBase
{
    private readonly DiscordSocketClient _client;
    private readonly BotDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<PreviewController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PreviewController"/> class.
    /// </summary>
    public PreviewController(
        DiscordSocketClient client,
        BotDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<PreviewController> logger)
    {
        _client = client;
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets preview data for a user (without guild context).
    /// </summary>
    /// <param name="userId">The Discord user ID as a string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User preview data.</returns>
    [HttpGet("users/{userId}")]
    [ProducesResponseType(typeof(UserPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserPreviewDto>> GetUserPreview(
        string userId,
        CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(userId, out var userIdParsed))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid user ID format",
                Detail = "User ID must be a valid Discord snowflake ID.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogDebug("User preview requested for user {UserId}", userIdParsed);

        // Try to get user from Discord cache
        var discordUser = _client.GetUser(userIdParsed);
        if (discordUser == null)
        {
            _logger.LogDebug("User {UserId} not found in Discord cache", userIdParsed);
            return NotFound(new ApiErrorDto
            {
                Message = "User not found",
                Detail = $"No user with ID {userIdParsed} found in Discord cache.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var preview = await BuildUserPreviewAsync(discordUser, null, cancellationToken);

        _logger.LogDebug("User preview retrieved for {Username}", discordUser.Username);
        return Ok(preview);
    }

    /// <summary>
    /// Gets preview data for a user with guild context.
    /// </summary>
    /// <param name="userId">The Discord user ID as a string.</param>
    /// <param name="guildId">The guild ID for context as a string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>User preview data with guild-specific information.</returns>
    [HttpGet("users/{userId}/guild/{guildId}")]
    [ProducesResponseType(typeof(UserPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserPreviewDto>> GetUserPreviewWithGuild(
        string userId,
        string guildId,
        CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(userId, out var userIdParsed))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid user ID format",
                Detail = "User ID must be a valid Discord snowflake ID.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        if (!ulong.TryParse(guildId, out var guildIdParsed))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid guild ID format",
                Detail = "Guild ID must be a valid Discord snowflake ID.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogDebug("User preview requested for user {UserId} in guild {GuildId}", userIdParsed, guildIdParsed);

        // Try to get guild from Discord cache
        var discordGuild = _client.GetGuild(guildIdParsed);
        if (discordGuild == null)
        {
            _logger.LogDebug("Guild {GuildId} not found in Discord cache", guildIdParsed);
            return NotFound(new ApiErrorDto
            {
                Message = "Guild not found",
                Detail = $"No guild with ID {guildIdParsed} found in Discord cache.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        // Try to get user as guild member
        var guildUser = discordGuild.GetUser(userIdParsed);
        if (guildUser == null)
        {
            // Fall back to regular user without guild context
            var regularUser = _client.GetUser(userIdParsed);
            if (regularUser == null)
            {
                _logger.LogDebug("User {UserId} not found in Discord cache", userIdParsed);
                return NotFound(new ApiErrorDto
                {
                    Message = "User not found",
                    Detail = $"No user with ID {userIdParsed} found in Discord cache.",
                    StatusCode = StatusCodes.Status404NotFound,
                    TraceId = HttpContext.GetCorrelationId()
                });
            }

            var preview = await BuildUserPreviewAsync(regularUser, guildIdParsed, cancellationToken);
            return Ok(preview);
        }

        var previewWithGuild = await BuildGuildUserPreviewAsync(guildUser, guildIdParsed, cancellationToken);

        _logger.LogDebug("User preview with guild context retrieved for {Username} in {GuildName}",
            guildUser.Username, discordGuild.Name);
        return Ok(previewWithGuild);
    }

    /// <summary>
    /// Gets preview data for a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild ID as a string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Guild preview data.</returns>
    [HttpGet("guilds/{guildId}")]
    [ProducesResponseType(typeof(GuildPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuildPreviewDto>> GetGuildPreview(
        string guildId,
        CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(guildId, out var guildIdParsed))
        {
            return BadRequest(new ApiErrorDto
            {
                Message = "Invalid guild ID format",
                Detail = "Guild ID must be a valid Discord snowflake ID.",
                StatusCode = StatusCodes.Status400BadRequest,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        _logger.LogDebug("Guild preview requested for guild {GuildId}", guildIdParsed);

        // Try to get guild from Discord cache first
        var discordGuild = _client.GetGuild(guildIdParsed);
        if (discordGuild == null)
        {
            _logger.LogDebug("Guild {GuildId} not found in Discord cache", guildIdParsed);
            return NotFound(new ApiErrorDto
            {
                Message = "Guild not found",
                Detail = $"No guild with ID {guildIdParsed} found in Discord cache.",
                StatusCode = StatusCodes.Status404NotFound,
                TraceId = HttpContext.GetCorrelationId()
            });
        }

        var preview = await BuildGuildPreviewAsync(discordGuild, cancellationToken);

        _logger.LogDebug("Guild preview retrieved for {GuildName}", discordGuild.Name);
        return Ok(preview);
    }

    /// <summary>
    /// Builds a user preview DTO from a Discord user.
    /// </summary>
    private async Task<UserPreviewDto> BuildUserPreviewAsync(
        SocketUser discordUser,
        ulong? guildId,
        CancellationToken cancellationToken)
    {
        // Get last activity from command logs
        var lastActive = await _dbContext.CommandLogs
            .Where(c => c.UserId == discordUser.Id)
            .OrderByDescending(c => c.ExecutedAt)
            .Select(c => (DateTime?)c.ExecutedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Check if user is verified (has a linked application user account)
        var isVerified = await _userManager.Users
            .AnyAsync(u => u.DiscordUserId == discordUser.Id, cancellationToken);

        // Check for active moderation cases
        var hasActiveModeration = guildId.HasValue && await HasActiveModerationAsync(guildId.Value, discordUser.Id, cancellationToken);

        return new UserPreviewDto
        {
            UserId = discordUser.Id,
            Username = discordUser.Username,
            DisplayName = discordUser.GlobalName,
            AvatarUrl = discordUser.GetAvatarUrl() ?? discordUser.GetDefaultAvatarUrl(),
            LastActive = lastActive,
            IsVerified = isVerified,
            HasActiveModeration = hasActiveModeration,
            Roles = [],
            MemberSince = null
        };
    }

    /// <summary>
    /// Builds a user preview DTO from a guild member.
    /// </summary>
    private async Task<UserPreviewDto> BuildGuildUserPreviewAsync(
        SocketGuildUser guildUser,
        ulong guildId,
        CancellationToken cancellationToken)
    {
        // Get last activity from command logs
        var lastActive = await _dbContext.CommandLogs
            .Where(c => c.UserId == guildUser.Id)
            .OrderByDescending(c => c.ExecutedAt)
            .Select(c => (DateTime?)c.ExecutedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Check if user is verified (has a linked application user account)
        var isVerified = await _userManager.Users
            .AnyAsync(u => u.DiscordUserId == guildUser.Id, cancellationToken);

        // Check for active moderation cases
        var hasActiveModeration = await HasActiveModerationAsync(guildId, guildUser.Id, cancellationToken);

        // Get top roles (excluding @everyone, limit to 5)
        var roles = guildUser.Roles
            .Where(r => !r.IsEveryone)
            .OrderByDescending(r => r.Position)
            .Take(5)
            .Select(r => r.Name)
            .ToList();

        return new UserPreviewDto
        {
            UserId = guildUser.Id,
            Username = guildUser.Username,
            DisplayName = guildUser.DisplayName,
            AvatarUrl = guildUser.GetGuildAvatarUrl() ?? guildUser.GetAvatarUrl() ?? guildUser.GetDefaultAvatarUrl(),
            MemberSince = guildUser.JoinedAt?.UtcDateTime,
            Roles = roles,
            LastActive = lastActive,
            IsVerified = isVerified,
            HasActiveModeration = hasActiveModeration
        };
    }

    /// <summary>
    /// Builds a guild preview DTO from a Discord guild.
    /// </summary>
    private async Task<GuildPreviewDto> BuildGuildPreviewAsync(
        SocketGuild discordGuild,
        CancellationToken cancellationToken)
    {
        // Get database guild for bot joined date and settings
        var dbGuild = await _dbContext.Guilds
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == discordGuild.Id, cancellationToken);

        // Get owner username
        var owner = discordGuild.Owner;
        var ownerUsername = owner?.Username ?? "Unknown";

        // Determine active features from guild settings
        var activeFeatures = new List<string>();
        if (dbGuild?.Settings != null)
        {
            var settings = GuildSettingsViewModel.Parse(dbGuild.Settings);
            if (settings.WelcomeMessagesEnabled) activeFeatures.Add("Welcome");
            if (settings.AutoModEnabled) activeFeatures.Add("AutoMod");
            if (settings.ModerationAlertsEnabled) activeFeatures.Add("Moderation");
            if (settings.CommandLoggingEnabled) activeFeatures.Add("Logging");
        }

        // Check for additional features in database tables
        var hasRatWatch = await _dbContext.GuildRatWatchSettings
            .AnyAsync(r => r.GuildId == discordGuild.Id && r.IsEnabled, cancellationToken);
        if (hasRatWatch) activeFeatures.Add("RatWatch");

        var hasScheduledMessages = await _dbContext.ScheduledMessages
            .AnyAsync(s => s.GuildId == discordGuild.Id && s.IsEnabled, cancellationToken);
        if (hasScheduledMessages) activeFeatures.Add("Scheduled Messages");

        return new GuildPreviewDto
        {
            GuildId = discordGuild.Id,
            Name = discordGuild.Name,
            IconUrl = discordGuild.IconUrl,
            MemberCount = discordGuild.MemberCount,
            OnlineMemberCount = discordGuild.Users.Count(u => u.Status != Discord.UserStatus.Offline),
            OwnerUsername = ownerUsername,
            BotJoinedAt = dbGuild?.JoinedAt ?? discordGuild.CurrentUser?.JoinedAt?.UtcDateTime ?? DateTime.UtcNow,
            ActiveFeatures = activeFeatures,
            IsActive = dbGuild?.IsActive ?? true
        };
    }

    /// <summary>
    /// Checks if a user has any active (non-expired) moderation cases.
    /// </summary>
    private async Task<bool> HasActiveModerationAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken)
    {
        // Check for active moderation cases (mutes, temp bans that haven't expired)
        // A case is active if it has no expiry (permanent) or hasn't expired yet
        return await _dbContext.ModerationCases
            .AnyAsync(c =>
                c.GuildId == guildId &&
                c.TargetUserId == userId &&
                (c.ExpiresAt == null || c.ExpiresAt > DateTime.UtcNow),
                cancellationToken);
    }
}
