using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DiscordBot.Bot.Metrics;

/// <summary>
/// Defines metrics for Discord bot command execution and status.
/// Uses System.Diagnostics.Metrics which is collected by OpenTelemetry.
/// </summary>
public sealed class BotMetrics : IDisposable
{
    public const string MeterName = "DiscordBot.Bot";

    private readonly Meter _meter;
    private readonly Counter<long> _commandCounter;
    private readonly Histogram<double> _commandDuration;
    private readonly UpDownCounter<long> _activeCommands;
    private readonly Counter<long> _rateLimitViolations;
    private readonly Counter<long> _componentCounter;
    private readonly Histogram<double> _componentDuration;

    // Observable gauges require callbacks
    private long _activeGuildCount;
    private long _uniqueUserCount;

    public BotMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _commandCounter = _meter.CreateCounter<long>(
            name: "discordbot.command.count",
            unit: "{commands}",
            description: "Total number of Discord commands executed");

        _commandDuration = _meter.CreateHistogram<double>(
            name: "discordbot.command.duration",
            unit: "ms",
            description: "Duration of command execution in milliseconds");

        _activeCommands = _meter.CreateUpDownCounter<long>(
            name: "discordbot.command.active",
            unit: "{commands}",
            description: "Number of currently executing commands");

        _rateLimitViolations = _meter.CreateCounter<long>(
            name: "discordbot.ratelimit.violations",
            unit: "{violations}",
            description: "Number of rate limit violations");

        _componentCounter = _meter.CreateCounter<long>(
            name: "discordbot.component.count",
            unit: "{interactions}",
            description: "Total number of component interactions");

        _componentDuration = _meter.CreateHistogram<double>(
            name: "discordbot.component.duration",
            unit: "ms",
            description: "Duration of component interaction handling");

        _meter.CreateObservableGauge(
            name: "discordbot.guilds.active",
            observeValue: () => _activeGuildCount,
            unit: "{guilds}",
            description: "Number of guilds the bot is connected to");

        _meter.CreateObservableGauge(
            name: "discordbot.users.unique",
            observeValue: () => _uniqueUserCount,
            unit: "{users}",
            description: "Estimated unique users in the last 24 hours");
    }

    /// <summary>
    /// Records a command execution with duration and success status.
    /// </summary>
    /// <param name="commandName">The name of the command executed.</param>
    /// <param name="success">Whether the command executed successfully.</param>
    /// <param name="durationMs">The duration of command execution in milliseconds.</param>
    /// <param name="guildId">Optional guild ID where the command was executed.</param>
    public void RecordCommandExecution(
        string commandName,
        bool success,
        double durationMs,
        ulong? guildId = null)
    {
        var tags = new TagList
        {
            { "command", commandName },
            { "status", success ? "success" : "failure" }
        };

        // Note: Removed guild_id label to avoid cardinality explosion as per updated requirements
        // Original implementation plan included it, but we're following the user's guidance to remove it

        _commandCounter.Add(1, tags);
        _commandDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Increments the active command counter for a specific command.
    /// </summary>
    /// <param name="commandName">The name of the command being executed.</param>
    public void IncrementActiveCommands(string commandName)
    {
        _activeCommands.Add(1, new TagList { { "command", commandName } });
    }

    /// <summary>
    /// Decrements the active command counter for a specific command.
    /// </summary>
    /// <param name="commandName">The name of the command that finished executing.</param>
    public void DecrementActiveCommands(string commandName)
    {
        _activeCommands.Add(-1, new TagList { { "command", commandName } });
    }

    /// <summary>
    /// Records a rate limit violation.
    /// </summary>
    /// <param name="commandName">The command that triggered the rate limit.</param>
    /// <param name="target">The rate limit target (user, guild, global).</param>
    public void RecordRateLimitViolation(string commandName, string target)
    {
        _rateLimitViolations.Add(1, new TagList
        {
            { "command", commandName },
            { "target", target }
        });
    }

    /// <summary>
    /// Records a component interaction (button, select menu, modal) with duration and success status.
    /// </summary>
    /// <param name="componentType">The type of component (button, select_menu, modal).</param>
    /// <param name="success">Whether the interaction was handled successfully.</param>
    /// <param name="durationMs">The duration of interaction handling in milliseconds.</param>
    public void RecordComponentInteraction(
        string componentType,
        bool success,
        double durationMs)
    {
        var tags = new TagList
        {
            { "component_type", componentType },
            { "status", success ? "success" : "failure" }
        };

        _componentCounter.Add(1, tags);
        _componentDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Updates the active guild count metric.
    /// </summary>
    /// <param name="count">The current number of guilds the bot is connected to.</param>
    public void UpdateActiveGuildCount(long count) => _activeGuildCount = count;

    /// <summary>
    /// Updates the unique user count metric.
    /// </summary>
    /// <param name="count">The estimated number of unique users.</param>
    public void UpdateUniqueUserCount(long count) => _uniqueUserCount = count;

    public void Dispose() => _meter.Dispose();
}
