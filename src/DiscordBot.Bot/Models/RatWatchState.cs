namespace DiscordBot.Bot.Models;

/// <summary>
/// State data for Rat Watch check-in interactions.
/// </summary>
public class RatWatchCheckInState
{
    /// <summary>
    /// The Rat Watch ID.
    /// </summary>
    public Guid WatchId { get; set; }

    /// <summary>
    /// The Discord user ID of the accused.
    /// </summary>
    public ulong AccusedUserId { get; set; }
}

/// <summary>
/// State data for Rat Watch voting interactions.
/// </summary>
public class RatWatchVotingState
{
    /// <summary>
    /// The Rat Watch ID.
    /// </summary>
    public Guid WatchId { get; set; }

    /// <summary>
    /// The Discord guild ID.
    /// </summary>
    public ulong GuildId { get; set; }
}
