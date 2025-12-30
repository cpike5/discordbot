namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for real-time guild activity updates.
/// </summary>
public class GuildActivityUpdateDto
{
    /// <summary>
    /// Gets or sets the guild ID where the activity occurred.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Gets or sets the name of the guild where the activity occurred.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the type of event that occurred.
    /// Examples: "MemberJoined", "MemberLeft", "MessageSent", "RatWatchCreated", "RatWatchVotingStarted"
    /// </summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the event occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the username associated with the event (optional).
    /// Used for events like Rat Watch where a user is involved.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets additional details about the event (optional).
    /// For Rat Watch: verdict result ("Guilty" or "Not Guilty"), etc.
    /// </summary>
    public string? Details { get; set; }
}
