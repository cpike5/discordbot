using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DiscordBot.Bot.Metrics;

/// <summary>
/// Defines business-level metrics for Discord bot operations.
/// Tracks guild membership changes, active usage, and feature adoption.
/// </summary>
public sealed class BusinessMetrics : IDisposable
{
    public const string MeterName = "DiscordBot.Business";

    private readonly Meter _meter;
    private readonly Counter<long> _guildJoinCounter;
    private readonly Counter<long> _guildLeaveCounter;
    private readonly Counter<long> _featureUsageCounter;

    // Observable gauge callbacks
    private long _guildsJoinedToday;
    private long _guildsLeftToday;
    private long _activeGuildsDaily;
    private long _activeUsers7d;
    private long _commandsToday;

    public BusinessMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _guildJoinCounter = _meter.CreateCounter<long>(
            name: "discordbot.business.guild.join",
            unit: "{events}",
            description: "Total number of guild join events");

        _guildLeaveCounter = _meter.CreateCounter<long>(
            name: "discordbot.business.guild.leave",
            unit: "{events}",
            description: "Total number of guild leave events");

        _featureUsageCounter = _meter.CreateCounter<long>(
            name: "discordbot.business.feature.usage",
            unit: "{events}",
            description: "Feature usage tracking");

        _meter.CreateObservableGauge(
            name: "discordbot.business.guilds.joined_today",
            observeValue: () => _guildsJoinedToday,
            unit: "{guilds}",
            description: "Number of new guilds joined today");

        _meter.CreateObservableGauge(
            name: "discordbot.business.guilds.left_today",
            observeValue: () => _guildsLeftToday,
            unit: "{guilds}",
            description: "Number of guilds left today");

        _meter.CreateObservableGauge(
            name: "discordbot.business.guilds.active_daily",
            observeValue: () => _activeGuildsDaily,
            unit: "{guilds}",
            description: "Number of guilds with command activity today");

        _meter.CreateObservableGauge(
            name: "discordbot.business.users.active_7d",
            observeValue: () => _activeUsers7d,
            unit: "{users}",
            description: "Number of unique active users in the last 7 days");

        _meter.CreateObservableGauge(
            name: "discordbot.business.commands.today",
            observeValue: () => _commandsToday,
            unit: "{commands}",
            description: "Total commands executed today");
    }

    /// <summary>
    /// Records a guild join event.
    /// </summary>
    public void RecordGuildJoin()
    {
        _guildJoinCounter.Add(1);
    }

    /// <summary>
    /// Records a guild leave event.
    /// </summary>
    public void RecordGuildLeave()
    {
        _guildLeaveCounter.Add(1);
    }

    /// <summary>
    /// Records a feature usage event.
    /// </summary>
    /// <param name="featureName">The name of the feature being used.</param>
    public void RecordFeatureUsage(string featureName)
    {
        _featureUsageCounter.Add(1, new TagList { { "feature", featureName } });
    }

    /// <summary>
    /// Updates the number of guilds joined today.
    /// </summary>
    /// <param name="count">The count of guilds joined today.</param>
    public void UpdateGuildsJoinedToday(long count) => _guildsJoinedToday = count;

    /// <summary>
    /// Updates the number of guilds left today.
    /// </summary>
    /// <param name="count">The count of guilds left today.</param>
    public void UpdateGuildsLeftToday(long count) => _guildsLeftToday = count;

    /// <summary>
    /// Updates the number of active guilds daily.
    /// </summary>
    /// <param name="count">The count of guilds with command activity today.</param>
    public void UpdateActiveGuildsDaily(long count) => _activeGuildsDaily = count;

    /// <summary>
    /// Updates the number of active users in the last 7 days.
    /// </summary>
    /// <param name="count">The count of unique users active in the last 7 days.</param>
    public void UpdateActiveUsers7d(long count) => _activeUsers7d = count;

    /// <summary>
    /// Updates the number of commands executed today.
    /// </summary>
    /// <param name="count">The count of commands executed today.</param>
    public void UpdateCommandsToday(long count) => _commandsToday = count;

    public void Dispose() => _meter.Dispose();
}
