using DiscordBot.Core.DTOs;

namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Posts moderation outcomes to a guild's mod-log channel, when one is configured.
/// </summary>
/// <remarks>
/// The feed is a Discord-side view of what the portal already records; it never writes an audit row
/// and never replaces one. Implementations never throw: a missing channel, a permission refusal or a
/// Discord outage is logged and swallowed, because a moderator's <c>/warn</c> must not fail over a
/// feed. Callers run it on <see cref="IBackgroundTaskRunner"/> so the reply is not held up either.
/// </remarks>
public interface IModLogNotifier
{
    /// <summary>
    /// Posts a newly created moderation case, if the guild has a mod-log channel and
    /// <see cref="Enums.ModLogEventKinds.Cases"/> is enabled.
    /// </summary>
    /// <param name="moderationCase">The case as returned by <see cref="IModerationService.CreateCaseAsync"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The id of the message posted, or null when nothing was posted.</returns>
    Task<ulong?> CaseCreatedAsync(ModerationCaseDto moderationCase, CancellationToken ct = default);
}
