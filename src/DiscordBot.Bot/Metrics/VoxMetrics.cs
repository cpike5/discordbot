using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DiscordBot.Bot.Metrics;

/// <summary>
/// Defines metrics for VOX (Half-Life concatenated audio) system.
/// Tracks command execution, clip usage, performance, and errors.
/// Uses System.Diagnostics.Metrics which is collected by OpenTelemetry.
/// </summary>
public sealed class VoxMetrics : IDisposable
{
    public const string MeterName = "DiscordBot.Vox";

    private readonly Meter _meter;

    // Counters
    private readonly Counter<long> _commandCounter;
    private readonly Counter<long> _clipsPlayedCounter;
    private readonly Counter<long> _wordsMatchedCounter;
    private readonly Counter<long> _wordsSkippedCounter;
    private readonly Counter<long> _errorCounter;

    // Histograms
    private readonly Histogram<double> _commandDuration;
    private readonly Histogram<double> _concatenationDuration;
    private readonly Histogram<long> _messageWords;
    private readonly Histogram<double> _matchPercentage;
    private readonly Histogram<long> _audioBytes;

    public VoxMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);

        // Initialize counters
        _commandCounter = _meter.CreateCounter<long>(
            name: "discordbot.vox.commands.total",
            unit: "{commands}",
            description: "Total number of VOX commands executed");

        _clipsPlayedCounter = _meter.CreateCounter<long>(
            name: "discordbot.vox.clips.played",
            unit: "{clips}",
            description: "Total number of individual clips played");

        _wordsMatchedCounter = _meter.CreateCounter<long>(
            name: "discordbot.vox.words.matched",
            unit: "{words}",
            description: "Total number of words that matched clips");

        _wordsSkippedCounter = _meter.CreateCounter<long>(
            name: "discordbot.vox.words.skipped",
            unit: "{words}",
            description: "Total number of words without matching clips");

        _errorCounter = _meter.CreateCounter<long>(
            name: "discordbot.vox.errors",
            unit: "{errors}",
            description: "Total number of VOX errors by type");

        // Initialize histograms
        _commandDuration = _meter.CreateHistogram<double>(
            name: "discordbot.vox.command.duration",
            unit: "ms",
            description: "Total command execution duration");

        _concatenationDuration = _meter.CreateHistogram<double>(
            name: "discordbot.vox.concatenation.duration",
            unit: "ms",
            description: "FFmpeg concatenation processing time");

        _messageWords = _meter.CreateHistogram<long>(
            name: "discordbot.vox.message.words",
            unit: "{words}",
            description: "Number of words per message");

        _matchPercentage = _meter.CreateHistogram<double>(
            name: "discordbot.vox.match.percentage",
            unit: "%",
            description: "Percentage of words matched");

        _audioBytes = _meter.CreateHistogram<long>(
            name: "discordbot.vox.audio.bytes",
            unit: "By",
            description: "Output PCM audio size in bytes");
    }

    /// <summary>
    /// Records a VOX command execution with duration and success status.
    /// </summary>
    /// <param name="group">The clip group (VOX, FVOX, HGRUNT).</param>
    /// <param name="source">The command source (slash_command, portal).</param>
    /// <param name="success">Whether the command executed successfully.</param>
    /// <param name="durationMs">The duration of command execution in milliseconds.</param>
    public void RecordCommandExecution(string group, string source, bool success, double durationMs)
    {
        var tags = new TagList
        {
            { "group", group.ToLowerInvariant() },
            { "source", source },
            { "status", success ? "success" : "failure" }
        };
        _commandCounter.Add(1, tags);
        _commandDuration.Record(durationMs, tags);
    }

    /// <summary>
    /// Records the number of clips played in a message.
    /// </summary>
    /// <param name="group">The clip group (VOX, FVOX, HGRUNT).</param>
    /// <param name="clipCount">The number of clips played.</param>
    public void RecordClipsPlayed(string group, int clipCount)
    {
        _clipsPlayedCounter.Add(clipCount, new TagList { { "group", group.ToLowerInvariant() } });
    }

    /// <summary>
    /// Records word matching statistics (matched, skipped, total).
    /// Also calculates and records match percentage.
    /// </summary>
    /// <param name="group">The clip group (VOX, FVOX, HGRUNT).</param>
    /// <param name="matchedCount">Number of words with matching clips.</param>
    /// <param name="skippedCount">Number of words without clips.</param>
    /// <param name="totalWords">Total words in the message.</param>
    public void RecordWordStats(string group, int matchedCount, int skippedCount, int totalWords)
    {
        var groupTag = new TagList { { "group", group.ToLowerInvariant() } };

        _wordsMatchedCounter.Add(matchedCount, groupTag);
        _wordsSkippedCounter.Add(skippedCount, groupTag);
        _messageWords.Record(totalWords, groupTag);

        if (totalWords > 0)
        {
            var matchPercentage = (matchedCount / (double)totalWords) * 100;
            _matchPercentage.Record(matchPercentage, groupTag);
        }
    }

    /// <summary>
    /// Records an error occurrence by type.
    /// </summary>
    /// <param name="group">The clip group (VOX, FVOX, HGRUNT).</param>
    /// <param name="errorType">The error type (NoClipsMatched, ConcatenationFailed, etc).</param>
    public void RecordError(string group, string errorType)
    {
        var tags = new TagList
        {
            { "group", group.ToLowerInvariant() },
            { "error_type", errorType }
        };
        _errorCounter.Add(1, tags);
    }

    /// <summary>
    /// Records concatenation performance metrics.
    /// </summary>
    /// <param name="group">The clip group (VOX, FVOX, HGRUNT).</param>
    /// <param name="durationMs">The duration of concatenation in milliseconds.</param>
    /// <param name="audioBytes">The output PCM file size in bytes.</param>
    public void RecordConcatenation(string group, double durationMs, long audioBytes)
    {
        var groupTag = new TagList { { "group", group.ToLowerInvariant() } };
        _concatenationDuration.Record(durationMs, groupTag);
        _audioBytes.Record(audioBytes, groupTag);
    }

    public void Dispose() => _meter.Dispose();
}
