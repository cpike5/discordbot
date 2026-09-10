using DiscordBot.Bot.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Singleton implementation of <see cref="IBotUptimeProvider"/>. <see cref="StartTime"/> is
/// fixed once, at construction, which happens on first resolution from the DI container
/// (effectively process start, matching the semantics of the static fields it replaces).
/// </summary>
public class BotUptimeProvider : IBotUptimeProvider
{
    public DateTime StartTime { get; } = DateTime.UtcNow;

    public TimeSpan Uptime => DateTime.UtcNow - StartTime;
}
