using DiscordBot.Bot.Extensions;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace DiscordBot.Bot.Authorization;

/// <summary>
/// Handles authorization for portal pages by verifying Discord OAuth authentication
/// and guild membership. This is a lighter-weight check than admin authorization
/// - it only requires being a member of the guild, no role checks. The guild must also have
/// switched its member portal on (<c>EnableMemberPortal</c>).
/// </summary>
public class PortalGuildMemberAuthorizationHandler : AuthorizationHandler<PortalGuildMemberRequirement>
{
    /// <summary>The <c>HttpContext.Items</c> key a refusal's reason is left under.</summary>
    public const string FailureReasonKey = "AuthorizationFailureReason";

    /// <summary>The reason left when the guild has switched its member portal off.</summary>
    public const string PortalDisabledReason = "PortalDisabled";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPortalGuildDirectory _guildDirectory;
    private readonly IGuildAudioSettingsRepository _audioSettingsRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PortalGuildMemberAuthorizationHandler> _logger;

    public PortalGuildMemberAuthorizationHandler(
        UserManager<ApplicationUser> userManager,
        IPortalGuildDirectory guildDirectory,
        IGuildAudioSettingsRepository audioSettingsRepository,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PortalGuildMemberAuthorizationHandler> logger)
    {
        _userManager = userManager;
        _guildDirectory = guildDirectory;
        _audioSettingsRepository = audioSettingsRepository;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PortalGuildMemberRequirement requirement)
    {
        var isAdmin = context.User.IsInRole(IdentitySeeder.Roles.SuperAdmin) ||
                      context.User.IsInRole(IdentitySeeder.Roles.Admin);

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogWarning("PortalGuildMember: HttpContext is null");
            return;
        }

        // Extract guild ID from route
        var guildIdString = httpContext.Request.RouteValues[requirement.GuildIdParameterName]?.ToString()
            ?? httpContext.Request.Query[requirement.GuildIdParameterName].FirstOrDefault();

        if (string.IsNullOrEmpty(guildIdString) || !ulong.TryParse(guildIdString, out var guildId))
        {
            // Nothing to gate: admins may reach any portal, everyone else needs a guild
            if (isAdmin)
            {
                context.Succeed(requirement);
                return;
            }

            _logger.LogDebug("PortalGuildMember: No valid guildId found in route or query");
            return;
        }

        // Check if user is authenticated
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            _logger.LogDebug("PortalGuildMember: User not authenticated, will redirect to login");
            // Let this fail - the auth middleware will redirect to login
            return;
        }

        // The member portal is its own switch (GuildAudioSettings.EnableMemberPortal, issue #947),
        // independent of AudioEnabled, and it is checked before the admin bypass: a portal that is
        // switched off is off for everyone, so the API agrees with the page.
        var audioSettings = await _audioSettingsRepository.GetByGuildIdAsync(guildId);
        if (audioSettings == null || !audioSettings.EnableMemberPortal)
        {
            _logger.LogDebug("PortalGuildMember: Member portal is not enabled for guild {GuildId}", guildId);
            httpContext.Items[FailureReasonKey] = PortalDisabledReason;
            return;
        }

        // SuperAdmins and Admins bypass portal guild membership checks
        // They need access to manage any guild's portal
        if (isAdmin)
        {
            _logger.LogDebug("PortalGuildMember: Admin user granted portal access");
            context.Succeed(requirement);
            return;
        }

        // Check if user has Discord linked (required for portal access)
        var user = await _userManager.GetUserAsync(context.User);
        if (user == null)
        {
            _logger.LogDebug("PortalGuildMember: User not found in database");
            SetForbiddenResult(httpContext);
            return;
        }

        if (!user.DiscordUserId.HasValue)
        {
            _logger.LogDebug("PortalGuildMember: User {UserId} does not have Discord linked", user.Id);
            SetForbiddenResult(httpContext);
            return;
        }

        if (!await _guildDirectory.IsGuildAvailableAsync(guildId))
        {
            _logger.LogWarning("PortalGuildMember: Guild {GuildId} not found in Discord client", guildId);
            SetNotFoundResult(httpContext);
            return;
        }

        if (!await _guildDirectory.IsMemberAsync(guildId, user.DiscordUserId.Value))
        {
            _logger.LogDebug(
                "PortalGuildMember: User {DiscordUserId} is not a member of guild {GuildId}",
                user.DiscordUserId.Value, guildId);
            SetForbiddenResult(httpContext);
            return;
        }

        _logger.LogDebug(
            "PortalGuildMember: User {DiscordUserId} granted access to guild {GuildId} portal",
            user.DiscordUserId.Value, guildId);
        context.Succeed(requirement);
    }

    /// <summary>
    /// Sets an item in HttpContext to signal a 403 Forbidden response.
    /// </summary>
    private static void SetForbiddenResult(HttpContext httpContext)
    {
        httpContext.Items[FailureReasonKey] = "Forbidden";
    }

    /// <summary>
    /// Sets an item in HttpContext to signal a 404 Not Found response.
    /// </summary>
    private static void SetNotFoundResult(HttpContext httpContext)
    {
        httpContext.Items[FailureReasonKey] = "NotFound";
    }
}
