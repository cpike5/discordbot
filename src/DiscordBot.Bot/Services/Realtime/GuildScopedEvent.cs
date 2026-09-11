namespace DiscordBot.Bot.Services.Realtime;

/// <summary>
/// Base for a dashboard event that is scoped to a single guild, mirroring the
/// <c>guild-{guildId}</c> / <c>guild-audio-{guildId}</c> SignalR groups. Deriving from
/// this lets a subscriber filter by guild with <see cref="IDashboardEventBus.Subscribe{TEvent}(ulong, Func{TEvent, CancellationToken, Task})"/>
/// instead of checking <see cref="GuildId"/> by hand.
/// </summary>
public abstract record GuildScopedEvent : IDashboardEvent
{
    /// <summary>
    /// Gets the guild this event applies to.
    /// </summary>
    public required ulong GuildId { get; init; }

    /// <inheritdoc/>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Base for a dashboard event delivered to one user, mirroring <c>Clients.User(userId)</c>
/// delivery for notifications.
/// </summary>
public abstract record UserScopedEvent : IDashboardEvent
{
    /// <summary>
    /// Gets the ASP.NET Identity user ID the event was delivered to.
    /// </summary>
    public required string UserId { get; init; }

    /// <inheritdoc/>
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
