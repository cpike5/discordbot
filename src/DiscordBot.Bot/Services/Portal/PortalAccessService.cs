using System.Security.Claims;
using Discord;
using Discord.WebSocket;
using DiscordBot.Bot.Extensions;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace DiscordBot.Bot.Services.Portal;

/// <summary>
/// Default <see cref="IPortalAccessService"/>. Behaviourally identical to the logic
/// <c>PortalPageModelBase.CheckPortalAuthorizationAsync</c> has always run inline - same
/// ordering (guild lookup, then Discord client lookup, then auth state, then Discord-link check,
/// then Admin/SuperAdmin bypass, then cache-then-REST guild membership check), same login URL
/// shape, same "user not found or unlinked is treated as unauthenticated" rule.
/// </summary>
/// <remarks>
/// <see cref="DiscordGuildExists"/> and <see cref="IsGuildMemberAsync"/> are protected virtual
/// seams, not inlined, for the same reason <c>GuildAccessHandler.GetLiveGuildMembershipAsync</c>
/// is one: Discord.Net's <c>SocketGuild</c>/<c>SocketGuildUser</c> are sealed and cannot be
/// mocked, so a test subclass overrides these two instead (see <c>PortalAccessServiceTests</c>).
/// </remarks>
public class PortalAccessService : IPortalAccessService
{
    private readonly IGuildService _guildService;
    private readonly DiscordSocketClient _discordClient;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger _logger;

    public PortalAccessService(
        IGuildService guildService,
        DiscordSocketClient discordClient,
        UserManager<ApplicationUser> userManager,
        ILogger logger)
    {
        _guildService = guildService;
        _discordClient = discordClient;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<PortalAccessResult> ResolveAsync(
        ulong guildId, ClaimsPrincipal user, string returnPath, CancellationToken ct = default)
    {
        var guild = await _guildService.GetGuildByIdAsync(guildId, ct);
        if (guild is null)
        {
            _logger.LogWarning("Guild {GuildId} not found", guildId);
            return PortalAccessResult.GuildNotFound();
        }

        if (!DiscordGuildExists(guildId))
        {
            _logger.LogWarning("Guild {GuildId} not found in Discord client", guildId);
            return PortalAccessResult.GuildNotFound();
        }

        var isBotOnline = _discordClient.ConnectionState == ConnectionState.Connected;
        var context = new PortalContext(guild, guildId.ToString(), guild.Name, guild.IconUrl, isBotOnline);
        var loginUrl = $"/Account/Login?returnUrl={Uri.EscapeDataString(returnPath)}";

        var isAuthenticated = user.Identity?.IsAuthenticated ?? false;
        if (!isAuthenticated)
        {
            _logger.LogDebug("Unauthenticated user viewing landing page for guild {GuildId}", guildId);
            return PortalAccessResult.ShowLanding(context, loginUrl);
        }

        var appUser = await _userManager.GetUserAsync(user);
        if (appUser is null || !appUser.DiscordUserId.HasValue)
        {
            _logger.LogDebug("User not found or no Discord linked, showing landing page for guild {GuildId}", guildId);
            return PortalAccessResult.ShowLanding(context, loginUrl);
        }

        // SuperAdmins and Admins bypass guild membership checks (consistent with
        // PortalGuildMemberAuthorizationHandler).
        if (user.IsInRole(IdentitySeeder.Roles.SuperAdmin) || user.IsInRole(IdentitySeeder.Roles.Admin))
        {
            _logger.LogDebug(
                "Admin user {DiscordUserId} granted portal access for guild {GuildId}",
                appUser.DiscordUserId.Value, guildId);
            return PortalAccessResult.Authorized(context, loginUrl);
        }

        var isMember = await IsGuildMemberAsync(guildId, appUser.DiscordUserId.Value);
        if (!isMember)
        {
            _logger.LogDebug(
                "User {DiscordUserId} is not a member of guild {GuildId}",
                appUser.DiscordUserId.Value, guildId);
            return PortalAccessResult.NotGuildMember(context, loginUrl);
        }

        _logger.LogDebug(
            "User {DiscordUserId} authorized for Portal in guild {GuildId}",
            appUser.DiscordUserId.Value, guildId);
        return PortalAccessResult.Authorized(context, loginUrl);
    }

    /// <summary>Whether <paramref name="guildId"/> is visible in the bot's Discord client cache.</summary>
    protected virtual bool DiscordGuildExists(ulong guildId) => _discordClient.GetGuild(guildId) is not null;

    /// <summary>
    /// Checks Discord guild membership: the gateway cache first, falling back to a REST API call
    /// (since <c>AlwaysDownloadUsers</c> is false, the cache may be incomplete). A REST failure is
    /// logged and treated as "not a member" rather than propagated, matching the original inline
    /// behavior.
    /// </summary>
    protected virtual async Task<bool> IsGuildMemberAsync(ulong guildId, ulong discordUserId)
    {
        var socketGuild = _discordClient.GetGuild(guildId);
        if (socketGuild?.GetUser(discordUserId) is not null)
        {
            return true;
        }

        try
        {
            var restUser = await _discordClient.Rest.GetGuildUserAsync(guildId, discordUserId);
            return restUser is not null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to verify guild membership via REST for user {DiscordUserId} in guild {GuildId}",
                discordUserId, guildId);
            return false;
        }
    }
}
