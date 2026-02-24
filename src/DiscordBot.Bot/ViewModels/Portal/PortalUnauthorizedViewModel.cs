namespace DiscordBot.Bot.ViewModels.Portal;

/// <summary>
/// ViewModel for the shared portal unauthorized view partial.
/// Displayed when a user is authenticated but not a guild member.
/// </summary>
public class PortalUnauthorizedViewModel
{
    /// <summary>
    /// Gets or sets the guild display name.
    /// </summary>
    public string GuildName { get; set; } = string.Empty;
}
