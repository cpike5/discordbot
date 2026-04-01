# VOX System Telemetry Specification

**Version:** 1.0
**Date:** 2026-02-03
**Status:** Design
**Target Framework:** .NET 8, System.Diagnostics.Metrics, OpenTelemetry

---

## Table of Contents

1. [Overview](#overview)
2. [Current State Analysis](#current-state-analysis)
3. [Structured Logging Enhancements](#structured-logging-enhancements)
4. [OpenTelemetry Metrics](#opentelemetry-metrics)
5. [Distributed Tracing](#distributed-tracing)
6. [Kibana Dashboard Recommendations](#kibana-dashboard-recommendations)
7. [Implementation Guide](#implementation-guide)
8. [Related Documentation](#related-documentation)

---

## Overview

### Purpose

This specification defines comprehensive telemetry for the VOX system (Half-Life style concatenated audio announcements). The telemetry strategy encompasses:

- **Structured Logging** - Consistent log events with rich context for debugging and auditing
- **OpenTelemetry Metrics** - Performance counters and histograms for operational visibility
- **Distributed Tracing** - End-to-end request spans for performance analysis
- **Dashboard Recommendations** - Kibana visualizations for monitoring and analytics

### Goals

1. **Performance Visibility** - Track concatenation time, playback latency, and overall command duration
2. **Usage Analytics** - Understand which clip groups are popular, match rates, and feature adoption
3. **Error Categorization** - Distinguish between user errors (no clips found) and system errors (FFmpeg failures)
4. **Operational Insights** - Enable dashboards for real-time monitoring and historical analysis

### Design Principles

- **Follow existing patterns** - Align with `BotMetrics.cs` and `BusinessMetrics.cs` conventions
- **Low cardinality tags** - Avoid high-cardinality dimensions (guild IDs, user IDs in metric tags)
- **Structured properties** - Use Serilog structured properties for queryable logs
- **Minimal overhead** - Metrics should not significantly impact performance

---

## Current State Analysis

### Existing Logging

The VOX system currently logs:

| Component | Events Logged | Log Level | Properties |
|-----------|--------------|-----------|------------|
| **VoxModule** | Command start | Information | Group, Username, UserId, GuildName, GuildId, Message, Gap |
| **VoxModule** | Command success | Information | Group, GuildId, UserId, MatchedCount, SkippedCount |
| **VoxModule** | Command failure | Error | Group, Message, ErrorMessage |
| **VoxService** | Token count | Debug | TokenCount, Group |
| **VoxService** | Clip lookup | Debug | Token (per miss) |
| **VoxService** | Playback start | Information | MatchedCount, SkippedCount |
| **VoxService** | Playback cancelled | Information | GuildId |
| **VoxService** | Playback error | Error | GuildId, Exception |
| **VoxConcatenationService** | Concatenation start | Debug | ClipCount, GapMs |
| **VoxConcatenationService** | Single clip optimization | Debug | - |
| **VoxConcatenationService** | Concatenation success | Information | ClipCount, OutputPath, FileSize |
| **VoxConcatenationService** | FFmpeg failure | Error | ExitCode, Error |
| **VoxClipLibraryInitializer** | Initialization start | Information | - |
| **VoxClipLibraryInitializer** | Initialization complete | Information | VoxCount, FvoxCount, HgruntCount, ElapsedMs |

### Gaps Identified

1. **No dedicated metrics** - Command execution not tracked in `BotMetrics.cs` or `BusinessMetrics.cs`
2. **No feature usage tracking** - VOX usage not recorded via `RecordFeatureUsage()`
3. **No performance breakdowns** - Individual operation durations (tokenization, concatenation, playback) not measured
4. **No clip popularity tracking** - Cannot identify most-used clips or groups
5. **No match percentage calculation** - Success rates not computed or logged
6. **No audio size/duration metrics** - PCM output size and estimated duration not captured as structured properties
7. **No distributed tracing** - No spans for performance profiling

---

## Structured Logging Enhancements

### Log Event Naming Convention

Use `VOX_<OPERATION>_<STATE>` format for event templates:

- `VOX_COMMAND_STARTED`
- `VOX_COMMAND_COMPLETED`
- `VOX_COMMAND_FAILED`
- `VOX_PORTAL_PLAY_STARTED`
- `VOX_PORTAL_PLAY_COMPLETED`
- `VOX_CONCATENATION_COMPLETED`
- `VOX_CLIP_LIBRARY_INITIALIZED`

### Event Specifications

#### VOX_COMMAND_STARTED

**Purpose**: Record the start of a VOX slash command execution.

**Log Level**: `Information`

**Template**:
```csharp
"VOX_COMMAND_STARTED: {Group} command by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})"
```

**Required Properties**:
- `Group` (string) - `"VOX"`, `"FVOX"`, or `"HGRUNT"`
- `Username` (string) - Discord username
- `UserId` (ulong) - Discord user ID
- `GuildName` (string) - Guild display name
- `GuildId` (ulong) - Discord guild ID
- `Message` (string) - Raw input message
- `WordGapMs` (int) - Configured word gap in milliseconds

**Optional Properties**:
- `Source` (string) - `"SlashCommand"` or `"Portal"` (default: `"SlashCommand"`)

**Example**:
```csharp
_logger.LogInformation(
    "VOX_COMMAND_STARTED: {Group} command by {Username} (ID: {UserId}) in guild {GuildName} (ID: {GuildId})",
    groupName, username, userId, guildName, guildId);
```

---

#### VOX_COMMAND_COMPLETED

**Purpose**: Record successful completion of a VOX command.

**Log Level**: `Information`

**Template**:
```csharp
"VOX_COMMAND_COMPLETED: {Group} playback finished. Matched: {MatchedCount}/{TotalWords} ({MatchPercentage:F1}%), Skipped: {SkippedCount}, Duration: {DurationMs}ms"
```

**Required Properties**:
- `Group` (string) - `"VOX"`, `"FVOX"`, or `"HGRUNT"`
- `GuildId` (ulong) - Discord guild ID
- `UserId` (ulong) - Discord user ID
- `MatchedCount` (int) - Number of words with matching clips
- `SkippedCount` (int) - Number of words without clips
- `TotalWords` (int) - Total words in message
- `MatchPercentage` (double) - `(MatchedCount / TotalWords) * 100`
- `DurationMs` (double) - Total command duration in milliseconds
- `ConcatenationMs` (double) - FFmpeg concatenation duration
- `AudioBytes` (long) - Output PCM file size in bytes
- `EstimatedDurationSeconds` (double) - Expected playback duration

**Optional Properties**:
- `Source` (string) - `"SlashCommand"` or `"Portal"`

**Example**:
```csharp
_logger.LogInformation(
    "VOX_COMMAND_COMPLETED: {Group} playback finished. Matched: {MatchedCount}/{TotalWords} ({MatchPercentage:F1}%), Skipped: {SkippedCount}, Duration: {DurationMs}ms",
    groupName, matchedCount, totalWords, matchPercentage, skippedCount, durationMs);
```

---

#### VOX_COMMAND_FAILED

**Purpose**: Record failed VOX command execution.

**Log Level**: `Error` (for system errors) or `Warning` (for user errors)

**Template**:
```csharp
"VOX_COMMAND_FAILED: {Group} command failed for guild {GuildId}. Reason: {ErrorType} - {ErrorMessage}"
```

**Required Properties**:
- `Group` (string) - `"VOX"`, `"FVOX"`, or `"HGRUNT"`
- `GuildId` (ulong) - Discord guild ID
- `UserId` (ulong) - Discord user ID
- `ErrorType` (string) - Error category (see Error Types table below)
- `ErrorMessage` (string) - User-facing error message
- `DurationMs` (double) - Duration until failure

**Optional Properties**:
- `Source` (string) - `"SlashCommand"` or `"Portal"`
- `Exception` (Exception) - For system errors only

**Error Types**:

| ErrorType | Description | Log Level |
|-----------|-------------|-----------|
| `NoClipsMatched` | No words had matching clips | `Warning` |
| `EmptyMessage` | Message was null/empty | `Warning` |
| `MessageTooLong` | Message exceeded character limit | `Warning` |
| `TooManyWords` | Word count exceeded limit | `Warning` |
| `InvalidWordGap` | Word gap outside 20-200ms range | `Warning` |
| `NotConnectedToVoice` | Bot not in voice channel | `Warning` |
| `ConcatenationFailed` | FFmpeg concatenation error | `Error` |
| `PlaybackFailed` | Discord audio streaming error | `Error` |
| `UnknownError` | Unexpected exception | `Error` |

**Example**:
```csharp
_logger.LogWarning(
    "VOX_COMMAND_FAILED: {Group} command failed for guild {GuildId}. Reason: {ErrorType} - {ErrorMessage}",
    groupName, guildId, "NoClipsMatched", "No matching clips found for any words in the message.");
```

---

#### VOX_PORTAL_PLAY_STARTED

**Purpose**: Record portal-initiated playback (distinct from slash commands).

**Log Level**: `Information`

**Template**:
```csharp
"VOX_PORTAL_PLAY_STARTED: {Group} playback initiated from portal by user {UserId} in guild {GuildId}"
```

**Required Properties**:
- `Group` (string) - `"VOX"`, `"FVOX"`, or `"HGRUNT"`
- `GuildId` (ulong) - Discord guild ID
- `UserId` (ulong) - Discord user ID
- `Message` (string) - Raw input message
- `WordGapMs` (int) - Configured word gap
- `Source` (string) - Always `"Portal"`

**Example**:
```csharp
_logger.LogInformation(
    "VOX_PORTAL_PLAY_STARTED: {Group} playback initiated from portal by user {UserId} in guild {GuildId}",
    groupName, userId, guildId);
```

---

#### VOX_CONCATENATION_COMPLETED

**Purpose**: Record successful audio concatenation.

**Log Level**: `Information`

**Template**:
```csharp
"VOX_CONCATENATION_COMPLETED: Concatenated {ClipCount} clips ({AudioBytes} bytes) in {ConcatenationMs}ms"
```

**Required Properties**:
- `Group` (string) - `"VOX"`, `"FVOX"`, or `"HGRUNT"`
- `ClipCount` (int) - Number of clips concatenated
- `WordGapMs` (int) - Word gap setting used
- `AudioBytes` (long) - Output PCM file size
- `ConcatenationMs` (double) - FFmpeg processing time
- `OutputPath` (string) - Temporary file path (for debugging)

**Example**:
```csharp
_logger.LogInformation(
    "VOX_CONCATENATION_COMPLETED: Concatenated {ClipCount} clips ({AudioBytes} bytes) in {ConcatenationMs}ms",
    clipCount, audioBytes, concatenationMs);
```

---

#### VOX_CLIP_LIBRARY_INITIALIZED

**Purpose**: Record clip library initialization at startup.

**Log Level**: `Information`

**Template**:
```csharp
"VOX_CLIP_LIBRARY_INITIALIZED: Loaded {VoxCount} VOX, {FvoxCount} FVOX, {HgruntCount} HGrunt clips in {InitializationMs}ms"
```

**Required Properties**:
- `VoxCount` (int) - VOX clip count
- `FvoxCount` (int) - FVOX clip count
- `HgruntCount` (int) - HGrunt clip count
- `TotalClips` (int) - Sum of all clips
- `InitializationMs` (long) - Initialization duration

**Example**:
```csharp
_logger.LogInformation(
    "VOX_CLIP_LIBRARY_INITIALIZED: Loaded {VoxCount} VOX, {FvoxCount} FVOX, {HgruntCount} HGrunt clips in {InitializationMs}ms",
    voxCount, fvoxCount, hgruntCount, stopwatch.ElapsedMilliseconds);
```

---

## OpenTelemetry Metrics

### Metrics Architecture

Create a new `VoxMetrics.cs` class following the pattern of `BotMetrics.cs` and `BusinessMetrics.cs`:

```csharp
namespace DiscordBot.Bot.Metrics;

public sealed class VoxMetrics : IDisposable
{
    public const string MeterName = "DiscordBot.Vox";

    private readonly Meter _meter;
    // Counter and Histogram fields defined below

    public VoxMetrics(IMeterFactory meterFactory)
    {
        _meter = meterFactory.Create(MeterName);
        // Initialize instruments
    }

    public void Dispose() => _meter.Dispose();
}
```

### Counter Metrics

#### discordbot.vox.commands.total

**Description**: Total number of VOX commands executed.

**Unit**: `{commands}`

**Type**: Counter (monotonically increasing)

**Tags**:
- `group` - `"vox"`, `"fvox"`, or `"hgrunt"`
- `source` - `"slash_command"` or `"portal"`
- `status` - `"success"` or `"failure"`

**Implementation**:
```csharp
private readonly Counter<long> _commandCounter;

_commandCounter = _meter.CreateCounter<long>(
    name: "discordbot.vox.commands.total",
    unit: "{commands}",
    description: "Total number of VOX commands executed");

public void RecordCommandExecution(string group, string source, bool success)
{
    var tags = new TagList
    {
        { "group", group.ToLowerInvariant() },
        { "source", source },
        { "status", success ? "success" : "failure" }
    };
    _commandCounter.Add(1, tags);
}
```

---

#### discordbot.vox.clips.played

**Description**: Total number of individual clips played (matched words).

**Unit**: `{clips}`

**Type**: Counter

**Tags**:
- `group` - `"vox"`, `"fvox"`, or `"hgrunt"`

**Implementation**:
```csharp
private readonly Counter<long> _clipsPlayedCounter;

_clipsPlayedCounter = _meter.CreateCounter<long>(
    name: "discordbot.vox.clips.played",
    unit: "{clips}",
    description: "Total number of individual clips played");

public void RecordClipsPlayed(string group, int clipCount)
{
    _clipsPlayedCounter.Add(clipCount, new TagList { { "group", group.ToLowerInvariant() } });
}
```

---

#### discordbot.vox.words.matched

**Description**: Total number of words that matched clips.

**Unit**: `{words}`

**Type**: Counter

**Tags**:
- `group` - `"vox"`, `"fvox"`, or `"hgrunt"`

**Implementation**:
```csharp
private readonly Counter<long> _wordsMatchedCounter;

_wordsMatchedCounter = _meter.CreateCounter<long>(
    name: "discordbot.vox.words.matched",
    unit: "{words}",
    description: "Total number of words that matched clips");

public void RecordWordsMatched(string group, int matchedCount)
{
    _wordsMatchedCounter.Add(matchedCount, new TagList { { "group", group.ToLowerInvariant() } });
}
```

---

#### discordbot.vox.words.skipped

**Description**: Total number of words without matching clips.

**Unit**: `{words}`

**Type**: Counter

**Tags**:
- `group` - `"vox"`, `"fvox"`, or `"hgrunt"`

**Implementation**:
```csharp
private readonly Counter<long> _wordsSkippedCounter;

_wordsSkippedCounter = _meter.CreateCounter<long>(
    name: "discordbot.vox.words.skipped",
    unit: "{words}",
    description: "Total number of words without matching clips");

public void RecordWordsSkipped(string group, int skippedCount)
{
    _wordsSkippedCounter.Add(skippedCount, new TagList { { "group", group.ToLowerInvariant() } });
}
```

---

#### discordbot.vox.errors

**Description**: Total number of VOX errors by type.

**Unit**: `{errors}`

**Type**: Counter

**Tags**:
- `group` - `"vox"`, `"fvox"`, or `"hgrunt"`
- `error_type` - See Error Types table in logging section

**Implementation**:
```csharp
private readonly Counter<long> _errorCounter;

_errorCounter = _meter.CreateCounter<long>(
    name: "discordbot.vox.errors",
    unit: "{errors}",
    description: "Total number of VOX errors by type");

public void RecordError(string group, string errorType)
{
    var tags = new TagList
    {
        { "group", group.ToLowerInvariant() },
        { "error_type", errorType }
    };
    _errorCounter.Add(1, tags);
}
```

---

### Histogram Metrics

#### discordbot.vox.command.duration

**Description**: Total command execution duration (end-to-end).

**Unit**: `ms`

**Type**: Histogram

**Tags**:
- `group` - `"vox"`, `"fvox"`, or `"hgrunt"`
- `source` - `"slash_command"` or `"portal"`
- `status` - `"success"` or `"failure"`

**Implementation**:
```csharp
private readonly Histogram<double> _commandDuration;

_commandDuration = _meter.CreateHistogram<double>(
    name: "discordbot.vox.command.duration",
    unit: "ms",
    description: "Total command execution duration");

public void RecordCommandDuration(string group, string source, bool success, double durationMs)
{
    var tags = new TagList
    {
        { "group", group.ToLowerInvariant() },
        { "source", source },
        { "status", success ? "success" : "failure" }
    };
    _commandDuration.Record(durationMs, tags);
}
```

---

#### discordbot.vox.concatenation.duration

**Description**: FFmpeg concatenation processing time.

**Unit**: `ms`

**Type**: Histogram

**Tags**:
- `group` - `"vox"`, `"fvox"`, or `"hgrunt"`

**Implementation**:
```csharp
private readonly Histogram<double> _concatenationDuration;

_concatenationDuration = _meter.CreateHistogram<double>(
    name: "discordbot.vox.concatenation.duration",
    unit: "ms",
    description: "FFmpeg concatenation processing time");

public void RecordConcatenationDuration(string group, double durationMs)
{
    _concatenationDuration.Record(durationMs, new TagList { { "group", group.ToLowerInvariant() } });
}
```

---

#### discordbot.vox.message.words

**Description**: Number of words per message.

**Unit**: `{words}`

**Type**: Histogram

**Tags**:
- `group` - `"vox"`, `"fvox"`, or `"hgrunt"`

**Implementation**:
```csharp
private readonly Histogram<long> _messageWords;

_messageWords = _meter.CreateHistogram<long>(
    name: "discordbot.vox.message.words",
    unit: "{words}",
    description: "Number of words per message");

public void RecordMessageWordCount(string group, int wordCount)
{
    _messageWords.Record(wordCount, new TagList { { "group", group.ToLowerInvariant() } });
}
```

---

#### discordbot.vox.match.percentage

**Description**: Percentage of words matched (0-100).

**Unit**: `%`

**Type**: Histogram

**Tags**:
- `group` - `"vox"`, `"fvox"`, or `"hgrunt"`

**Implementation**:
```csharp
private readonly Histogram<double> _matchPercentage;

_matchPercentage = _meter.CreateHistogram<double>(
    name: "discordbot.vox.match.percentage",
    unit: "%",
    description: "Percentage of words matched");

public void RecordMatchPercentage(string group, double percentage)
{
    _matchPercentage.Record(percentage, new TagList { { "group", group.ToLowerInvariant() } });
}
```

---

#### discordbot.vox.audio.bytes

**Description**: Output PCM audio size in bytes.

**Unit**: `By` (bytes)

**Type**: Histogram

**Tags**:
- `group` - `"vox"`, `"fvox"`, or `"hgrunt"`

**Implementation**:
```csharp
private readonly Histogram<long> _audioBytes;

_audioBytes = _meter.CreateHistogram<long>(
    name: "discordbot.vox.audio.bytes",
    unit: "By",
    description: "Output PCM audio size in bytes");

public void RecordAudioSize(string group, long bytes)
{
    _audioBytes.Record(bytes, new TagList { { "group", group.ToLowerInvariant() } });
}
```

---

### Business Metrics Integration

Add VOX feature usage to `BusinessMetrics.cs`:

```csharp
// In VoxModule.cs or VoxService.cs
_businessMetrics.RecordFeatureUsage($"vox.{group.ToLowerInvariant()}");
```

This tracks VOX alongside other bot features (soundboard, TTS, reminders, etc.).

---

## Distributed Tracing

### Activity Source Setup

Create a shared `ActivitySource` for VOX operations:

```csharp
// In VoxService.cs
private static readonly ActivitySource ActivitySource = new("DiscordBot.Vox");
```

Register in DI:

```csharp
// In Program.cs OpenTelemetry configuration
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("DiscordBot.Bot")
        .AddSource("DiscordBot.Vox") // Add VOX tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());
```

### Span Hierarchy

```
VoxCommand (root span)
├── Tokenization
├── ClipLookup
├── Concatenation
│   ├── FFmpegDecode (per clip)
│   └── FFmpegDecode (per clip)
└── Playback
```

### Implementation Example

#### Root Span: VoxCommand

```csharp
public async Task<VoxPlaybackResult> PlayAsync(
    ulong guildId,
    string message,
    VoxClipGroup group,
    VoxPlaybackOptions options,
    CancellationToken cancellationToken = default)
{
    using var activity = ActivitySource.StartActivity("VoxCommand");
    activity?.SetTag("group", group.ToString());
    activity?.SetTag("guild_id", guildId);
    activity?.SetTag("word_gap_ms", options.WordGapMs);
    activity?.SetTag("message_length", message.Length);

    try
    {
        // Tokenization span
        using (var tokenizeActivity = ActivitySource.StartActivity("Tokenization", ActivityKind.Internal))
        {
            var tokens = TokenizeMessage(message);
            tokenizeActivity?.SetTag("token_count", tokens.Count);
            activity?.SetTag("total_words", tokens.Count);
        }

        // Clip lookup span
        List<VoxClipInfo> matchedClips;
        List<string> skippedWords;
        using (var lookupActivity = ActivitySource.StartActivity("ClipLookup", ActivityKind.Internal))
        {
            // ... clip resolution logic ...
            lookupActivity?.SetTag("matched_count", matchedClips.Count);
            lookupActivity?.SetTag("skipped_count", skippedWords.Count);

            activity?.SetTag("matched_words", matchedClips.Count);
            activity?.SetTag("skipped_words", skippedWords.Count);
        }

        // Concatenation span (VoxConcatenationService will create child spans)
        var concatenatedPcmPath = await _concatenationService.ConcatenateAsync(
            clipPaths, options.WordGapMs, cancellationToken);

        var audioBytes = new FileInfo(concatenatedPcmPath).Length;
        activity?.SetTag("audio_bytes", audioBytes);

        // Playback span
        using (var playbackActivity = ActivitySource.StartActivity("Playback", ActivityKind.Internal))
        {
            await StreamPcmToDiscordAsync(audioClient, concatenatedPcmPath, guildId, cancellationToken);
        }

        activity?.SetStatus(ActivityStatusCode.Ok);
        return new VoxPlaybackResult { Success = true, ... };
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.RecordException(ex);
        throw;
    }
}
```

---

#### Child Span: Concatenation

```csharp
public async Task<string> ConcatenateAsync(
    IReadOnlyList<string> clipFilePaths,
    int wordGapMs,
    CancellationToken cancellationToken = default)
{
    using var activity = ActivitySource.StartActivity("Concatenation");
    activity?.SetTag("clip_count", clipFilePaths.Count);
    activity?.SetTag("word_gap_ms", wordGapMs);

    var stopwatch = Stopwatch.StartNew();

    try
    {
        // Concatenation logic...

        stopwatch.Stop();
        activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
        activity?.SetTag("output_bytes", outputStream.Length);
        activity?.SetStatus(ActivityStatusCode.Ok);

        return outputPath;
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.RecordException(ex);
        throw;
    }
}
```

---

## Kibana Dashboard Integration

VOX telemetry visualizations are integrated into the **Soundboard & Audio** dashboard (`discord-soundboard-dashboard`).

### Implemented Visualizations

| ID | Name | Type | Purpose |
|----|------|------|---------|
| `discord-vox-commands-total` | VOX Commands Total | Metric | Total VOX commands executed |
| `discord-vox-by-group` | VOX Commands by Group | Donut | Distribution across VOX/FVOX/HGRUNT |
| `discord-vox-match-rate` | VOX Average Match Rate | Metric | Average word-to-clip match percentage |
| `discord-vox-timeline` | VOX Commands Over Time | Line | VOX usage trend by clip group |
| `discord-vox-errors` | VOX Errors | Metric | VOX command failure count |

### KQL Queries

```kql
# All VOX commands (started)
message:"VOX_COMMAND_STARTED"

# Successful VOX completions
message:"VOX_COMMAND_COMPLETED"

# VOX failures
message:"VOX_COMMAND_FAILED"

# VOX by specific group (VOX, FVOX, HGRUNT)
message:"VOX_COMMAND_COMPLETED" AND labels.Group:"FVOX"

# Low match rate commands (<50%)
message:"VOX_COMMAND_COMPLETED" AND labels.MatchPercentage:<50

# VOX concatenation events
message:"VOX_CONCATENATION_COMPLETED"

# VOX errors by type
message:"VOX_COMMAND_FAILED" AND labels.ErrorType:"NoClipsMatched"

# Slow VOX commands (>2 seconds)
message:"VOX_COMMAND_COMPLETED" AND labels.DurationMs:>2000

# VOX portal vs slash command usage
message:"VOX_COMMAND_STARTED" AND labels.Source:"Portal"
```

See [kibana-dashboards.md](kibana-dashboards.md) for complete dashboard documentation.

---

### Future Dashboard Enhancements

The following visualizations could be added for deeper VOX analytics:

1. **VOX Performance Dashboard**
   - Command duration distribution (histogram with p50/p75/p95)
   - Concatenation vs playback time breakdown
   - Audio size vs duration scatter plot

2. **VOX Errors & Quality Dashboard**
   - Error rate over time by error_type
   - Match vs skip ratio by group
   - Match percentage distribution histogram

3. **VOX Guild Activity Dashboard**
   - Top active guilds table
   - Guild command heatmap (hour of day vs day of week)
   - User activity table

---

### Log Query Examples

#### Commands with Low Match Rate

```
@log_level: "Warning" AND @message: "VOX_COMMAND_COMPLETED" AND MatchPercentage < 50
```

#### FFmpeg Failures

```
@log_level: "Error" AND @message: "VOX_COMMAND_FAILED" AND ErrorType: "ConcatenationFailed"
```

#### Slow Commands (>2s)

```
@message: "VOX_COMMAND_COMPLETED" AND DurationMs > 2000
```

#### Portal vs Slash Command Usage

```
@message: "VOX_COMMAND_STARTED" | stats count() by Source
```

---

## Implementation Guide

### Step 1: Create VoxMetrics Class

Create `c:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Metrics\VoxMetrics.cs`:

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DiscordBot.Bot.Metrics;

/// <summary>
/// Defines metrics for VOX (Half-Life concatenated audio) system.
/// Tracks command execution, clip usage, performance, and errors.
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
    /// Records a VOX command execution.
    /// </summary>
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
    public void RecordClipsPlayed(string group, int clipCount)
    {
        _clipsPlayedCounter.Add(clipCount, new TagList { { "group", group.ToLowerInvariant() } });
    }

    /// <summary>
    /// Records word matching statistics.
    /// </summary>
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
    /// Records an error occurrence.
    /// </summary>
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
    /// Records concatenation performance.
    /// </summary>
    public void RecordConcatenation(string group, double durationMs, long audioBytes)
    {
        var groupTag = new TagList { { "group", group.ToLowerInvariant() } };
        _concatenationDuration.Record(durationMs, groupTag);
        _audioBytes.Record(audioBytes, groupTag);
    }

    public void Dispose() => _meter.Dispose();
}
```

---

### Step 2: Register VoxMetrics in DI

In `c:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Extensions\VoiceServiceExtensions.cs`:

```csharp
public static IServiceCollection AddVox(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.Configure<VoxOptions>(
        configuration.GetSection(VoxOptions.SectionName));

    // Services
    services.AddSingleton<IVoxClipLibrary, VoxClipLibrary>();
    services.AddSingleton<IVoxConcatenationService, VoxConcatenationService>();
    services.AddScoped<IVoxService, VoxService>();

    // Metrics
    services.AddSingleton<VoxMetrics>();

    // Hosted services
    services.AddHostedService<VoxClipLibraryInitializer>();

    return services;
}
```

---

### Step 3: Add Telemetry to VoxService

Update `c:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Services\VoxService.cs`:

```csharp
public class VoxService : IVoxService
{
    private readonly ILogger<VoxService> _logger;
    private readonly VoxOptions _options;
    private readonly IVoxClipLibrary _clipLibrary;
    private readonly IVoxConcatenationService _concatenationService;
    private readonly IAudioService _audioService;
    private readonly VoxMetrics _voxMetrics;
    private readonly BusinessMetrics _businessMetrics;

    private static readonly ActivitySource ActivitySource = new("DiscordBot.Vox");

    public VoxService(
        ILogger<VoxService> logger,
        IOptions<VoxOptions> options,
        IVoxClipLibrary clipLibrary,
        IVoxConcatenationService concatenationService,
        IAudioService audioService,
        VoxMetrics voxMetrics,
        BusinessMetrics businessMetrics)
    {
        _logger = logger;
        _options = options.Value;
        _clipLibrary = clipLibrary;
        _concatenationService = concatenationService;
        _audioService = audioService;
        _voxMetrics = voxMetrics;
        _businessMetrics = businessMetrics;
    }

    public async Task<VoxPlaybackResult> PlayAsync(
        ulong guildId,
        string message,
        VoxClipGroup group,
        VoxPlaybackOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var groupName = group.ToString().ToUpperInvariant();

        using var activity = ActivitySource.StartActivity("VoxCommand");
        activity?.SetTag("group", groupName);
        activity?.SetTag("guild_id", guildId);
        activity?.SetTag("word_gap_ms", options.WordGapMs);

        try
        {
            // Validation...

            // Tokenization
            List<string> tokens;
            using (var tokenizeActivity = ActivitySource.StartActivity("Tokenization", ActivityKind.Internal))
            {
                tokens = TokenizeMessage(message);
                tokenizeActivity?.SetTag("token_count", tokens.Count);
                activity?.SetTag("total_words", tokens.Count);
            }

            // Clip lookup
            var matchedClips = new List<VoxClipInfo>();
            var matchedWords = new List<string>();
            var skippedWords = new List<string>();

            using (var lookupActivity = ActivitySource.StartActivity("ClipLookup", ActivityKind.Internal))
            {
                foreach (var token in tokens)
                {
                    var clip = _clipLibrary.GetClip(group, token);
                    if (clip != null)
                    {
                        matchedClips.Add(clip);
                        matchedWords.Add(token);
                    }
                    else
                    {
                        skippedWords.Add(token);
                    }
                }

                lookupActivity?.SetTag("matched_count", matchedClips.Count);
                lookupActivity?.SetTag("skipped_count", skippedWords.Count);
            }

            if (matchedClips.Count == 0)
            {
                stopwatch.Stop();
                _voxMetrics.RecordError(groupName, "NoClipsMatched");
                _voxMetrics.RecordCommandExecution(groupName, "slash_command", false, stopwatch.Elapsed.TotalMilliseconds);

                _logger.LogWarning(
                    "VOX_COMMAND_FAILED: {Group} command failed for guild {GuildId}. Reason: {ErrorType} - {ErrorMessage}",
                    groupName, guildId, "NoClipsMatched", "No matching clips found");

                return new VoxPlaybackResult { Success = false, ErrorMessage = "...", SkippedWords = skippedWords };
            }

            // Concatenation
            var concatenatedPcmPath = await _concatenationService.ConcatenateAsync(
                matchedClips.Select(c => _clipLibrary.GetClipFilePath(c.Group, c.Name)).ToList(),
                options.WordGapMs,
                cancellationToken);

            var audioBytes = new FileInfo(concatenatedPcmPath).Length;
            activity?.SetTag("audio_bytes", audioBytes);

            // Playback
            using (var playbackActivity = ActivitySource.StartActivity("Playback", ActivityKind.Internal))
            {
                await StreamPcmToDiscordAsync(audioClient, concatenatedPcmPath, guildId, cancellationToken);
            }

            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;

            // Record metrics
            _voxMetrics.RecordCommandExecution(groupName, "slash_command", true, durationMs);
            _voxMetrics.RecordClipsPlayed(groupName, matchedClips.Count);
            _voxMetrics.RecordWordStats(groupName, matchedWords.Count, skippedWords.Count, tokens.Count);
            _businessMetrics.RecordFeatureUsage($"vox.{groupName.ToLowerInvariant()}");

            // Log completion
            var matchPercentage = (matchedWords.Count / (double)tokens.Count) * 100;
            _logger.LogInformation(
                "VOX_COMMAND_COMPLETED: {Group} playback finished. Matched: {MatchedCount}/{TotalWords} ({MatchPercentage:F1}%), Skipped: {SkippedCount}, Duration: {DurationMs}ms, AudioBytes: {AudioBytes}",
                groupName, matchedWords.Count, tokens.Count, matchPercentage, skippedWords.Count, durationMs, audioBytes);

            activity?.SetStatus(ActivityStatusCode.Ok);
            return new VoxPlaybackResult { Success = true, MatchedWords = matchedWords, SkippedWords = skippedWords };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _voxMetrics.RecordError(groupName, "UnknownError");
            _voxMetrics.RecordCommandExecution(groupName, "slash_command", false, stopwatch.Elapsed.TotalMilliseconds);

            _logger.LogError(ex,
                "VOX_COMMAND_FAILED: {Group} command failed for guild {GuildId}. Reason: {ErrorType}",
                groupName, guildId, "UnknownError");

            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);
            throw;
        }
    }
}
```

---

### Step 4: Add Concatenation Telemetry

Update `c:\Users\cpike\workspace\discordbot\src\DiscordBot.Infrastructure\Services\Vox\VoxConcatenationService.cs`:

```csharp
public class VoxConcatenationService : IVoxConcatenationService
{
    private readonly ILogger<VoxConcatenationService> _logger;
    private readonly VoxOptions _options;
    private readonly VoxMetrics _voxMetrics;

    private static readonly ActivitySource ActivitySource = new("DiscordBot.Vox");

    public VoxConcatenationService(
        ILogger<VoxConcatenationService> logger,
        IOptions<VoxOptions> options,
        VoxMetrics voxMetrics)
    {
        _logger = logger;
        _options = options.Value;
        _voxMetrics = voxMetrics;
    }

    public async Task<string> ConcatenateAsync(
        IReadOnlyList<string> clipFilePaths,
        int wordGapMs,
        CancellationToken cancellationToken = default)
    {
        using var activity = ActivitySource.StartActivity("Concatenation");
        activity?.SetTag("clip_count", clipFilePaths.Count);
        activity?.SetTag("word_gap_ms", wordGapMs);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // ... existing concatenation logic ...

            stopwatch.Stop();
            var audioBytes = new FileInfo(outputPath).Length;
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;

            // Extract group from first clip path (e.g., "sounds/vox/warning.mp3" -> "vox")
            var group = Path.GetFileName(Path.GetDirectoryName(clipFilePaths[0]))?.ToUpperInvariant() ?? "UNKNOWN";

            _voxMetrics.RecordConcatenation(group, durationMs, audioBytes);

            _logger.LogInformation(
                "VOX_CONCATENATION_COMPLETED: Concatenated {ClipCount} clips ({AudioBytes} bytes) in {ConcatenationMs}ms",
                clipFilePaths.Count, audioBytes, durationMs);

            activity?.SetTag("duration_ms", durationMs);
            activity?.SetTag("output_bytes", audioBytes);
            activity?.SetStatus(ActivityStatusCode.Ok);

            return outputPath;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.RecordException(ex);

            _logger.LogError(ex, "FFmpeg concatenation failed after {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
```

---

### Step 5: Register Activity Source

In `c:\Users\cpike\workspace\discordbot\src\DiscordBot.Bot\Program.cs`:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("DiscordBot.Bot")
        .AddSource("DiscordBot.Vox") // Add VOX tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());
```

---

### Implementation Checklist

- [ ] Create `VoxMetrics.cs` class
- [ ] Register `VoxMetrics` in `VoiceServiceExtensions.cs`
- [ ] Add `VoxMetrics` and `BusinessMetrics` to `VoxService` constructor
- [ ] Add `ActivitySource` to `VoxService` and `VoxConcatenationService`
- [ ] Update `VoxService.PlayAsync()` with telemetry calls
- [ ] Update `VoxConcatenationService.ConcatenateAsync()` with telemetry
- [ ] Add structured logging events (`VOX_COMMAND_STARTED`, `VOX_COMMAND_COMPLETED`, etc.)
- [ ] Register `"DiscordBot.Vox"` activity source in OpenTelemetry configuration
- [ ] Add portal source tracking (use `"portal"` source tag when called from web)
- [x] Create Kibana dashboards for VOX metrics
- [ ] Test metrics collection with local OTLP exporter
- [ ] Update VOX documentation with telemetry examples

---

## Related Documentation

- **[vox-system-spec.md](vox-system-spec.md)** - VOX system architecture and service layer
- **[vox-ui-spec.md](vox-ui-spec.md)** - VOX Portal UI/UX specification
- **[BotMetrics.cs](../../src/DiscordBot.Bot/Metrics/BotMetrics.cs)** - Existing metrics pattern reference
- **[BusinessMetrics.cs](../../src/DiscordBot.Bot/Metrics/BusinessMetrics.cs)** - Feature usage tracking

---

**Last Updated**: 2026-02-04
**Version**: 1.1 (Kibana visualizations implemented)
