namespace DiscordBot.Bot.ViewModels.Components.Enums;

/// <summary>
/// What a Discord server has been doing, as the dashboard's Connected Servers card shows it.
/// This is activity from the command log, not the bot's connection: the card header says
/// separately when the bot is offline.
/// </summary>
public enum ServerActivityStatus
{
    /// <summary>
    /// Guild is active and ran at least one command today.
    /// </summary>
    ActiveToday,

    /// <summary>
    /// Guild is active but ran no commands today.
    /// </summary>
    Quiet,

    /// <summary>
    /// The bot is no longer in this guild.
    /// </summary>
    Left
}

/// <summary>
/// The words and badge style for a <see cref="ServerActivityStatus"/>. The one place the labels
/// live: the page renders them, and the refresh JSON carries them to dashboard-actions.js.
/// </summary>
public static class ServerActivityDisplay
{
    /// <summary>
    /// Gets the label, badge class and tooltip for a status.
    /// </summary>
    public static (string Label, string BadgeClass, string Title) For(ServerActivityStatus status) => status switch
    {
        ServerActivityStatus.ActiveToday => ("Active today", "badge-success", "Ran commands today"),
        ServerActivityStatus.Quiet => ("Quiet", "badge-gray", "In this server, no commands today"),
        _ => ("Left", "badge-gray", "The bot is no longer in this server")
    };
}
