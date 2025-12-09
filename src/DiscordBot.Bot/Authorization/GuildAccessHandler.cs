using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace DiscordBot.Bot.Authorization;

/// <summary>
/// Handles guild-specific authorization by verifying user membership/ownership.
/// </summary>
public class GuildAccessHandler : AuthorizationHandler<GuildAccessRequirement>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly DiscordSocketClient _discordClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GuildAccessHandler> _logger;

    public GuildAccessHandler(
        UserManager<ApplicationUser> userManager,
        DiscordSocketClient discordClient,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GuildAccessHandler> logger)
    {
        _userManager = userManager;
        _discordClient = discordClient;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GuildAccessRequirement requirement)
    {
        // SuperAdmins bypass guild-specific checks
        if (context.User.IsInRole(IdentitySeeder.Roles.SuperAdmin))
        {
            _logger.LogDebug("SuperAdmin user granted guild access");
            context.Succeed(requirement);
            return;
        }

        // Get the guild ID from route data
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogWarning("HttpContext is null, cannot verify guild access");
            return;
        }

        var guildIdString = httpContext.Request.RouteValues[requirement.GuildIdParameterName]?.ToString()
            ?? httpContext.Request.Query[requirement.GuildIdParameterName].FirstOrDefault();

        if (string.IsNullOrEmpty(guildIdString) || !ulong.TryParse(guildIdString, out var guildId))
        {
            _logger.LogDebug("Guild ID not found in route, skipping guild access check");
            return;
        }

        // Get the current user
        var user = await _userManager.GetUserAsync(context.User);
        if (user == null)
        {
            _logger.LogWarning("User not found in database");
            return;
        }

        // User must have Discord linked
        if (!user.DiscordUserId.HasValue)
        {
            _logger.LogDebug("User {UserId} does not have Discord linked, denying guild access", user.Id);
            return;
        }

        // Check if user is a member/admin of the guild via Discord
        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found in Discord client", guildId);
            return;
        }

        var guildUser = guild.GetUser(user.DiscordUserId.Value);
        if (guildUser == null)
        {
            _logger.LogDebug("User {DiscordUserId} is not a member of guild {GuildId}",
                user.DiscordUserId.Value, guildId);
            return;
        }

        // User is a member of the guild - check role-based permissions
        // Admins need Administrator permission in the guild
        if (context.User.IsInRole(IdentitySeeder.Roles.Admin))
        {
            if (guildUser.GuildPermissions.Administrator)
            {
                _logger.LogDebug("User {DiscordUserId} has Administrator permission in guild {GuildId}",
                    user.DiscordUserId.Value, guildId);
                context.Succeed(requirement);
                return;
            }
            _logger.LogDebug("Admin user {DiscordUserId} lacks Administrator permission in guild {GuildId}",
                user.DiscordUserId.Value, guildId);
            return;
        }

        // Moderators and Viewers just need to be guild members
        _logger.LogDebug("User {DiscordUserId} is a member of guild {GuildId}, granting access",
            user.DiscordUserId.Value, guildId);
        context.Succeed(requirement);
    }
}
