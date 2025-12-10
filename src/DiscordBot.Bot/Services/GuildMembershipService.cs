using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for checking Discord guild membership and administrative permissions.
/// Used by authorization policies to verify user access to guild-specific resources.
/// </summary>
public class GuildMembershipService : IGuildMembershipService
{
    private readonly IDiscordUserInfoService _userInfoService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GuildMembershipService> _logger;

    private const string MembershipCacheKeyPrefix = "guild:membership:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    // Discord permission flags
    private const long AdministratorPermission = 0x8; // 1 << 3
    private const long ManageGuildPermission = 0x20; // 1 << 5

    /// <summary>
    /// Initializes a new instance of the <see cref="GuildMembershipService"/> class.
    /// </summary>
    /// <param name="userInfoService">Service for retrieving Discord user information.</param>
    /// <param name="cache">Memory cache for caching membership checks.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public GuildMembershipService(
        IDiscordUserInfoService userInfoService,
        IMemoryCache cache,
        ILogger<GuildMembershipService> logger)
    {
        _userInfoService = userInfoService;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> IsMemberOfGuildAsync(
        string applicationUserId,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{MembershipCacheKeyPrefix}{applicationUserId}:{guildId}:member";

        // Try to get from cache
        if (_cache.TryGetValue(cacheKey, out bool cachedIsMember))
        {
            _logger.LogTrace("Retrieved guild membership for user {UserId} in guild {GuildId} from cache: {IsMember}",
                applicationUserId, guildId, cachedIsMember);
            return cachedIsMember;
        }

        _logger.LogDebug("Checking guild membership for user {UserId} in guild {GuildId}",
            applicationUserId, guildId);

        try
        {
            var guilds = await _userInfoService.GetUserGuildsAsync(applicationUserId, false, cancellationToken);
            var isMember = guilds.Any(g => g.Id == guildId);

            // Cache the result
            _cache.Set(cacheKey, isMember, new MemoryCacheEntryOptions
            {
                SlidingExpiration = CacheDuration
            });

            _logger.LogDebug("User {UserId} is {Membership} guild {GuildId}",
                applicationUserId, isMember ? "a member of" : "NOT a member of", guildId);

            return isMember;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check guild membership for user {UserId} in guild {GuildId}",
                applicationUserId, guildId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsGuildAdminAsync(
        string applicationUserId,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{MembershipCacheKeyPrefix}{applicationUserId}:{guildId}:admin";

        // Try to get from cache
        if (_cache.TryGetValue(cacheKey, out bool cachedIsAdmin))
        {
            _logger.LogTrace("Retrieved guild admin status for user {UserId} in guild {GuildId} from cache: {IsAdmin}",
                applicationUserId, guildId, cachedIsAdmin);
            return cachedIsAdmin;
        }

        _logger.LogDebug("Checking guild admin permissions for user {UserId} in guild {GuildId}",
            applicationUserId, guildId);

        try
        {
            var guilds = await _userInfoService.GetUserGuildsAsync(applicationUserId, false, cancellationToken);
            var guild = guilds.FirstOrDefault(g => g.Id == guildId);

            if (guild == null)
            {
                _logger.LogDebug("User {UserId} is not a member of guild {GuildId}",
                    applicationUserId, guildId);
                _cache.Set(cacheKey, false, new MemoryCacheEntryOptions
                {
                    SlidingExpiration = CacheDuration
                });
                return false;
            }

            // Check if user is owner or has Administrator permission
            var isAdmin = guild.Owner || HasPermission(guild.Permissions, AdministratorPermission);

            // Cache the result
            _cache.Set(cacheKey, isAdmin, new MemoryCacheEntryOptions
            {
                SlidingExpiration = CacheDuration
            });

            _logger.LogInformation("User {UserId} {AdminStatus} guild {GuildId} (Owner: {IsOwner}, Administrator: {HasAdministrator})",
                applicationUserId,
                isAdmin ? "has admin permissions in" : "does NOT have admin permissions in",
                guildId,
                guild.Owner,
                HasPermission(guild.Permissions, AdministratorPermission));

            return isAdmin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check guild admin permissions for user {UserId} in guild {GuildId}",
                applicationUserId, guildId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DiscordGuildDto>> GetAdministeredGuildsAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving administered guilds for user {UserId}", applicationUserId);

        try
        {
            var guilds = await _userInfoService.GetUserGuildsAsync(applicationUserId, false, cancellationToken);

            var administeredGuilds = guilds
                .Where(g => g.Owner || HasPermission(g.Permissions, AdministratorPermission))
                .ToList()
                .AsReadOnly();

            _logger.LogInformation("User {UserId} has admin permissions in {Count} of {TotalCount} guilds",
                applicationUserId, administeredGuilds.Count, guilds.Count);

            return administeredGuilds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve administered guilds for user {UserId}", applicationUserId);
            throw;
        }
    }

    /// <summary>
    /// Checks if a permission flag is set in the permissions bitfield.
    /// </summary>
    /// <param name="permissions">The permissions bitfield.</param>
    /// <param name="permission">The permission flag to check.</param>
    /// <returns>True if the permission is granted, false otherwise.</returns>
    private static bool HasPermission(long permissions, long permission)
    {
        return (permissions & permission) == permission;
    }
}
