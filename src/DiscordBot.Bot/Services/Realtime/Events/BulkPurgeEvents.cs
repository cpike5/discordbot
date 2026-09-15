using DiscordBot.Core.DTOs;

namespace DiscordBot.Bot.Services.Realtime.Events;

/// <summary>
/// Dual-publish of <see cref="Services.BulkPurgeService"/>'s progress broadcast
/// ("BulkPurgeProgress", sent to the <c>bulk-purge</c> group). Not guild-scoped: a purge can
/// span every guild, mirroring the group's own scope.
/// </summary>
public sealed record BulkPurgeProgressEvent : IDashboardEvent
{
    public required BulkPurgeProgressDto Progress { get; init; }

    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}
