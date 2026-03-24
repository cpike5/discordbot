using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for managing audio playback log entries.
/// Provides methods for logging audio plays and querying with pagination and filters.
/// </summary>
public interface IAudioPlaybackLogRepository : IRepository<AudioPlaybackLog>
{
    /// <summary>
    /// Gets a paged list of audio playback log entries with optional filters.
    /// </summary>
    /// <param name="guildId">The Discord guild ID to filter by.</param>
    /// <param name="page">The 1-based page number.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="featureType">Optional filter by audio feature type.</param>
    /// <param name="userId">Optional filter by Discord user ID.</param>
    /// <param name="from">Optional filter for entries on or after this date (UTC).</param>
    /// <param name="to">Optional filter for entries on or before this date (UTC).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the page of items and the total count matching the filters.</returns>
    Task<(IReadOnlyList<AudioPlaybackLog> Items, int TotalCount)> GetPagedAsync(
        ulong guildId,
        int page,
        int pageSize,
        AudioFeatureType? featureType,
        ulong? userId,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default);
}
