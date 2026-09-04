namespace DiscordBot.Bot.Interfaces;

/// <summary>
/// Single shared source of truth for the bot process's start time and uptime.
/// Replaces duplicate static <c>_startTime</c> fields that previously lived independently
/// in <see cref="Services.BotHostedService"/> and <see cref="Services.BotStatusBroadcaster"/>,
/// which could drift apart since each was initialized on first use of its own type.
/// </summary>
public interface IBotUptimeProvider
{
    /// <summary>
    /// The UTC time at which this provider was created (approximates process start).
    /// </summary>
    DateTime StartTime { get; }

    /// <summary>
    /// Elapsed time since <see cref="StartTime"/>.
    /// </summary>
    TimeSpan Uptime { get; }
}
