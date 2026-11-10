using cpike.Agent;

namespace DiscordBot.Infrastructure.Services.LLM.Tooling;

/// <summary>
/// Who and where an assistant run is for, as Discord sees it: the caller, the guild, channel and
/// message, and the DM assistant's active guild. A live view over the run's
/// <see cref="AgentRunScope"/>, scoped like it, so a tool reads the caller from here and never from
/// its arguments, and a value a tool sets (the active guild, a cleared conversation) is what the
/// pipeline reads back after the run.
/// </summary>
public sealed class DiscordRunContext(AgentRunScope scope)
{
    /// <summary>The scope item keys. Prefixed so they never collide with the framework's own.</summary>
    public static class Keys
    {
        public const string UserId = "discord.userId";
        public const string GuildId = "discord.guildId";
        public const string ChannelId = "discord.channelId";
        public const string MessageId = "discord.messageId";
        public const string ActiveGuildId = "discord.activeGuildId";
        public const string ConversationCleared = "discord.conversationCleared";
    }

    /// <summary>The Discord user the run answers.</summary>
    public ulong UserId => Read(Keys.UserId);

    /// <summary>The guild the message came from; 0 in a DM.</summary>
    public ulong GuildId => Read(Keys.GuildId);

    /// <summary>The channel the message came from; 0 in a DM.</summary>
    public ulong ChannelId => Read(Keys.ChannelId);

    /// <summary>The message being answered; 0 when there is none.</summary>
    public ulong MessageId => Read(Keys.MessageId);

    /// <summary>Whether the caller may change data (the framework's <see cref="AgentRunScope.CanEditData"/>).</summary>
    public bool CanMutate => scope.CanEditData;

    /// <summary>
    /// The guild the DM assistant's guild-scoped tools act on, chosen with <c>set_active_guild</c>;
    /// null until one is chosen.
    /// </summary>
    public ulong? ActiveGuildId
    {
        get => scope.Items.TryGetValue(Keys.ActiveGuildId, out var value) && value is ulong id ? id : null;
        set
        {
            if (value.HasValue)
            {
                scope.Items[Keys.ActiveGuildId] = value.Value;
            }
            else
            {
                scope.Items.Remove(Keys.ActiveGuildId);
            }
        }
    }

    /// <summary>Set by <c>clear_conversation</c>, so the DM assistant does not re-save the turn it just cleared.</summary>
    public bool ConversationCleared
    {
        get => scope.Items.TryGetValue(Keys.ConversationCleared, out var value) && value is true;
        set => scope.Items[Keys.ConversationCleared] = value;
    }

    /// <summary>The scope seed for a run with <paramref name="identity"/>.</summary>
    public static TurnScopeSeed Seed(DiscordRunIdentity identity, string mode)
    {
        var items = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [Keys.UserId] = identity.UserId,
            [Keys.GuildId] = identity.GuildId,
            [Keys.ChannelId] = identity.ChannelId,
            [Keys.MessageId] = identity.MessageId,
        };
        if (identity.ActiveGuildId.HasValue)
        {
            items[Keys.ActiveGuildId] = identity.ActiveGuildId.Value;
        }

        return new TurnScopeSeed
        {
            UserKey = identity.UserId.ToString(),
            Mode = mode,
            RawMode = mode,
            CanEditData = identity.CanMutate,
            Items = items,
        };
    }

    private ulong Read(string key) => scope.Items.TryGetValue(key, out var value) && value is ulong id ? id : 0;
}

/// <summary>The Discord facts a run is seeded with; see <see cref="DiscordRunContext"/>.</summary>
public sealed record DiscordRunIdentity(
    ulong UserId,
    ulong GuildId = 0,
    ulong ChannelId = 0,
    ulong MessageId = 0,
    bool CanMutate = false,
    ulong? ActiveGuildId = null);
