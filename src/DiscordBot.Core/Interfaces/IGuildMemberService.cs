using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing guild members with business logic, caching, and export capabilities.
/// </summary>
public interface IGuildMemberService
{
    /// <summary>
    /// Gets a paginated list of guild members with filtering, searching, and sorting.
    /// Results are cached to reduce database load.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="query">Query parameters for filtering, searching, sorting, and pagination.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated response containing guild member DTOs.</returns>
    Task<PaginatedResponseDto<GuildMemberDto>> GetMembersAsync(
        ulong guildId,
        GuildMemberQueryDto query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single guild member by user ID.
    /// Results are cached to reduce database load.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="userId">The Discord user snowflake ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guild member DTO if found, null otherwise.</returns>
    Task<GuildMemberDto?> GetMemberAsync(
        ulong guildId,
        ulong userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of guild members matching the specified filters.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="query">Query parameters for filtering and searching.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The count of members matching the criteria.</returns>
    Task<int> GetMemberCountAsync(
        ulong guildId,
        GuildMemberQueryDto query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports guild members to a CSV file with optional filtering.
    /// Enforces a maximum row limit to prevent resource exhaustion.
    /// </summary>
    /// <param name="guildId">The Discord guild snowflake ID.</param>
    /// <param name="query">Query parameters for filtering and searching.</param>
    /// <param name="maxRows">Maximum number of rows to export. Default is 10,000.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A byte array containing the CSV data.</returns>
    Task<byte[]> ExportMembersToCsvAsync(
        ulong guildId,
        GuildMemberQueryDto query,
        int maxRows = 10000,
        CancellationToken cancellationToken = default);
}
