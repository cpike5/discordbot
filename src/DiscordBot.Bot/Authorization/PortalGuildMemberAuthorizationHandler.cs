using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace DiscordBot.Bot.Authorization;

/// <summary>
/// Handles authorization for portal pages by verifying Discord OAuth authentication
/// and guild membership. This is a lighter-weight check than admin authorization
/// - it only requires being a member of the guild, no role checks.
/// </summary>
public class PortalGuildMemberAuthorizationHandler : AuthorizationHandler<PortalGuildMemberRequirement>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly DiscordSocketClient _discordClient;
    private readonly IGuildAudioSettingsRepository _audioSettingsRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PortalGuildMemberAuthorizationHandler> _logger;

    public PortalGuildMemberAuthorizationHandler(
        UserManager<ApplicationUser> userManager,
        DiscordSocketClient discordClient,
        IGuildAudioSettingsRepository audioSettingsRepository,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PortalGuildMemberAuthorizationHandler> logger)
    {
        _userManager = userManager;
        _discordClient = discordClient;
        _audioSettingsRepository = audioSettingsRepository;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PortalGuildMemberRequirement requirement)
    {
        // SuperAdmins and Admins bypass portal guild membership checks
        // They need access to manage any guild's portal
        if (context.User.IsInRole(IdentitySeeder.Roles.SuperAdmin) ||
            context.User.IsInRole(IdentitySeeder.Roles.Admin))
        {
            _logger.LogDebug("PortalGuildMember: Admin user granted portal access");
            context.Succeed(requirement);
            return;
        }

        ulong guildId;
        if (context.Resource is ulong resourceGuildId)
        {
            guildId = resourceGuildId;
        }
        else
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                _logger.LogWarning("PortalGuildMember: HttpContext is null and no resource guild ID");
                return;
            }

            var guildIdString = httpContext.Request.RouteValues[requirement.GuildIdParameterName]?.ToString()
                ?? httpContext.Request.Query[requirement.GuildIdParameterName].FirstOrDefault();

            if (string.IsNullOrEmpty(guildIdString) || !ulong.TryParse(guildIdString, out guildId))
            {
                _logger.LogDebug("PortalGuildMember: No valid guildId found in route or query");
                return;
            }
        }

        // Check if user is authenticated
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogDebug("PortalGuildMember: User not authenticated, will redirect to login");
            // Let this fail - the auth middleware will redirect to login
            return;
        }

        // Check if user has Discord linked (required for portal access)
        var user = await _userManager.GetUserAsync(context.User);
        if (user == null)
        {
            _logger.LogDebug("PortalGuildMember: User not found in database");
            SetForbiddenResult(_httpContextAccessor.HttpContext);
            return;
        }

        if (!user.DiscordUserId.HasValue)
        {
            _logger.LogDebug("PortalGuildMember: User {UserId} does not have Discord linked", user.Id);
            SetForbiddenResult(_httpContextAccessor.HttpContext);
            return;
        }

        // Check if portal is enabled for this guild
        var audioSettings = await _audioSettingsRepository.GetByGuildIdAsync(guildId);

        // TODO: Issue #947 will add EnableMemberPortal property
        // For now, we check AudioEnabled as a proxy
        // Once #947 is merged, change this to: !audioSettings.EnableMemberPortal
        if (audioSettings == null || !audioSettings.AudioEnabled)
        {
            _logger.LogDebug("PortalGuildMember: Portal not enabled for guild {GuildId}", guildId);
            SetNotFoundResult(_httpContextAccessor.HttpContext);
            return;
        }

        // Check if user is a member of the guild via Discord API
        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogWarning("PortalGuildMember: Guild {GuildId} not found in Discord client", guildId);
            SetNotFoundResult(_httpContextAccessor.HttpContext);
            return;
        }

        var guildUser = guild.GetUser(user.DiscordUserId.Value);
        if (guildUser == null)
        {
            // Cache miss - try REST API (AlwaysDownloadUsers is false, so cache may be incomplete)
            try
            {
                var restUser = await _discordClient.Rest.GetGuildUserAsync(guildId, user.DiscordUserId.Value);
                if (restUser == null)
                {
                    _logger.LogDebug(
                        "PortalGuildMember: User {DiscordUserId} is not a member of guild {GuildId}",
                        user.DiscordUserId.Value, guildId);
                    SetForbiddenResult(_httpContextAccessor.HttpContext);
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "PortalGuildMember: Failed to verify guild membership via REST for user {DiscordUserId} in guild {GuildId}",
                    user.DiscordUserId.Value, guildId);
                SetForbiddenResult(_httpContextAccessor.HttpContext);
                return;
            }
        }

        _logger.LogDebug(
            "PortalGuildMember: User {DiscordUserId} granted access to guild {GuildId} portal",
            user.DiscordUserId.Value, guildId);
        context.Succeed(requirement);
    }

    /// <summary>
    /// Sets an item in HttpContext to signal a 403 Forbidden response.
    /// No-op when HttpContext is null (Blazor circuit path).
    /// </summary>
    private static void SetForbiddenResult(HttpContext? httpContext)
    {
        if (httpContext != null)
            httpContext.Items["AuthorizationFailureReason"] = "Forbidden";
    }

    /// <summary>
    /// Sets an item in HttpContext to signal a 404 Not Found response.
    /// No-op when HttpContext is null (Blazor circuit path).
    /// </summary>
    private static void SetNotFoundResult(HttpContext? httpContext)
    {
        if (httpContext != null)
            httpContext.Items["AuthorizationFailureReason"] = "NotFound";
    }
}
