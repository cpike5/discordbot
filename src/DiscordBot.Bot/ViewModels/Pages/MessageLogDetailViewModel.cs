using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.ViewModels.Pages;

/// <summary>
/// View model for displaying detailed information about a single message log entry.
/// </summary>
public class MessageLogDetailViewModel
{
    public MessageLogDto Message { get; set; } = null!;
}
