using DiscordBot.Bot.ViewModels.Components.Enums;

namespace DiscordBot.Bot.ViewModels.Components;

/// <summary>
/// View model representing a single server in the Connected Servers widget.
/// </summary>
public record ConnectedServerItemViewModel
{
    /// <summary>
    /// Gets the Discord server ID (snowflake).
    /// </summary>
    public ulong Id { get; init; }

    /// <summary>
    /// Gets the server name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the server icon URL. Null if the server has no icon.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// Gets the initials to display in the avatar when no icon is available.
    /// Example: "GC" for "Gaming Community"
    /// </summary>
    public string Initials { get; init; } = string.Empty;

    /// <summary>
    /// Gets the gradient CSS classes for the avatar background.
    /// Example: "from-purple-500 to-pink-500"
    /// </summary>
    public string AvatarGradient { get; init; } = string.Empty;

    /// <summary>
    /// Gets the number of members in the server.
    /// </summary>
    public int MemberCount { get; init; }

    /// <summary>
    /// Gets the connection status of the server (Online, Idle, or Offline).
    /// </summary>
    public ServerConnectionStatus Status { get; init; }

    /// <summary>
    /// Gets the number of commands executed in this server today.
    /// </summary>
    public int CommandsToday { get; init; }

    /// <summary>
    /// Gets the URL to the server detail page.
    /// Example: "/Servers/123456789"
    /// </summary>
    public string DetailUrl { get; init; } = string.Empty;
}
