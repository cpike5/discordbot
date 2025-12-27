using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Models;

/// <summary>
/// State data for scheduled message list select menu interactions.
/// </summary>
public class ScheduleListState
{
    /// <summary>
    /// Gets or sets the list of scheduled messages displayed in the select menu.
    /// </summary>
    public List<ScheduledMessageDto> Messages { get; set; } = new();

    /// <summary>
    /// Gets or sets the guild ID for which the messages are listed.
    /// </summary>
    public ulong GuildId { get; set; }
}
