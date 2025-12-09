namespace DiscordBot.Bot.Models;

/// <summary>
/// State data for shutdown confirmation interactions.
/// </summary>
public class ShutdownState
{
    /// <summary>
    /// The timestamp when the shutdown was requested.
    /// </summary>
    public DateTime RequestedAt { get; set; }
}
