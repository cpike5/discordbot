using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for managing Discord guild memberships captured during OAuth authentication.
/// Stores guild membership data locally to enable guild-based access control without
/// requiring real-time Discord API calls.
/// </summary>
public class UserDiscordGuildService : IUserDiscordGuildService
{
    private readonly BotDbContext _context;
    private readonly ILogger<UserDiscordGuildService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserDiscordGuildService"/> class.
    /// </summary>
    /// <param name="context">The database context for storing guild memberships.</param>
    /// <param name="logger">Logger for diagnostic information.</param>
    public UserDiscordGuildService(
        BotDbContext context,
        ILogger<UserDiscordGuildService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<int> StoreGuildMembershipsAsync(
        string applicationUserId,
        IEnumerable<DiscordGuildDto> guilds,
        CancellationToken cancellationToken = default)
    {
        var guildList = guilds.ToList();
        _logger.LogDebug("Storing {Count} guild memberships for user {UserId}",
            guildList.Count, applicationUserId);

        try
        {
            // Get existing guild memberships for this user
            var existingMemberships = await _context.UserDiscordGuilds
                .Where(g => g.ApplicationUserId == applicationUserId)
                .ToListAsync(cancellationToken);

            var existingGuildIds = existingMemberships.Select(m => m.GuildId).ToHashSet();
            var newGuildIds = guildList.Select(g => g.Id).ToHashSet();

            // Track counts for logging
            var addedCount = 0;
            var updatedCount = 0;
            var removedCount = 0;

            // Remove memberships for guilds the user has left
            var guildsToRemove = existingMemberships.Where(m => !newGuildIds.Contains(m.GuildId)).ToList();
            if (guildsToRemove.Any())
            {
                _context.UserDiscordGuilds.RemoveRange(guildsToRemove);
                removedCount = guildsToRemove.Count;
                _logger.LogDebug("Removing {Count} stale guild memberships for user {UserId}",
                    removedCount, applicationUserId);
            }

            // Process each guild from OAuth
            foreach (var guild in guildList)
            {
                var existing = existingMemberships.FirstOrDefault(m => m.GuildId == guild.Id);

                if (existing != null)
                {
                    // Update existing membership
                    existing.GuildName = guild.Name;
                    existing.GuildIconHash = guild.Icon;
                    existing.IsOwner = guild.Owner;
                    existing.Permissions = guild.Permissions;
                    existing.LastUpdatedAt = DateTime.UtcNow;
                    updatedCount++;
                }
                else
                {
                    // Add new membership
                    var newMembership = new UserDiscordGuild
                    {
                        Id = Guid.NewGuid(),
                        ApplicationUserId = applicationUserId,
                        GuildId = guild.Id,
                        GuildName = guild.Name,
                        GuildIconHash = guild.Icon,
                        IsOwner = guild.Owner,
                        Permissions = guild.Permissions,
                        CapturedAt = DateTime.UtcNow,
                        LastUpdatedAt = DateTime.UtcNow
                    };
                    _context.UserDiscordGuilds.Add(newMembership);
                    addedCount++;
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Stored guild memberships for user {UserId}: {Added} added, {Updated} updated, {Removed} removed",
                applicationUserId, addedCount, updatedCount, removedCount);

            return guildList.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store guild memberships for user {UserId}", applicationUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UserDiscordGuild>> GetUserGuildsAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Retrieving guild memberships for user {UserId}", applicationUserId);

        try
        {
            var guilds = await _context.UserDiscordGuilds
                .Where(g => g.ApplicationUserId == applicationUserId)
                .OrderBy(g => g.GuildName)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            _logger.LogDebug("Retrieved {Count} guild memberships for user {UserId}",
                guilds.Count, applicationUserId);

            return guilds.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve guild memberships for user {UserId}", applicationUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> HasGuildMembershipAsync(
        string applicationUserId,
        ulong guildId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Checking guild {GuildId} membership for user {UserId}",
            guildId, applicationUserId);

        try
        {
            var hasMembership = await _context.UserDiscordGuilds
                .AnyAsync(g => g.ApplicationUserId == applicationUserId && g.GuildId == guildId,
                    cancellationToken);

            _logger.LogDebug("User {UserId} {HasMembership} guild {GuildId} membership",
                applicationUserId, hasMembership ? "has" : "does not have", guildId);

            return hasMembership;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check guild {GuildId} membership for user {UserId}",
                guildId, applicationUserId);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteUserGuildsAsync(
        string applicationUserId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting all guild memberships for user {UserId}", applicationUserId);

        try
        {
            var guildsToDelete = await _context.UserDiscordGuilds
                .Where(g => g.ApplicationUserId == applicationUserId)
                .ToListAsync(cancellationToken);

            if (guildsToDelete.Any())
            {
                _context.UserDiscordGuilds.RemoveRange(guildsToDelete);
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Deleted {Count} guild memberships for user {UserId}",
                    guildsToDelete.Count, applicationUserId);
            }
            else
            {
                _logger.LogDebug("No guild memberships found to delete for user {UserId}", applicationUserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete guild memberships for user {UserId}", applicationUserId);
            throw;
        }
    }
}
