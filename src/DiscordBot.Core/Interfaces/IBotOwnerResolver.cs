namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Resolves the bot application owner from the Discord API.
/// Implemented in the Bot layer where DiscordSocketClient is available.
/// </summary>
public interface IBotOwnerResolver
{
    /// <summary>
    /// Gets the Discord user ID of the bot application owner.
    /// Result is cached after first lookup.
    /// </summary>
    Task<ulong> GetOwnerIdAsync();
}
