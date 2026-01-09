using DiscordBot.Core.Entities;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for managing guild audio settings.
/// Handles volume levels, storage limits, and command role restrictions.
/// </summary>
public interface IGuildAudioSettingsService
{
    /// <summary>
    /// Gets the audio settings for a guild.
    /// Creates default settings if they don't exist.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The guild's audio settings entity.</returns>
    Task<GuildAudioSettings> GetSettingsAsync(ulong guildId, CancellationToken ct = default);

    /// <summary>
    /// Updates the audio settings for a guild.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="updateAction">Action to apply updates to the settings.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated settings entity.</returns>
    /// <remarks>
    /// Creates default settings if they don't exist before applying the update.
    /// </remarks>
    Task<GuildAudioSettings> UpdateSettingsAsync(ulong guildId, Action<GuildAudioSettings> updateAction, CancellationToken ct = default);

    /// <summary>
    /// Adds a role restriction for a specific soundboard command.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="commandName">Name of the command to restrict.</param>
    /// <param name="roleId">Discord role ID to allow.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// If the restriction already exists, this operation is idempotent.
    /// Valid command names: "upload", "play", "delete", "list", "manage"
    /// </remarks>
    Task AddCommandRestrictionAsync(ulong guildId, string commandName, ulong roleId, CancellationToken ct = default);

    /// <summary>
    /// Removes a role restriction for a specific soundboard command.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="commandName">Name of the command.</param>
    /// <param name="roleId">Discord role ID to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// If the restriction doesn't exist, this operation is idempotent.
    /// </remarks>
    Task RemoveCommandRestrictionAsync(ulong guildId, string commandName, ulong roleId, CancellationToken ct = default);

    /// <summary>
    /// Gets all roles allowed to use a specific command.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="commandName">Name of the command.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Collection of role IDs allowed to use the command. Empty collection means everyone can use the command.</returns>
    Task<IReadOnlyList<ulong>> GetAllowedRolesForCommandAsync(ulong guildId, string commandName, CancellationToken ct = default);

    /// <summary>
    /// Checks if a role is allowed to use a specific command.
    /// </summary>
    /// <param name="guildId">Discord guild ID.</param>
    /// <param name="commandName">Name of the command.</param>
    /// <param name="roleId">Discord role ID to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the role is allowed or if there are no restrictions (everyone allowed), false otherwise.</returns>
    /// <remarks>
    /// Returns true when no restrictions exist (unrestricted command).
    /// Returns true if the specific role is in the allowed list.
    /// Returns false if restrictions exist but the role is not in the allowed list.
    /// </remarks>
    Task<bool> IsCommandAllowedForRoleAsync(ulong guildId, string commandName, ulong roleId, CancellationToken ct = default);
}
