using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Services.Realtime.Events;

/// <summary>
/// Dual-publish of <see cref="Services.DashboardNotifier.BroadcastBotStatusAsync"/>
/// ("BotStatusUpdated" sent to <c>Clients.All</c>). Carries the same <see cref="BotStatusDto"/>
/// the hub sends. Distinct from <see cref="BotStatusBroadcastEvent"/>, which carries the
/// different DTO used by <see cref="Services.DashboardUpdateService"/> for the same event name.
/// </summary>
public sealed record BotStatusUpdatedEvent : IDashboardEvent
{
    public required BotStatusDto Status { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Dual-publish of <see cref="Services.DashboardUpdateService.BroadcastBotStatusAsync"/>
/// ("BotStatusUpdated" sent to <c>Clients.All</c>). This is the update actually driven by the
/// 30-second <c>BotStatusBroadcastService</c> poller.
/// </summary>
public sealed record BotStatusBroadcastEvent : IDashboardEvent
{
    public required BotStatusUpdateDto Status { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Dual-publish of <see cref="Services.DashboardUpdateService.BroadcastCommandExecutedAsync"/>
/// ("CommandExecuted" sent to <c>Clients.All</c>). <see cref="CommandExecutedUpdateDto.GuildId"/>
/// is null for commands executed in a DM, so this does not derive from
/// <see cref="GuildScopedEvent"/>; filter on <c>Update.GuildId</c> where needed.
/// </summary>
public sealed record CommandExecutedEvent : IDashboardEvent
{
    public required CommandExecutedUpdateDto Update { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Dual-publish of <see cref="Services.DashboardUpdateService.BroadcastGuildActivityAsync"/> and
/// <see cref="Services.DashboardUpdateService.BroadcastGuildActivityToGuildAsync"/> ("GuildActivity",
/// sent to <c>Clients.All</c> and/or the <c>guild-{guildId}</c> group). Subscribe without the guild
/// overload to see every guild's activity (mirrors the "All" send); subscribe with
/// <see cref="IDashboardEventBus.Subscribe{TEvent}(ulong, Func{TEvent, CancellationToken, Task})"/>
/// to mirror the guild-group send.
/// </summary>
public sealed record GuildActivityEvent : GuildScopedEvent
{
    public required GuildActivityUpdateDto Update { get; init; }
}

/// <summary>
/// Dual-publish of <see cref="Services.DashboardUpdateService.BroadcastStatsUpdateAsync"/>
/// ("StatsUpdated" sent to <c>Clients.All</c>).
/// </summary>
public sealed record StatsUpdatedEvent : IDashboardEvent
{
    public required DashboardStatsDto Stats { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Dual-publish of <see cref="Services.DashboardNotifier.SendGuildUpdateAsync"/>: a
/// guild-targeted event whose name and payload shape are decided by the caller rather than
/// fixed ahead of time. No current caller uses this generic path (every concrete update goes
/// through a typed event above); kept for parity with the hub send it mirrors.
/// </summary>
public sealed record DashboardGuildUpdateEvent : GuildScopedEvent
{
    public required string EventName { get; init; }

    public required object Data { get; init; }
}

/// <summary>
/// Dual-publish of <see cref="Services.DashboardNotifier.BroadcastToAllAsync"/>: an
/// all-clients event whose name and payload shape are decided by the caller. No current caller
/// uses this generic path; kept for parity with the hub send it mirrors.
/// </summary>
public sealed record DashboardBroadcastEvent : IDashboardEvent
{
    public required string EventName { get; init; }

    public required object Data { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
