using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for GuildMember entities with Discord-specific operations.
/// </summary>
public interface IGuildMemberRepository : IRepository<GuildMember>
{
    /// <summary>
    /// Gets a member by guild and user IDs.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="userId">The Discord user snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guild member if found, null otherwise.</returns>
    Task<GuildMember?> GetByGuildAndUserAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active members for a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of active guild members.</returns>
    Task<IReadOnlyList<GuildMember>> GetActiveByGuildAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all member user IDs for a guild (for reconciliation comparison).
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A hash set of user IDs for all active members in the guild.</returns>
    Task<HashSet<ulong>> GetMemberUserIdsAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a single member (insert or update).
    /// </summary>
    /// <param name="member">The guild member to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The upserted guild member.</returns>
    Task<GuildMember> UpsertAsync(
        GuildMember member,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch upserts multiple members in a single transaction.
    /// Returns the count of affected records.
    /// </summary>
    /// <param name="members">The collection of guild members to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of records affected.</returns>
    Task<int> BatchUpsertAsync(
        IEnumerable<GuildMember> members,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks members as inactive (soft delete) for users not in the provided list.
    /// Used for reconciliation to detect members who left while bot was offline.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="activeUserIds">The collection of user IDs that are currently active.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of members marked as inactive.</returns>
    Task<int> MarkInactiveExceptAsync(
        ulong guildId,
        IEnumerable<ulong> activeUserIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a single member as inactive.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="userId">The Discord user snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a member was marked inactive, false if not found.</returns>
    Task<bool> MarkInactiveAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates member nickname and roles.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="userId">The Discord user snowflake ID.</param>
    /// <param name="nickname">The guild-specific nickname.</param>
    /// <param name="cachedRolesJson">The JSON array of role IDs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a member was updated, false if not found.</returns>
    Task<bool> UpdateMemberInfoAsync(
        ulong guildId,
        ulong userId,
        string? nickname,
        string? cachedRolesJson,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets count of members for a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="activeOnly">If true, only count active members. Default is true.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of members matching the criteria.</returns>
    Task<int> GetMemberCountAsync(
        ulong guildId,
        bool activeOnly = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the last sync timestamp for a guild.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The most recent LastCachedAt timestamp for the guild, or null if no members exist.</returns>
    Task<DateTime?> GetLastSyncTimeAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a paginated list of guild members with filtering, searching, and sorting.
    /// Includes the User navigation property.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="searchTerm">Optional search term to filter by username, global display name, or nickname.</param>
    /// <param name="roleIds">Optional list of role IDs to filter by. Members must have ALL specified roles.</param>
    /// <param name="joinedAtStart">Optional filter for join date range start (inclusive).</param>
    /// <param name="joinedAtEnd">Optional filter for join date range end (inclusive).</param>
    /// <param name="lastActiveAtStart">Optional filter for last active date range start (inclusive).</param>
    /// <param name="lastActiveAtEnd">Optional filter for last active date range end (inclusive).</param>
    /// <param name="isActive">Optional filter by active status. Null for all, true for active only, false for inactive only.</param>
    /// <param name="sortBy">Field to sort by: Username, DisplayName, JoinedAt, or LastActiveAt.</param>
    /// <param name="sortDescending">Sort in descending order if true.</param>
    /// <param name="page">Page number (1-based).</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="userIds">Optional list of specific user IDs to filter by. Used for exporting selected members.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple containing the list of members and the total count.</returns>
    Task<(IReadOnlyList<GuildMember> Members, int TotalCount)> GetMembersAsync(
        ulong guildId,
        string? searchTerm = null,
        List<ulong>? roleIds = null,
        DateTime? joinedAtStart = null,
        DateTime? joinedAtEnd = null,
        DateTime? lastActiveAtStart = null,
        DateTime? lastActiveAtEnd = null,
        bool? isActive = true,
        string sortBy = "JoinedAt",
        bool sortDescending = false,
        int page = 1,
        int pageSize = 25,
        List<ulong>? userIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single guild member by guild and user IDs with User navigation property included.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="userId">The Discord user snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guild member with User entity if found, null otherwise.</returns>
    Task<GuildMember?> GetMemberAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default);
}
