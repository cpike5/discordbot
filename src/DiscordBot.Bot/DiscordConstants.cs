namespace DiscordBot.Bot;

/// <summary>
/// Constants for Discord interaction behavior and limits.
/// </summary>
public static class DiscordConstants
{
    /// <summary>
    /// Discord's interaction response timeout threshold in milliseconds.
    /// Discord requires a response within 3000ms or the interaction will timeout.
    /// </summary>
    public const int InteractionTimeoutMs = 3000;
}
