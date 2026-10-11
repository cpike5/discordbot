using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DiscordBot.Bot.Metrics;

/// <summary>
/// Outcome metrics for features that run outside a slash command, so the command metrics never see
/// them: auto-moderation, reminder delivery, scheduled message execution and audio playback.
/// </summary>
/// <remarks>
/// Call sites take this as an optional constructor parameter (or resolve it from the service
/// provider they already hold), so an existing test that does not pass one keeps compiling and
/// simply records nothing.
/// </remarks>
public sealed class FeatureMetrics : IDisposable
{
    public const string MeterName = "DiscordBot.Features";

    private readonly Meter _meter;
    private readonly Counter<long> _automodDetections;
    private readonly Counter<long> _automodActions;
    private readonly Counter<long> _reminderDeliveries;
    private readonly Counter<long> _scheduledMessageExecutions;
    private readonly Counter<long> _audioPlaybacks;
    private readonly Histogram<double> _audioPlaybackDuration;

    public FeatureMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        _automodDetections = _meter.CreateCounter<long>(
            name: "discordbot.automod.detections",
            unit: "{detections}",
            description: "Auto-moderation detections, by rule type and severity");

        _automodActions = _meter.CreateCounter<long>(
            name: "discordbot.automod.actions",
            unit: "{actions}",
            description: "Automatic moderation actions taken, by action and status");

        _reminderDeliveries = _meter.CreateCounter<long>(
            name: "discordbot.reminders.deliveries",
            unit: "{deliveries}",
            description: "Reminder delivery attempts, by status");

        _scheduledMessageExecutions = _meter.CreateCounter<long>(
            name: "discordbot.scheduled_messages.executions",
            unit: "{executions}",
            description: "Scheduled message executions, by status");

        _audioPlaybacks = _meter.CreateCounter<long>(
            name: "discordbot.audio.playbacks",
            unit: "{playbacks}",
            description: "Audio playbacks in voice channels, by source and status");

        _audioPlaybackDuration = _meter.CreateHistogram<double>(
            name: "discordbot.audio.playback.duration",
            unit: "s",
            description: "Wall-clock duration of an audio playback");
    }

    /// <summary>Records an auto-moderation detection.</summary>
    public void RecordAutomodDetection(string ruleType, string severity)
    {
        _automodDetections.Add(1, new TagList
        {
            { "rule_type", ruleType },
            { "severity", severity }
        });
    }

    /// <summary>Records an automatic moderation action.</summary>
    /// <param name="action">The action, e.g. "Delete", "Mute".</param>
    /// <param name="status">"success" or "failure".</param>
    public void RecordAutomodAction(string action, string status)
    {
        _automodActions.Add(1, new TagList
        {
            { "action", action },
            { "status", status }
        });
    }

    /// <summary>Records a reminder delivery attempt.</summary>
    /// <param name="status">"delivered", "dm_disabled", "user_not_found" or "failed".</param>
    public void RecordReminderDelivery(string status)
        => _reminderDeliveries.Add(1, new KeyValuePair<string, object?>("status", status));

    /// <summary>Records a scheduled message execution.</summary>
    /// <param name="status">"success", "failure", "timeout" or "error".</param>
    public void RecordScheduledMessageExecution(string status)
        => _scheduledMessageExecutions.Add(1, new KeyValuePair<string, object?>("status", status));

    /// <summary>Records one audio playback.</summary>
    /// <param name="source">"soundboard" or "tts". VOX has its own meter.</param>
    /// <param name="status">"success", "cancelled" or "failure".</param>
    /// <param name="elapsed">How long the playback ran.</param>
    public void RecordAudioPlayback(string source, string status, TimeSpan elapsed)
    {
        var tags = new TagList
        {
            { "source", source },
            { "status", status }
        };

        _audioPlaybacks.Add(1, tags);
        _audioPlaybackDuration.Record(elapsed.TotalSeconds, tags);
    }

    public void Dispose() => _meter.Dispose();
}
