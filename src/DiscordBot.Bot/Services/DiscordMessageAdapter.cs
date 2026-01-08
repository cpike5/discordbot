using Discord;
using Discord.WebSocket;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Adapter that wraps Discord.NET's SocketMessage into an IDiscordMessage interface.
/// This enables testing by abstracting away Discord.NET's non-virtual properties.
/// </summary>
public class DiscordMessageAdapter : IDiscordMessage
{
    private readonly SocketMessage _message;

    public DiscordMessageAdapter(SocketMessage message)
    {
        _message = message ?? throw new ArgumentNullException(nameof(message));
    }

    /// <inheritdoc/>
    public ulong Id => _message.Id;

    /// <inheritdoc/>
    public ulong AuthorId => _message.Author.Id;

    /// <inheritdoc/>
    public bool IsAuthorBot => _message.Author.IsBot;

    /// <inheritdoc/>
    public bool IsUserMessage => _message is SocketUserMessage;

    /// <inheritdoc/>
    public ulong ChannelId => _message.Channel.Id;

    /// <inheritdoc/>
    public string? ChannelName => (_message.Channel as SocketGuildChannel)?.Name;

    /// <inheritdoc/>
    public bool IsDirectMessage => _message.Channel is IDMChannel;

    /// <inheritdoc/>
    public ulong? GuildId => (_message.Channel as SocketGuildChannel)?.Guild.Id;

    /// <inheritdoc/>
    public string Content => _message.Content ?? string.Empty;

    /// <inheritdoc/>
    public DateTimeOffset Timestamp => _message.Timestamp;

    /// <inheritdoc/>
    public bool HasAttachments => _message.Attachments.Any();

    /// <inheritdoc/>
    public bool HasEmbeds => _message.Embeds.Any();

    /// <inheritdoc/>
    public ulong? ReplyToMessageId => _message.Reference?.MessageId.GetValueOrDefault();
}
