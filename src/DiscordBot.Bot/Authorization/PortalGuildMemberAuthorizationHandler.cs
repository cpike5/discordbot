using Discord.WebSocket;
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

        // Check if portal is enabled for this guild
        var audioSettings = await _audioSettingsRepository.GetByGuildIdAsync(guildId);

        // TODO: Issue #947 will add EnableMemberPortal property
        // For now, we check AudioEnabled as a proxy
        // Once #947 is merged, change this to: !audioSettings.EnableMemberPortal
        if (audioSettings == null || !audioSettings.AudioEnabled)
        {
            _logger.LogDebug("PortalGuildMember: Portal not enabled for guild {GuildId}", guildId);
            SetNotFoundResult(httpContext);
            return;
        }

        // Check if user is a member of the guild via Discord API
        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogWarning("PortalGuildMember: Guild {GuildId} not found in Discord client", guildId);
            SetNotFoundResult(httpContext);
            return;
        }

        var guildUser = guild.GetUser(user.DiscordUserId.Value);
        if (guildUser == null)
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
        httpContext.Items["AuthorizationFailureReason"] = "Forbidden";
    }

    /// <summary>
    /// Sets an item in HttpContext to signal a 404 Not Found response.
    /// </summary>
    private static void SetNotFoundResult(HttpContext httpContext)
    {
        httpContext.Items["AuthorizationFailureReason"] = "NotFound";
    }
}
