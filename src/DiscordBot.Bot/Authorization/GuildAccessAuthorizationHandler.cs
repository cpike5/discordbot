using System.Security.Claims;
using DiscordBot.Core.Authorization;
using DiscordBot.Core.Entities;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Bot.Authorization;

/// <summary>
/// Handles authorization for guild-specific access by verifying user permissions.
/// Authorization logic order:
/// 1. SuperAdmins bypass all checks (access to all guilds)
/// 2. Admins bypass guild membership checks (access to all guilds)
/// 3. Discord guild membership check via UserDiscordGuild table
/// 4. Fallback to explicit grants via UserGuildAccess table
/// </summary>
public class GuildAccessAuthorizationHandler : AuthorizationHandler<GuildAccessRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GuildAccessAuthorizationHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuildAccessAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">Accessor to retrieve the current HTTP context.</param>
    /// <param name="scopeFactory">Factory to create service scopes for database access.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public GuildAccessAuthorizationHandler(
        IHttpContextAccessor httpContextAccessor,
        IServiceScopeFactory scopeFactory,
        ILogger<GuildAccessAuthorizationHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Handles the authorization requirement by checking if the user has sufficient access to the specified guild.
    /// </summary>
    /// <param name="context">The authorization context containing user information.</param>
    /// <param name="requirement">The requirement specifying the minimum access level needed.</param>
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GuildAccessRequirement requirement)
    {
        // SuperAdmins have access to all guilds
        if (context.User.IsInRole(Roles.SuperAdmin))
        {
            _logger.LogDebug("SuperAdmin user granted access to all guilds");
            context.Succeed(requirement);
            return;
        }

        // Admins have access to all guilds
        if (context.User.IsInRole(Roles.Admin))
        {
            _logger.LogDebug("Admin user granted access to all guilds");
            context.Succeed(requirement);
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogWarning("GuildAccess check failed: No HTTP context available");
            return;
        }

        // Extract guild ID from route or query string
        var guildIdString = httpContext.Request.RouteValues["guildId"]?.ToString()
            ?? httpContext.Request.Query["guildId"].FirstOrDefault();

        if (string.IsNullOrEmpty(guildIdString) || !ulong.TryParse(guildIdString, out var guildId))
        {
            _logger.LogWarning("GuildAccess check failed: No valid guildId found in route or query string");
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("GuildAccess check failed: No user ID found in claims");
            return;
        }

        // Check user's guild access using a scoped DbContext
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BotDbContext>();

        // Check if user is a Discord member of the guild
        var isDiscordMember = await dbContext.Set<UserDiscordGuild>()
            .AnyAsync(u =>
                u.ApplicationUserId == userId &&
                u.GuildId == guildId);

        if (isDiscordMember)
        {
            _logger.LogDebug(
                "User granted access via Discord guild membership for guild {GuildId}",
                guildId);
            context.Succeed(requirement);
            return;
        }

        // Fallback: Check explicit grants in UserGuildAccess table
        var access = await dbContext.Set<UserGuildAccess>()
            .FirstOrDefaultAsync(a =>
                a.ApplicationUserId == userId &&
                a.GuildId == guildId);

        if (access != null && access.AccessLevel >= requirement.MinimumLevel)
        {
            _logger.LogDebug(
                "GuildAccess granted for user {UserId} to guild {GuildId} with level {AccessLevel} (required: {RequiredLevel})",
                userId, guildId, access.AccessLevel, requirement.MinimumLevel);
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogDebug(
                "GuildAccess denied for user {UserId} to guild {GuildId} (user level: {UserLevel}, required: {RequiredLevel})",
                userId, guildId, access?.AccessLevel.ToString() ?? "None", requirement.MinimumLevel);
        }
    }
}
