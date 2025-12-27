namespace DiscordBot.Bot.Models;

/// <summary>
/// State data for scheduled message delete confirmation interactions.
/// </summary>
public class ScheduleDeleteState
{
    /// <summary>
    /// Gets or sets the ID of the scheduled message to be deleted.
    /// </summary>
    public Guid MessageId { get; set; }

    /// <summary>
    /// Gets or sets the title of the scheduled message for display in confirmation.
    /// </summary>
    public string MessageTitle { get; set; } = string.Empty;
}
