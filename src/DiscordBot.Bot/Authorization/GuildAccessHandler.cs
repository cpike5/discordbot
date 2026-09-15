using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using GuildPermissions = Discord.GuildPermissions;

namespace DiscordBot.Bot.Authorization;

/// <summary>
/// Handles guild-specific authorization by verifying user membership/ownership.
/// This is the single guild-access handler registered in DI; it does not query Discord's
/// gateway on every request. It checks the locally cached <see cref="UserDiscordGuild"/>
/// membership (and any explicit <see cref="UserGuildAccess"/> grant) first, and only falls
/// back to a live <see cref="DiscordSocketClient"/> lookup when nothing is cached for the
/// user/guild pair. A live hit refreshes the cache so the next check for that guild is
/// served from cache again.
/// </summary>
/// <remarks>
/// Authorization flow:
/// 1. SuperAdmin bypasses all checks.
/// 2. Guild ID is resolved from <see cref="AuthorizationHandlerContext.Resource"/> first (a
///    raw <c>ulong</c>) so callers outside an HTTP request - e.g. a future Blazor
///    <c>GuildLayout</c> calling <c>IAuthorizationService.AuthorizeAsync(user, guildId, "GuildAccess")</c>
///    directly - do not need a route. It falls back to the <c>guildId</c> route value, then
///    the <c>guildId</c> query string, for the existing Razor Page/controller callers.
/// 3. The user must have a linked Discord account (<see cref="ApplicationUser.DiscordUserId"/>).
/// 4. Cache check: a cached <see cref="UserDiscordGuild"/> row for the guild - unless its
///    <see cref="UserDiscordGuild.LastUpdatedAt"/> is older than
///    <see cref="GuildMembershipCacheOptions.MembershipMaxAge"/>, in which case it is treated
///    as a cache miss (step 5) rather than trusted - or an explicit <see cref="UserGuildAccess"/>
///    grant. Both are compared against <see cref="GuildAccessRequirement.MinimumLevel"/> via the
///    same effective <see cref="GuildAccessLevel"/> computation as the live-lookup path: the
///    Admin role maps a cached Administrator permission bit (captured from Discord OAuth) to
///    the top of the enum, Moderator/Viewer map plain membership to <see cref="GuildAccessLevel.Viewer"/>,
///    and an explicit grant keeps its stored level. A fresh cache hit never falls through to a
///    live lookup, even if it turns out to be insufficient.
/// 5. Cache miss (no fresh cached row and no sufficient explicit grant): a live gateway lookup
///    via <see cref="DiscordSocketClient"/>. On a live hit the cache is refreshed (best-effort)
///    via <see cref="IUserDiscordGuildService.UpsertGuildMembershipAsync"/> before the
///    requirement is evaluated, so a transient refresh failure never blocks authorization; a
///    live miss leaves any stale cached row in place rather than deleting it, since the next
///    check will simply repeat the (cheap) live lookup.
/// </remarks>
public class GuildAccessHandler : AuthorizationHandler<GuildAccessRequirement>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly DiscordSocketClient _discordClient;
    private readonly BotDbContext _dbContext;
    private readonly IUserDiscordGuildService _userDiscordGuildService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<GuildAccessHandler> _logger;
    private readonly GuildMembershipCacheOptions _cacheOptions;

    public GuildAccessHandler(
        UserManager<ApplicationUser> userManager,
        DiscordSocketClient discordClient,
        BotDbContext dbContext,
        IUserDiscordGuildService userDiscordGuildService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<GuildAccessHandler> logger,
        IOptions<GuildMembershipCacheOptions> cacheOptions)
    {
        _userManager = userManager;
        _discordClient = discordClient;
        _dbContext = dbContext;
        _userDiscordGuildService = userDiscordGuildService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _cacheOptions = cacheOptions.Value;
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

        var guildId = ResolveGuildId(context, requirement);
        if (guildId is null)
        {
            _logger.LogDebug("Guild ID could not be resolved, denying guild access");
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

        var isAdminRole = context.User.IsInRole(IdentitySeeder.Roles.Admin);

        // --- Cache check -----------------------------------------------------------------
        var cachedGuilds = await _userDiscordGuildService.GetUserGuildsAsync(user.Id);
        var cachedMembership = cachedGuilds.FirstOrDefault(g => g.GuildId == guildId.Value);
        if (cachedMembership != null && IsStale(cachedMembership))
        {
            _logger.LogDebug(
                "Cached membership for user {DiscordUserId} in guild {GuildId} is older than {MembershipMaxAge}, treating as a cache miss",
                user.DiscordUserId.Value, guildId.Value, _cacheOptions.MembershipMaxAge);
            cachedMembership = null;
        }

        var explicitGrant = await _dbContext.UserGuildAccess
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ApplicationUserId == user.Id && a.GuildId == guildId.Value);
        var explicitGrantSufficient = explicitGrant != null && explicitGrant.AccessLevel >= requirement.MinimumLevel;

        if (cachedMembership != null)
        {
            var membershipLevel = EffectiveMembershipLevel(isAdminRole, HasAdministratorPermission(cachedMembership.Permissions));
            if (membershipLevel is { } level && level >= requirement.MinimumLevel)
            {
                _logger.LogDebug(
                    "User {DiscordUserId} has a cached membership in guild {GuildId} sufficient for {MinimumLevel} (effective {EffectiveLevel}), granting access",
                    user.DiscordUserId.Value, guildId.Value, requirement.MinimumLevel, level);
                context.Succeed(requirement);
                return;
            }

            if (explicitGrantSufficient)
            {
                _logger.LogDebug(
                    "User {DiscordUserId} has an insufficient cached membership in guild {GuildId} but a sufficient explicit grant",
                    user.DiscordUserId.Value, guildId.Value);
                context.Succeed(requirement);
                return;
            }

            // Cache hit but insufficient - cache-first design does not fall back to a live
            // lookup here; the cached row is treated as authoritative until it expires.
            _logger.LogDebug(
                "User {DiscordUserId} has a cached membership in guild {GuildId} that is insufficient for {MinimumLevel} and no sufficient grant, denying",
                user.DiscordUserId.Value, guildId.Value, requirement.MinimumLevel);
            return;
        }

        if (explicitGrantSufficient)
        {
            _logger.LogDebug(
                "User {DiscordUserId} granted access to guild {GuildId} via explicit UserGuildAccess grant",
                user.DiscordUserId.Value, guildId.Value);
            context.Succeed(requirement);
            return;
        }

        // --- Cache miss: live gateway lookup ----------------------------------------------
        var live = await GetLiveGuildMembershipAsync(guildId.Value, user.DiscordUserId.Value);
        if (live is null)
        {
            _logger.LogDebug(
                "User {DiscordUserId} is not a member of guild {GuildId} (cache miss, live miss)",
                user.DiscordUserId.Value, guildId.Value);
            return;
        }

        // Live hit - refresh the cache regardless of the permission outcome below, so the
        // next check for this user/guild is served from cache again.
        await TryRefreshCachedMembershipAsync(user.Id, guildId.Value, live);

        var liveMembershipLevel = EffectiveMembershipLevel(isAdminRole, live.IsAdministrator);
        if (liveMembershipLevel is not { } liveLevel || liveLevel < requirement.MinimumLevel)
        {
            _logger.LogDebug(
                "User {DiscordUserId} lacks sufficient access to guild {GuildId} for {MinimumLevel} via live lookup",
                user.DiscordUserId.Value, guildId.Value, requirement.MinimumLevel);
            return;
        }

        _logger.LogDebug(
            "User {DiscordUserId} granted access to guild {GuildId} via live lookup",
            user.DiscordUserId.Value, guildId.Value);
        context.Succeed(requirement);
    }

    private bool IsStale(UserDiscordGuild membership)
        => DateTime.UtcNow - membership.LastUpdatedAt > _cacheOptions.MembershipMaxAge;

    /// <summary>
    /// Computes the effective <see cref="GuildAccessLevel"/> a plain membership signal (cached
    /// or live) grants, used identically by both the cache-check and live-lookup paths so the
    /// two stay in sync. The Admin application role requires Discord's Administrator permission
    /// bit and, when present, maps it to the highest level the enum defines (SuperAdmin is a
    /// separate application role handled earlier, not a member of this enum) since that
    /// permission grants full guild control; without it, the Admin role earns nothing from
    /// membership alone. The Moderator/Viewer application roles only need to be a guild member,
    /// which maps to the lowest level so a stricter <see cref="GuildAccessRequirement.MinimumLevel"/>
    /// than the default (the only policy in use today) can still deny it.
    /// </summary>
    private static GuildAccessLevel? EffectiveMembershipLevel(bool isAdminRole, bool hasAdministratorPermission)
    {
        if (isAdminRole)
        {
            return hasAdministratorPermission ? GuildAccessLevel.Owner : null;
        }

        return GuildAccessLevel.Viewer;
    }

    /// <summary>
    /// Resolves the target guild ID for this authorization check.
    /// </summary>
    /// <remarks>
    /// Order: <see cref="AuthorizationHandlerContext.Resource"/> as a raw <c>ulong</c> first
    /// (this is how a non-HTTP caller, e.g. the future Blazor <c>GuildLayout</c>, is expected
    /// to pass the guild ID); then the <c>guildId</c> route value; then the <c>guildId</c>
    /// query string. No resource type in this codebase exposes a <c>GuildId</c> property
    /// today, so that step of the originally-specified resolution order is intentionally
    /// omitted - add a typed branch here if such a resource type is introduced later.
    /// </remarks>
    private ulong? ResolveGuildId(AuthorizationHandlerContext context, GuildAccessRequirement requirement)
    {
        if (context.Resource is ulong resourceGuildId)
        {
            return resourceGuildId;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            _logger.LogDebug("HttpContext is null and no resource guild ID was supplied, cannot resolve guild access");
            return null;
        }

        var guildIdString = httpContext.Request.RouteValues[requirement.GuildIdParameterName]?.ToString()
            ?? httpContext.Request.Query[requirement.GuildIdParameterName].FirstOrDefault();

        return ulong.TryParse(guildIdString, out var guildId) ? guildId : null;
    }

    private static bool HasAdministratorPermission(long permissionsRaw)
        => new GuildPermissions((ulong)permissionsRaw).Administrator;

    /// <summary>
    /// Performs the live Discord gateway lookup. Kept behind this seam (rather than inlined)
    /// so tests can override it without needing to construct Discord.Net's sealed
    /// <see cref="SocketGuild"/>/<see cref="SocketGuildUser"/> types.
    /// </summary>
    protected virtual Task<LiveGuildMembershipResult?> GetLiveGuildMembershipAsync(ulong guildId, ulong discordUserId)
    {
        var guild = _discordClient.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogDebug("Guild {GuildId} not found in Discord client", guildId);
            return Task.FromResult<LiveGuildMembershipResult?>(null);
        }

        var guildUser = guild.GetUser(discordUserId);
        if (guildUser == null)
        {
            _logger.LogDebug(
                "User {DiscordUserId} is not a member of guild {GuildId}", discordUserId, guildId);
            return Task.FromResult<LiveGuildMembershipResult?>(null);
        }

        return Task.FromResult<LiveGuildMembershipResult?>(new LiveGuildMembershipResult(
            IsAdministrator: guildUser.GuildPermissions.Administrator,
            GuildName: guild.Name,
            GuildIconHash: guild.IconId,
            IsOwner: guild.OwnerId == discordUserId,
            PermissionsRaw: (long)guildUser.GuildPermissions.RawValue));
    }

    private async Task TryRefreshCachedMembershipAsync(
        string applicationUserId, ulong guildId, LiveGuildMembershipResult live)
    {
        try
        {
            await _userDiscordGuildService.UpsertGuildMembershipAsync(
                applicationUserId,
                new DiscordGuildDto
                {
                    Id = guildId,
                    Name = live.GuildName,
                    Icon = live.GuildIconHash,
                    Owner = live.IsOwner,
                    Permissions = live.PermissionsRaw
                });
        }
        catch (Exception ex)
        {
            // Best-effort refresh: a failure here must not block authorization, since the
            // live lookup above already answered the requirement.
            _logger.LogWarning(ex,
                "Failed to refresh cached guild membership for user {UserId} in guild {GuildId}",
                applicationUserId, guildId);
        }
    }

    /// <summary>
    /// Result of a live Discord gateway membership lookup, decoupled from Discord.Net's
    /// sealed socket types so <see cref="GetLiveGuildMembershipAsync"/> can be overridden in
    /// tests.
    /// </summary>
    protected internal sealed record LiveGuildMembershipResult(
        bool IsAdministrator,
        string GuildName,
        string? GuildIconHash,
        bool IsOwner,
        long PermissionsRaw);
}
