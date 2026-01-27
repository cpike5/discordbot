using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Repository interface for Sound entities with soundboard-specific operations.
/// </summary>
public interface ISoundRepository : IRepository<Sound>
{
    /// <summary>
    /// Gets all sounds for a specific guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of all sounds for the guild.</returns>
    Task<IReadOnlyList<Sound>> GetByGuildIdAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a sound by its ID, verifying it belongs to the specified guild.
    /// </summary>
    /// <param name="id">Sound ID.</param>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sound if found and belongs to the guild, otherwise null.</returns>
    Task<Sound?> GetByIdAndGuildAsync(
        Guid id,
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a sound by its name for a specific guild.
    /// </summary>
    /// <param name="name">Sound name (case-insensitive).</param>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sound if found, otherwise null.</returns>
    Task<Sound?> GetByNameAndGuildAsync(
        string name,
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total storage used by all sounds for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sum of FileSizeBytes for all sounds in the guild.</returns>
    Task<long> GetTotalStorageUsedAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of sounds for a specific guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of sounds in the guild.</returns>
    Task<int> GetSoundCountAsync(
        ulong guildId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments the play count for a sound.
    /// </summary>
    /// <param name="soundId">Sound ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task IncrementPlayCountAsync(
        Guid soundId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the top sounds by play count for a guild since a specific time.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="count">Maximum number of sounds to return.</param>
    /// <param name="since">The start time for counting (UTC).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of tuples containing sound name and play count, ordered by play count descending.</returns>
    Task<IReadOnlyList<(string Name, int PlayCount)>> GetTopSoundsByPlayCountAsync(
        ulong guildId,
        int count,
        DateTime since,
        CancellationToken cancellationToken = default);
}
