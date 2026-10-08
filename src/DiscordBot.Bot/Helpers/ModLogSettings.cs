using Discord.WebSocket;
using DiscordBot.Core.Enums;

namespace DiscordBot.Bot.Helpers;

/// <summary>
/// Validation the mod-log settings get on both ways in: the Moderation Settings page and the
/// moderation-config API. One place, so the two cannot accept different things.
/// </summary>
public static class ModLogSettings
{
    /// <summary>Message for a channel id that does not parse.</summary>
    public const string InvalidChannelMessage = "Choose a channel from the list.";

    /// <summary>Message for a channel the bot cannot see in this guild.</summary>
    public const string UnknownChannelMessage = "That channel is not a text channel the bot can see in this server.";

    /// <summary>Message for an event selection outside the known kinds.</summary>
    public const string InvalidEventsMessage = "Choose which events to post from the options shown.";

    /// <summary>
    /// Parses a submitted channel value. Empty means "no channel" (the feed is off).
    /// </summary>
    /// <returns>Null for no error; otherwise the message to show on the field.</returns>
    public static string? TryParseChannel(string? submitted, out ulong? channelId)
    {
        channelId = null;
        if (string.IsNullOrWhiteSpace(submitted))
        {
            return null;
        }

        if (!ulong.TryParse(submitted.Trim(), out var parsed) || parsed == 0)
        {
            return InvalidChannelMessage;
        }

        channelId = parsed;
        return null;
    }

    /// <summary>
    /// Checks that the channel is a text channel of the guild. When the bot cannot see the guild
    /// (offline mode, or not connected yet) the check is skipped: the page would otherwise refuse
    /// every save while the bot is down, and the notifier copes with a wrong channel at post time.
    /// </summary>
    public static string? ValidateChannel(DiscordSocketClient client, ulong guildId, ulong? channelId)
    {
        if (channelId is null)
        {
            return null;
        }

        var guild = client.GetGuild(guildId);
        if (guild is null)
        {
            return null;
        }

        return guild.GetTextChannel(channelId.Value) is null ? UnknownChannelMessage : null;
    }

    /// <summary>Checks that an events value only has known bits set.</summary>
    public static string? ValidateEvents(int events)
        => events < 0 || (events & ~(int)ModLogEventKinds.All) != 0 ? InvalidEventsMessage : null;
}
