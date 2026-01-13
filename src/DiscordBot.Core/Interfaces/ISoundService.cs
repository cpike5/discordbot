using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing soundboard sounds.
/// Handles sound CRUD operations, validation, and usage tracking.
/// </summary>
public interface ISoundService
{
    /// <summary>
    /// Gets a sound by its unique identifier with guild validation.
    /// </summary>
    /// <param name="id">Unique identifier of the sound.</param>
    /// <param name="guildId">Discord guild ID to validate ownership.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sound entity, or null if not found or guild mismatch.</returns>
    Task<Sound?> GetByIdAsync(Guid id, ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Gets all sounds for a specific guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Read-only collection of sounds ordered by name.</returns>
    Task<IReadOnlyList<Sound>> GetAllByGuildAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Gets a sound by its name within a guild.
    /// Name lookup is case-insensitive.
    /// </summary>
    /// <param name="name">Name of the sound.</param>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sound entity, or null if not found.</returns>
    Task<Sound?> GetByNameAsync(string name, ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new sound.
    /// </summary>
    /// <param name="sound">The sound entity to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created sound entity with generated ID.</returns>
    /// <remarks>
    /// Validates that the sound name is unique within the guild.
    /// Throws InvalidOperationException if a sound with the same name already exists.
    /// </remarks>
    Task<Sound> CreateSoundAsync(Sound sound, CancellationToken ct = default);

    /// <summary>
    /// Deletes a sound with guild validation.
    /// </summary>
    /// <param name="id">Unique identifier of the sound.</param>
    /// <param name="guildId">Discord guild ID to validate ownership.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the sound was found and deleted, false if not found or guild mismatch.</returns>
    /// <remarks>
    /// This method only removes the database record. The caller is responsible for deleting the associated file.
    /// </remarks>
    Task<bool> DeleteSoundAsync(Guid id, ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Increments the play count for a sound.
    /// Updates LastPlayedAt timestamp to current UTC time.
    /// </summary>
    /// <param name="soundId">Unique identifier of the sound.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task IncrementPlayCountAsync(Guid soundId, CancellationToken ct = default);

    /// <summary>
    /// Validates whether adding additional bytes would exceed the guild's storage limit.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="additionalBytes">Number of bytes to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the addition would remain within limits, false if it would exceed the limit.</returns>
    /// <remarks>
    /// The storage limit is defined in GuildAudioSettings.MaxStorageBytes.
    /// </remarks>
    Task<bool> ValidateStorageLimitAsync(ulong guildId, long additionalBytes, CancellationToken ct = default);

    /// <summary>
    /// Validates whether the guild can add another sound without exceeding the count limit.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the guild is within the sound count limit, false if at or over the limit.</returns>
    /// <remarks>
    /// The sound count limit is defined in GuildAudioSettings.MaxSounds.
    /// </remarks>
    Task<bool> ValidateSoundCountLimitAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Gets the total storage used by all sounds in a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Total bytes used by all sound files in the guild.</returns>
    Task<long> GetStorageUsedAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Gets the total number of sounds in a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Count of sounds in the guild.</returns>
    Task<int> GetSoundCountAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Logs a sound play event for analytics tracking.
    /// Creates a new SoundPlayLog entry with the current timestamp.
    /// </summary>
    /// <param name="soundId">Unique identifier of the sound.</param>
    /// <param name="guildId">Discord guild ID where the sound was played.</param>
    /// <param name="userId">Discord user ID who played the sound.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method logs failures as warnings but does not throw exceptions.
    /// Logging failures should not block playback functionality.
    /// </remarks>
    Task LogPlayAsync(Guid soundId, ulong guildId, ulong userId, CancellationToken ct = default);
}
