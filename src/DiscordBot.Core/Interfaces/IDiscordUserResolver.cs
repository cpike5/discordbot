namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Resolves Discord user IDs to display information (username, avatar URL)
/// via the Discord REST API with short-lived caching.
/// </summary>
public interface IDiscordUserResolver
{
    /// <summary>
    /// Resolves a single Discord user ID to a username and optional avatar URL.
    /// Returns fallback values if the user cannot be found.
    /// </summary>
    /// <param name="userId">The Discord user ID to resolve.</param>
    /// <returns>A tuple of (Username, AvatarUrl). Username defaults to "Unknown#{userId}" on failure.</returns>
    Task<(string Username, string? AvatarUrl)> ResolveUserAsync(ulong userId);

    /// <summary>
    /// Resolves multiple Discord user IDs to usernames and optional avatar URLs in batch.
    /// Deduplicates input IDs and returns fallback values for any users that cannot be found.
    /// </summary>
    /// <param name="userIds">The Discord user IDs to resolve.</param>
    /// <returns>A dictionary mapping each user ID to (Username, AvatarUrl).</returns>
    Task<Dictionary<ulong, (string Username, string? AvatarUrl)>> ResolveUsersAsync(IEnumerable<ulong> userIds);
}
