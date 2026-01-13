# Issue #1058 - Add Distributed Tracing for TTS and Audio Streaming Operations

## Implementation Plan

**Document Version:** 1.0
**Date:** 2026-01-12
**Issue Reference:** GitHub Issue #1058
**Priority:** P2 - Medium
**Effort:** Medium (6-8 hours)
**Dependencies:**
- Issue #105: Distributed Tracing with OpenTelemetry (COMPLETED)

---

## 1. Requirement Summary

Extend distributed tracing to cover Text-to-Speech (TTS) and audio streaming operations to improve observability of latency-sensitive workflows. The system currently has:
- OpenTelemetry tracing infrastructure (`BotActivitySource`, `InfrastructureActivitySource`)
- Command and database operation tracing
- Trace context propagation through correlation IDs

This implementation will add:
- Azure Speech API synthesis tracing with detailed timing
- TTS audio conversion spans (mono-to-stereo)
- Discord audio streaming spans for TTS playback
- FFmpeg transcode spans for soundboard playback
- Voice channel connection/disconnection spans
- Proper span hierarchy for nested audio operations
- Audio-specific attributes (duration, bytes, voice, filters)

**Current Problem:**
A trace for `POST PortalTts/SendTts` shows the database operations but the Azure Speech API call (~10 seconds) and Discord audio streaming are invisible, making performance debugging difficult.

---

## 2. Architectural Considerations

### 2.1 Existing System Components

| Component | Location | Relevance |
|-----------|----------|-----------|
| `BotActivitySource` | `src/DiscordBot.Bot/Tracing/` | Extend with audio-specific helper methods |
| `TracingConstants` | `src/DiscordBot.Bot/Tracing/` | Add new span names and attributes |
| `AzureTtsService` | `src/DiscordBot.Bot/Services/` | Primary TTS instrumentation point |
| `PortalTtsController` | `src/DiscordBot.Bot/Controllers/` | Instrument audio conversion and streaming |
| `PlaybackService` | `src/DiscordBot.Bot/Services/` | Enhance existing soundboard tracing |
| `AudioService` | `src/DiscordBot.Bot/Services/` | Add voice connection tracing |
| `TtsModule` | `src/DiscordBot.Bot/Commands/` | Future command-level tracing point |

### 2.2 Audio Operations Trace Hierarchy

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                        TTS Portal Request Flow                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ Parent Span: "POST PortalTts/SendTts" (ASP.NET Core auto)           │   │
│  │ TraceId: abc123... | SpanId: def456... | CorrelationId: 1a2b3c4d... │   │
│  │                                                                       │   │
│  │  ┌────────────────────────────────────────────────────────────────┐  │   │
│  │  │ Child Span: "azure_speech.synthesize"                          │  │   │
│  │  │ Attributes: tts.text_length, tts.voice, tts.region             │  │   │
│  │  │ Duration: ~10 seconds                                          │  │   │
│  │  └────────────────────────────────────────────────────────────────┘  │   │
│  │                                                                       │   │
│  │  ┌────────────────────────────────────────────────────────────────┐  │   │
│  │  │ Child Span: "tts.audio_convert"                                │  │   │
│  │  │ Attributes: audio.format_from, audio.format_to                 │  │   │
│  │  │ Duration: ~50ms                                                │  │   │
│  │  └────────────────────────────────────────────────────────────────┘  │   │
│  │                                                                       │   │
│  │  ┌────────────────────────────────────────────────────────────────┐  │   │
│  │  │ Child Span: "discord.audio_stream"                             │  │   │
│  │  │ Attributes: audio.duration_seconds, audio.bytes_written        │  │   │
│  │  │ Duration: ~variable (based on TTS length)                      │  │   │
│  │  └────────────────────────────────────────────────────────────────┘  │   │
│  │                                                                       │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                       Soundboard Playback Flow                               │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ Parent Span: "service.playback.play" (existing)                      │   │
│  │                                                                       │   │
│  │  ┌────────────────────────────────────────────────────────────────┐  │   │
│  │  │ Child Span: "soundboard.ffmpeg_transcode"                       │  │   │
│  │  │ Attributes: ffmpeg.process_id, sound.file_path, audio.filter   │  │   │
│  │  │                                                                 │  │   │
│  │  │  ┌─────────────────────────────────────────────────────────┐   │  │   │
│  │  │  │ Child Span: "soundboard.audio_stream"                   │   │  │   │
│  │  │  │ Attributes: audio.bytes_streamed, audio.buffer_count    │   │  │   │
│  │  │  └─────────────────────────────────────────────────────────┘   │  │   │
│  │  └────────────────────────────────────────────────────────────────┘  │   │
│  │                                                                       │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────────┐
│                       Voice Connection Flow                                  │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                              │
│  ┌──────────────────────────────────────────────────────────────────────┐   │
│  │ Parent Span: "service.audio.join_channel" (existing)                 │   │
│  │ Attributes: voice.channel_id, voice.channel_name, discord.guild.id  │   │
│  │                                                                       │   │
│  │  ┌────────────────────────────────────────────────────────────────┐  │   │
│  │  │ Child Span: "discord.voice_connect" (Discord.NET internal)     │  │   │
│  │  └────────────────────────────────────────────────────────────────┘  │   │
│  │                                                                       │   │
│  └──────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
└─────────────────────────────────────────────────────────────────────────────┘
```

### 2.3 Integration Requirements

1. **Azure Speech SDK Integration**
   - Wrap `SpeechSynthesizer.SpeakSsmlAsync()` with activity span
   - Capture synthesis result details (audio size, cancellation reason)
   - Record exceptions with meaningful context

2. **Audio Streaming Integration**
   - Instrument PCM stream writes in `PortalTtsController.SendTts`
   - Track bytes written and streaming duration
   - Separate conversion from streaming for granular timing

3. **FFmpeg Process Integration**
   - Add span around `StreamAudioAsync` in `PlaybackService`
   - Record FFmpeg process ID and exit code
   - Capture filter application and fallback behavior

4. **Voice Connection Integration**
   - Enhance existing `AudioService` spans with voice-specific attributes
   - Add channel name and connection metadata

### 2.4 Performance Considerations

| Concern | Approach |
|---------|----------|
| TTS synthesis duration | Long-running span (~10s) is acceptable; provides visibility |
| Audio streaming overhead | Span creation is minimal compared to I/O operations |
| FFmpeg process tracking | Process ID tracking has negligible overhead |
| Voice connection spans | One-time events, no performance concern |

### 2.5 Security Considerations

| Risk | Mitigation |
|------|------------|
| TTS text content in traces | Never include full text; only length attribute |
| Voice names in traces | Safe to include; no PII |
| File paths in traces | Include relative path only; no absolute system paths |
| User IDs in audio spans | Already handled by parent span attributes |

---

## 3. Tracing Specification

### 3.1 New Span Names

Add to `TracingConstants.Spans`:

| Span Name | Usage | Parent |
|-----------|-------|--------|
| `azure_speech.synthesize` | Azure TTS synthesis | HTTP request or command |
| `azure_speech.get_voices` | Voice list retrieval | HTTP request or command |
| `tts.audio_convert` | Mono-to-stereo conversion | `azure_speech.synthesize` |
| `discord.audio_stream` | PCM stream writes | TTS controller action |
| `soundboard.ffmpeg_transcode` | FFmpeg process execution | `service.playback.play_sound` |
| `soundboard.audio_stream` | PCM writes from FFmpeg | `soundboard.ffmpeg_transcode` |
| `discord.voice_join` | Voice channel connection | `service.audio.join_channel` |
| `discord.voice_leave` | Voice channel disconnection | `service.audio.leave_channel` |

### 3.2 New Span Attributes

Add to `TracingConstants.Attributes`:

| Attribute Key | Type | Description | Example |
|--------------|------|-------------|---------|
| `tts.text_length` | int | Character count of TTS text | `256` |
| `tts.voice` | string | Azure voice name | `"en-US-JennyNeural"` |
| `tts.region` | string | Azure region | `"eastus"` |
| `tts.audio_size_bytes` | long | Raw audio data size | `384000` |
| `tts.speed` | double | Speech speed multiplier | `1.0` |
| `tts.pitch` | double | Pitch multiplier | `1.0` |
| `tts.volume` | double | Volume level | `1.0` |
| `tts.synthesis_result` | string | Azure result reason | `"SynthesizingAudioCompleted"` |
| `tts.cancellation_reason` | string | Cancellation details | `"Error"` |
| `audio.format_from` | string | Source format | `"mono"` |
| `audio.format_to` | string | Target format | `"stereo"` |
| `audio.duration_seconds` | double | Audio length | `5.2` |
| `audio.bytes_written` | long | Total bytes streamed | `998400` |
| `audio.bytes_streamed` | long | Bytes sent to Discord | `998400` |
| `audio.buffer_count` | int | Number of buffers written | `260` |
| `audio.filter` | string | Audio filter name | `"bass_boost"` |
| `ffmpeg.process_id` | int | Process ID | `12345` |
| `ffmpeg.exit_code` | int | Process exit code | `0` |
| `ffmpeg.arguments` | string | Command arguments (sanitized) | `-af bass=...` |
| `sound.id` | string | Sound GUID | `"a1b2c3d4..."` |
| `sound.name` | string | Sound display name | `"airhorn"` |
| `sound.file_path` | string | Relative file path | `"123456/airhorn.mp3"` |
| `sound.file_size_bytes` | long | File size | `45678` |
| `sound.duration_seconds` | double | Sound duration | `2.5` |
| `voice.channel_id` | string | Voice channel snowflake | `"987654321"` |
| `voice.channel_name` | string | Channel display name | `"General Voice"` |
| `voice.connected_at` | string | ISO timestamp | `"2026-01-12T10:30:00Z"` |

---

## 4. Implementation Details

### 4.1 Update TracingConstants

**Location:** `src/DiscordBot.Bot/Tracing/TracingConstants.cs`

Add the following to the `Attributes` class:

```csharp
// Azure TTS attributes
public const string TtsTextLength = "tts.text_length";
public const string TtsVoice = "tts.voice";
public const string TtsRegion = "tts.region";
public const string TtsAudioSizeBytes = "tts.audio_size_bytes";
public const string TtsSpeed = "tts.speed";
public const string TtsPitch = "tts.pitch";
public const string TtsVolume = "tts.volume";
public const string TtsSynthesisResult = "tts.synthesis_result";
public const string TtsCancellationReason = "tts.cancellation_reason";

// Audio conversion and streaming attributes
public const string AudioFormatFrom = "audio.format_from";
public const string AudioFormatTo = "audio.format_to";
public const string AudioDurationSeconds = "audio.duration_seconds";
public const string AudioBytesWritten = "audio.bytes_written";
public const string AudioBytesStreamed = "audio.bytes_streamed";
public const string AudioBufferCount = "audio.buffer_count";
public const string AudioFilter = "audio.filter";

// FFmpeg attributes
public const string FfmpegProcessId = "ffmpeg.process_id";
public const string FfmpegExitCode = "ffmpeg.exit_code";
public const string FfmpegArguments = "ffmpeg.arguments";

// Sound attributes
public const string SoundId = "sound.id";
public const string SoundName = "sound.name";
public const string SoundFilePath = "sound.file_path";
public const string SoundFileSizeBytes = "sound.file_size_bytes";
public const string SoundDurationSeconds = "sound.duration_seconds";

// Voice channel attributes
public const string VoiceChannelId = "voice.channel_id";
public const string VoiceChannelName = "voice.channel_name";
public const string VoiceConnectedAt = "voice.connected_at";
```

Add the following to the `Spans` class:

```csharp
// Azure TTS spans
public const string AzureSpeechSynthesize = "azure_speech.synthesize";
public const string AzureSpeechGetVoices = "azure_speech.get_voices";

// TTS audio processing spans
public const string TtsAudioConvert = "tts.audio_convert";
public const string DiscordAudioStream = "discord.audio_stream";

// Soundboard spans
public const string SoundboardFfmpegTranscode = "soundboard.ffmpeg_transcode";
public const string SoundboardAudioStream = "soundboard.audio_stream";

// Voice channel spans
public const string DiscordVoiceJoin = "discord.voice_join";
public const string DiscordVoiceLeave = "discord.voice_leave";
```

### 4.2 Add Helper Methods to BotActivitySource

**Location:** `src/DiscordBot.Bot/Tracing/BotActivitySource.cs`

Add the following methods:

```csharp
/// <summary>
/// Starts an activity for Azure Speech synthesis.
/// </summary>
/// <param name="textLength">The length of the text being synthesized.</param>
/// <param name="voice">The voice name.</param>
/// <param name="region">The Azure region.</param>
/// <returns>The started activity, or null if not sampled.</returns>
public static Activity? StartAzureSpeechActivity(
    int textLength,
    string voice,
    string region)
{
    var activity = Source.StartActivity(
        name: TracingConstants.Spans.AzureSpeechSynthesize,
        kind: ActivityKind.Client);

    if (activity is null)
        return null;

    activity.SetTag(TracingConstants.Attributes.TtsTextLength, textLength);
    activity.SetTag(TracingConstants.Attributes.TtsVoice, voice);
    activity.SetTag(TracingConstants.Attributes.TtsRegion, region);

    return activity;
}

/// <summary>
/// Starts an activity for retrieving available voices from Azure Speech.
/// </summary>
/// <param name="locale">The locale filter for voices.</param>
/// <returns>The started activity, or null if not sampled.</returns>
public static Activity? StartGetVoicesActivity(string? locale)
{
    var activity = Source.StartActivity(
        name: TracingConstants.Spans.AzureSpeechGetVoices,
        kind: ActivityKind.Client);

    if (activity is null)
        return null;

    if (!string.IsNullOrEmpty(locale))
    {
        activity.SetTag("tts.locale", locale);
    }

    return activity;
}

/// <summary>
/// Starts an activity for audio format conversion.
/// </summary>
/// <param name="fromFormat">Source audio format.</param>
/// <param name="toFormat">Target audio format.</param>
/// <param name="bytesIn">Input byte count.</param>
/// <returns>The started activity, or null if not sampled.</returns>
public static Activity? StartAudioConversionActivity(
    string fromFormat,
    string toFormat,
    int bytesIn)
{
    var activity = Source.StartActivity(
        name: TracingConstants.Spans.TtsAudioConvert,
        kind: ActivityKind.Internal);

    if (activity is null)
        return null;

    activity.SetTag(TracingConstants.Attributes.AudioFormatFrom, fromFormat);
    activity.SetTag(TracingConstants.Attributes.AudioFormatTo, toFormat);
    activity.SetTag("audio.bytes_in", bytesIn);

    return activity;
}

/// <summary>
/// Starts an activity for Discord audio streaming.
/// </summary>
/// <param name="guildId">The guild ID where audio is being streamed.</param>
/// <param name="durationSeconds">Expected duration in seconds.</param>
/// <returns>The started activity, or null if not sampled.</returns>
public static Activity? StartDiscordAudioStreamActivity(
    ulong guildId,
    double durationSeconds)
{
    var activity = Source.StartActivity(
        name: TracingConstants.Spans.DiscordAudioStream,
        kind: ActivityKind.Client);

    if (activity is null)
        return null;

    activity.SetTag(TracingConstants.Attributes.GuildId, guildId.ToString());
    activity.SetTag(TracingConstants.Attributes.AudioDurationSeconds, durationSeconds);

    return activity;
}

/// <summary>
/// Starts an activity for FFmpeg transcoding.
/// </summary>
/// <param name="soundName">The name of the sound being transcoded.</param>
/// <param name="filePath">The relative file path.</param>
/// <param name="filter">The audio filter being applied.</param>
/// <returns>The started activity, or null if not sampled.</returns>
public static Activity? StartFfmpegTranscodeActivity(
    string soundName,
    string filePath,
    string filter)
{
    var activity = Source.StartActivity(
        name: TracingConstants.Spans.SoundboardFfmpegTranscode,
        kind: ActivityKind.Internal);

    if (activity is null)
        return null;

    activity.SetTag(TracingConstants.Attributes.SoundName, soundName);
    activity.SetTag(TracingConstants.Attributes.SoundFilePath, filePath);
    activity.SetTag(TracingConstants.Attributes.AudioFilter, filter);

    return activity;
}

/// <summary>
/// Starts an activity for soundboard audio streaming to Discord.
/// </summary>
/// <param name="guildId">The guild ID.</param>
/// <param name="soundId">The sound ID.</param>
/// <returns>The started activity, or null if not sampled.</returns>
public static Activity? StartSoundboardStreamActivity(
    ulong guildId,
    Guid soundId)
{
    var activity = Source.StartActivity(
        name: TracingConstants.Spans.SoundboardAudioStream,
        kind: ActivityKind.Client);

    if (activity is null)
        return null;

    activity.SetTag(TracingConstants.Attributes.GuildId, guildId.ToString());
    activity.SetTag(TracingConstants.Attributes.SoundId, soundId.ToString());

    return activity;
}

/// <summary>
/// Records audio streaming completion metrics on the activity.
/// </summary>
/// <param name="activity">The activity to record on.</param>
/// <param name="bytesWritten">Total bytes written to the stream.</param>
/// <param name="bufferCount">Number of buffers written.</param>
public static void RecordAudioStreamMetrics(
    Activity? activity,
    long bytesWritten,
    int bufferCount)
{
    if (activity is null)
        return;

    activity.SetTag(TracingConstants.Attributes.AudioBytesWritten, bytesWritten);
    activity.SetTag(TracingConstants.Attributes.AudioBufferCount, bufferCount);
}

/// <summary>
/// Records FFmpeg process details on the activity.
/// </summary>
/// <param name="activity">The activity to record on.</param>
/// <param name="processId">The FFmpeg process ID.</param>
/// <param name="exitCode">The process exit code.</param>
/// <param name="arguments">The FFmpeg arguments (sanitized).</param>
public static void RecordFfmpegDetails(
    Activity? activity,
    int processId,
    int exitCode,
    string arguments)
{
    if (activity is null)
        return;

    activity.SetTag(TracingConstants.Attributes.FfmpegProcessId, processId);
    activity.SetTag(TracingConstants.Attributes.FfmpegExitCode, exitCode);
    activity.SetTag(TracingConstants.Attributes.FfmpegArguments, arguments);
}
```

### 4.3 Instrument AzureTtsService

**Location:** `src/DiscordBot.Bot/Services/AzureTtsService.cs`

**Changes to `SynthesizeSpeechAsync` method:**

```csharp
public async Task<Stream> SynthesizeSpeechAsync(string text, Core.Models.TtsOptions? options = null, CancellationToken cancellationToken = default)
{
    // ... existing validation code ...

    // Start tracing activity for Azure Speech synthesis
    using var activity = BotActivitySource.StartAzureSpeechActivity(
        textLength: text.Length,
        voice: ttsOptions.Voice,
        region: _options.Region);

    try
    {
        // Build SSML for synthesis
        var ssml = BuildSsml(text, ttsOptions);
        _logger.LogDebug("SSML: {Ssml}", ssml);

        // Add TTS options to activity
        activity?.SetTag(TracingConstants.Attributes.TtsSpeed, ttsOptions.Speed);
        activity?.SetTag(TracingConstants.Attributes.TtsPitch, ttsOptions.Pitch);
        activity?.SetTag(TracingConstants.Attributes.TtsVolume, ttsOptions.Volume);

        // Create synthesizer with raw PCM output format (mono - we'll convert to stereo)
        _speechConfig!.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw48Khz16BitMonoPcm);

        using var synthesizer = new SpeechSynthesizer(_speechConfig, null);

        // Synthesize speech from SSML
        var result = await synthesizer.SpeakSsmlAsync(ssml);

        // Record synthesis result
        activity?.SetTag(TracingConstants.Attributes.TtsSynthesisResult, result.Reason.ToString());

        if (result.Reason == ResultReason.SynthesizingAudioCompleted)
        {
            _logger.LogInformation("Speech synthesis completed successfully. Audio data size: {SizeBytes} bytes", result.AudioData.Length);

            // Record audio size
            activity?.SetTag(TracingConstants.Attributes.TtsAudioSizeBytes, result.AudioData.Length);

            // Convert mono PCM to stereo PCM for Discord
            var stereoData = ConvertMonoToStereo(result.AudioData);

            BotActivitySource.SetSuccess(activity);
            return new MemoryStream(stereoData);
        }
        else if (result.Reason == ResultReason.Canceled)
        {
            var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);
            _logger.LogError("Speech synthesis cancelled: {Reason} - {ErrorDetails}", cancellation.Reason, cancellation.ErrorDetails);

            // Record cancellation details
            activity?.SetTag(TracingConstants.Attributes.TtsCancellationReason, cancellation.Reason.ToString());

            var ex = new InvalidOperationException($"Speech synthesis failed: {cancellation.ErrorDetails}");
            BotActivitySource.RecordException(activity, ex);
            throw ex;
        }
        else
        {
            _logger.LogError("Speech synthesis failed with reason: {Reason}", result.Reason);
            var ex = new InvalidOperationException($"Speech synthesis failed: {result.Reason}");
            BotActivitySource.RecordException(activity, ex);
            throw ex;
        }
    }
    catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
    {
        _logger.LogError(ex, "Unexpected error during speech synthesis");
        BotActivitySource.RecordException(activity, ex);
        throw new InvalidOperationException("Speech synthesis failed. See inner exception for details.", ex);
    }
}
```

**Changes to `ConvertMonoToStereo` method:**

```csharp
private byte[] ConvertMonoToStereo(byte[] monoData)
{
    // Start activity for audio conversion
    using var activity = BotActivitySource.StartAudioConversionActivity(
        fromFormat: "mono_48khz_16bit",
        toFormat: "stereo_48khz_16bit",
        bytesIn: monoData.Length);

    try
    {
        // Each sample is 2 bytes (16-bit). For stereo, we need to duplicate each sample.
        var stereoData = new byte[monoData.Length * 2];

        for (int i = 0; i < monoData.Length; i += 2)
        {
            // Get the mono sample (2 bytes)
            var sampleLow = monoData[i];
            var sampleHigh = monoData[i + 1];

            // Write to left channel
            stereoData[i * 2] = sampleLow;
            stereoData[i * 2 + 1] = sampleHigh;

            // Write to right channel (duplicate)
            stereoData[i * 2 + 2] = sampleLow;
            stereoData[i * 2 + 3] = sampleHigh;
        }

        _logger.LogDebug("Converted mono audio ({MonoBytes} bytes) to stereo ({StereoBytes} bytes)",
            monoData.Length, stereoData.Length);

        // Record output size
        activity?.SetTag("audio.bytes_out", stereoData.Length);
        BotActivitySource.SetSuccess(activity);

        return stereoData;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error converting mono to stereo");
        BotActivitySource.RecordException(activity, ex);
        throw;
    }
}
```

**Changes to `GetAvailableVoicesAsync` method:**

```csharp
public async Task<IEnumerable<Core.Models.VoiceInfo>> GetAvailableVoicesAsync(string? locale = "en-US", CancellationToken cancellationToken = default)
{
    if (!IsConfigured)
    {
        _logger.LogWarning("Cannot retrieve voices - Azure Speech service is not configured");
        return Enumerable.Empty<Core.Models.VoiceInfo>();
    }

    var cacheKey = locale ?? "all";

    // Check cache first
    if (_voiceCache.TryGetValue(cacheKey, out var cachedVoices))
    {
        _logger.LogDebug("Returning {Count} voices from cache for locale '{Locale}'", cachedVoices.Count, cacheKey);
        return cachedVoices;
    }

    // Not in cache, fetch from Azure
    await _voiceCacheLock.WaitAsync(cancellationToken);
    try
    {
        // Double-check after acquiring lock
        if (_voiceCache.TryGetValue(cacheKey, out cachedVoices))
        {
            return cachedVoices;
        }

        _logger.LogInformation("Fetching available voices from Azure Speech service for locale '{Locale}'", cacheKey);

        // Start tracing activity for voice retrieval
        using var activity = BotActivitySource.StartGetVoicesActivity(locale);

        try
        {
            using var synthesizer = new SpeechSynthesizer(_speechConfig!, null);
            var result = await synthesizer.GetVoicesAsync(locale);

            if (result.Reason == ResultReason.VoicesListRetrieved)
            {
                var voices = result.Voices
                    .Select(v => new Core.Models.VoiceInfo
                    {
                        ShortName = v.ShortName,
                        DisplayName = v.LocalName,
                        Locale = v.Locale,
                        Gender = v.Gender.ToString()
                    })
                    .ToList();

                _logger.LogInformation("Retrieved {Count} voices for locale '{Locale}'", voices.Count, cacheKey);

                // Record voice count
                activity?.SetTag("tts.voices_retrieved", voices.Count);

                // Cache the results
                _voiceCache[cacheKey] = voices;

                BotActivitySource.SetSuccess(activity);
                return voices;
            }
            else
            {
                _logger.LogError("Failed to retrieve voices. Reason: {Reason}", result.Reason);
                activity?.SetTag("tts.retrieval_failed", result.Reason.ToString());
                BotActivitySource.SetSuccess(activity); // Not an error, just no voices
                return Enumerable.Empty<Core.Models.VoiceInfo>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available voices for locale '{Locale}'", locale);
            BotActivitySource.RecordException(activity, ex);
            return Enumerable.Empty<Core.Models.VoiceInfo>();
        }
    }
    finally
    {
        _voiceCacheLock.Release();
    }
}
```

### 4.4 Instrument PortalTtsController

**Location:** `src/DiscordBot.Bot/Controllers/PortalTtsController.cs`

**Changes to `SendTts` method (audio streaming section):**

```csharp
// Add using statement at the top:
using DiscordBot.Bot.Tracing;

// ... existing code up to line 265 ...

// Stream the audio to Discord
try
{
    // Start activity for Discord audio streaming
    using var streamActivity = BotActivitySource.StartDiscordAudioStreamActivity(
        guildId: guildId,
        durationSeconds: durationSeconds);

    try
    {
        var bytesWritten = 0L;
        var buffer = new byte[3840]; // Match PlaybackService buffer size
        int bytesRead;

        while ((bytesRead = await audioStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await pcmStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            bytesWritten += bytesRead;
        }

        await pcmStream.FlushAsync(cancellationToken);

        // Record streaming metrics
        BotActivitySource.RecordAudioStreamMetrics(
            streamActivity,
            bytesWritten: bytesWritten,
            bufferCount: (int)(bytesWritten / 3840));

        // Update activity to prevent auto-leave
        _audioService.UpdateLastActivity(guildId);

        _logger.LogInformation("Successfully played TTS message for guild {GuildId}. Bytes written: {BytesWritten}",
            guildId, bytesWritten);

        BotActivitySource.SetSuccess(streamActivity);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to stream TTS audio for guild {GuildId}", guildId);
        BotActivitySource.RecordException(streamActivity, ex);
        throw;
    }
}
catch (Exception ex)
{
    _logger.LogError(ex, "Failed to stream TTS audio for guild {GuildId}", guildId);
    _currentMessages.TryRemove(guildId, out _);
    _ttsPlaybackState.TryRemove(guildId, out _);
    return BadRequest(new ApiErrorDto
    {
        Message = "Failed to play TTS",
        Detail = "An error occurred while streaming audio to Discord.",
        StatusCode = StatusCodes.Status400BadRequest,
        TraceId = HttpContext.GetCorrelationId()
    });
}
finally
{
    // Clear TTS playback state and message tracking after streaming completes
    _ttsPlaybackState.TryRemove(guildId, out _);
    _currentMessages.TryRemove(guildId, out _);
}

// ... rest of existing code ...
```

### 4.5 Enhance PlaybackService Instrumentation

**Location:** `src/DiscordBot.Bot/Services/PlaybackService.cs`

**Changes to `StreamAudioAsync` method:**

```csharp
private async Task<(bool Success, bool FilterFailed, bool WasCancelled)> StreamAudioAsync(
    ulong guildId,
    Sound sound,
    string filePath,
    string ffmpegPath,
    AudioFilter filter,
    Stream discord,
    double durationSeconds,
    CancellationToken cancellationToken)
{
    var ffmpegArguments = BuildFfmpegArguments(filePath, filter);
    _logger.LogDebug("FFmpeg arguments: {Arguments}", ffmpegArguments);

    // Start activity for FFmpeg transcode
    using var transcodeActivity = BotActivitySource.StartFfmpegTranscodeActivity(
        soundName: sound.Name,
        filePath: Path.GetFileName(filePath), // Relative path only
        filter: filter.ToString());

    var startInfo = new ProcessStartInfo
    {
        FileName = ffmpegPath,
        Arguments = ffmpegArguments,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    using var ffmpeg = Process.Start(startInfo)
        ?? throw new InvalidOperationException($"Failed to start FFmpeg process from '{ffmpegPath}'");

    _logger.LogDebug("FFmpeg process started (PID: {ProcessId}) for sound {SoundName} in guild {GuildId} with filter {Filter}",
        ffmpeg.Id, sound.Name, guildId, filter);

    // Record FFmpeg process ID
    transcodeActivity?.SetTag(TracingConstants.Attributes.FfmpegProcessId, ffmpeg.Id);

    const int bufferSize = 3840; // 20ms of audio at 48kHz stereo 16-bit
    var buffer = new byte[bufferSize];
    int bytesRead;
    long totalBytesRead = 0;
    var wasCancelled = false;

    var playbackStartTime = Stopwatch.GetTimestamp();
    var lastProgressBroadcast = playbackStartTime;
    const long progressBroadcastIntervalTicks = TimeSpan.TicksPerSecond;

    // Start child activity for audio streaming
    using var streamActivity = BotActivitySource.StartSoundboardStreamActivity(
        guildId: guildId,
        soundId: sound.Id);

    try
    {
        int bufferCount = 0;

        while ((bytesRead = await ffmpeg.StandardOutput.BaseStream.ReadAsync(buffer, 0, bufferSize, cancellationToken)) > 0)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Playback cancelled for sound {SoundName} in guild {GuildId}", sound.Name, guildId);
                wasCancelled = true;
                break;
            }

            await discord.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            totalBytesRead += bytesRead;
            bufferCount++;

            var currentTime = Stopwatch.GetTimestamp();
            var elapsedSinceLastBroadcast = currentTime - lastProgressBroadcast;
            if (elapsedSinceLastBroadcast >= progressBroadcastIntervalTicks && durationSeconds > 0)
            {
                var elapsedTotalSeconds = Stopwatch.GetElapsedTime(playbackStartTime).TotalSeconds;
                var positionSeconds = Math.Min(elapsedTotalSeconds, durationSeconds);

                _ = _audioNotifier.NotifyPlaybackProgressAsync(
                    guildId, sound.Id, positionSeconds, durationSeconds, cancellationToken);

                lastProgressBroadcast = currentTime;
            }
        }

        await discord.FlushAsync(cancellationToken);

        // Record streaming metrics
        BotActivitySource.RecordAudioStreamMetrics(
            streamActivity,
            bytesWritten: totalBytesRead,
            bufferCount: bufferCount);

        BotActivitySource.SetSuccess(streamActivity);
    }
    catch (OperationCanceledException)
    {
        wasCancelled = true;
        BotActivitySource.RecordException(streamActivity, new OperationCanceledException("Playback cancelled"));
        throw;
    }
    catch (Exception ex)
    {
        BotActivitySource.RecordException(streamActivity, ex);
        throw;
    }
    finally
    {
        if (!ffmpeg.HasExited)
        {
            ffmpeg.Kill();
        }
    }

    // Check for FFmpeg errors
    var errorOutput = await ffmpeg.StandardError.ReadToEndAsync();
    var hasError = !string.IsNullOrWhiteSpace(errorOutput) || ffmpeg.ExitCode != 0;

    // Record FFmpeg completion
    BotActivitySource.RecordFfmpegDetails(
        transcodeActivity,
        processId: ffmpeg.Id,
        exitCode: ffmpeg.ExitCode,
        arguments: ffmpegArguments);

    if (hasError)
    {
        _logger.LogWarning("FFmpeg errors for sound {SoundName} in guild {GuildId} (exit code {ExitCode}): {ErrorOutput}",
            sound.Name, guildId, ffmpeg.ExitCode, errorOutput);

        transcodeActivity?.SetTag("ffmpeg.error_output", errorOutput.Length > 256 ? errorOutput.Substring(0, 256) : errorOutput);

        // If we got very little data and had a filter, it's likely the filter caused the failure
        var filterFailed = filter != AudioFilter.None && totalBytesRead < bufferSize * 10; // Less than ~200ms of audio

        if (filterFailed)
        {
            transcodeActivity?.SetTag("ffmpeg.filter_failed", true);
        }

        BotActivitySource.SetSuccess(transcodeActivity); // Mark as handled
        return (false, filterFailed, wasCancelled);
    }

    BotActivitySource.SetSuccess(transcodeActivity);
    return (true, false, wasCancelled);
}
```

### 4.6 Enhance AudioService Voice Connection Spans

**Location:** `src/DiscordBot.Bot/Services/AudioService.cs`

**Changes to `JoinChannelAsync` method:**

The existing span from `BotActivitySource.StartServiceActivity` already exists. Enhance it with voice-specific attributes:

```csharp
public async Task<IAudioClient?> JoinChannelAsync(ulong guildId, ulong voiceChannelId, CancellationToken cancellationToken = default)
{
    using var activity = BotActivitySource.StartServiceActivity(
        "audio",
        "join_channel",
        guildId: guildId);

    var guildLock = _guildLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
    await guildLock.WaitAsync(cancellationToken);

    try
    {
        _logger.LogInformation("Attempting to join voice channel {ChannelId} in guild {GuildId}", voiceChannelId, guildId);

        // Check if already connected to the same channel
        if (_connections.TryGetValue(guildId, out var existingConnection))
        {
            if (existingConnection.ChannelId == voiceChannelId)
            {
                _logger.LogInformation("Already connected to voice channel {ChannelId} in guild {GuildId}", voiceChannelId, guildId);

                // Add voice channel attributes
                activity?.SetTag(TracingConstants.Attributes.VoiceChannelId, voiceChannelId.ToString());

                BotActivitySource.SetSuccess(activity);
                return existingConnection.AudioClient;
            }

            // Connected to different channel - disconnect first
            _logger.LogInformation("Disconnecting from voice channel {OldChannelId} before joining {NewChannelId} in guild {GuildId}",
                existingConnection.ChannelId, voiceChannelId, guildId);
            await DisconnectInternalAsync(guildId);
        }

        // Get guild and voice channel
        var guild = _client.GetGuild(guildId);
        if (guild == null)
        {
            _logger.LogWarning("Guild {GuildId} not found, cannot join voice channel", guildId);
            BotActivitySource.SetSuccess(activity);
            return null;
        }

        var voiceChannel = guild.GetVoiceChannel(voiceChannelId);
        if (voiceChannel == null)
        {
            _logger.LogWarning("Voice channel {ChannelId} not found in guild {GuildId}", voiceChannelId, guildId);
            BotActivitySource.SetSuccess(activity);
            return null;
        }

        // Add voice channel attributes
        activity?.SetTag(TracingConstants.Attributes.VoiceChannelId, voiceChannelId.ToString());
        activity?.SetTag(TracingConstants.Attributes.VoiceChannelName, voiceChannel.Name);

        // Connect to voice channel
        var audioClient = await voiceChannel.ConnectAsync();

        // Store connection info
        var now = DateTime.UtcNow;
        var connectionInfo = new VoiceConnectionInfo(audioClient, voiceChannelId, now, now);
        _connections[guildId] = connectionInfo;

        // Add connection timestamp
        activity?.SetTag(TracingConstants.Attributes.VoiceConnectedAt, now.ToString("O"));

        _logger.LogInformation("Successfully joined voice channel {ChannelId} ({ChannelName}) in guild {GuildId}",
            voiceChannelId, voiceChannel.Name, guildId);

        // Broadcast AudioConnected event to subscribed clients
        _ = _audioNotifier.NotifyAudioConnectedAsync(guildId, voiceChannelId, voiceChannel.Name, cancellationToken);

        BotActivitySource.SetSuccess(activity);
        return audioClient;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error joining voice channel {ChannelId} in guild {GuildId}", voiceChannelId, guildId);
        BotActivitySource.RecordException(activity, ex);
        throw;
    }
    finally
    {
        guildLock.Release();
    }
}
```

**Changes to `LeaveChannelAsync` method:**

```csharp
public async Task<bool> LeaveChannelAsync(ulong guildId, CancellationToken cancellationToken = default)
{
    using var activity = BotActivitySource.StartServiceActivity(
        "audio",
        "leave_channel",
        guildId: guildId);

    var guildLock = _guildLocks.GetOrAdd(guildId, _ => new SemaphoreSlim(1, 1));
    await guildLock.WaitAsync(cancellationToken);

    try
    {
        if (!_connections.TryGetValue(guildId, out var connection))
        {
            _logger.LogDebug("Not connected to any voice channel in guild {GuildId}", guildId);
            BotActivitySource.SetSuccess(activity);
            return false;
        }

        _logger.LogInformation("Leaving voice channel {ChannelId} in guild {GuildId}", connection.ChannelId, guildId);

        // Add voice channel attributes
        activity?.SetTag(TracingConstants.Attributes.VoiceChannelId, connection.ChannelId.ToString());

        // Calculate connection duration
        var connectionDuration = DateTime.UtcNow - connection.ConnectedAt;
        activity?.SetTag("voice.connection_duration_seconds", connectionDuration.TotalSeconds);

        await DisconnectInternalAsync(guildId);

        _logger.LogInformation("Successfully left voice channel in guild {GuildId}", guildId);

        // Broadcast AudioDisconnected event to subscribed clients
        _ = _audioNotifier.NotifyAudioDisconnectedAsync(guildId, "User requested disconnect", cancellationToken);

        BotActivitySource.SetSuccess(activity);
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error leaving voice channel in guild {GuildId}", guildId);
        BotActivitySource.RecordException(activity, ex);
        throw;
    }
    finally
    {
        guildLock.Release();
    }
}
```

---

## 5. Timeline / Dependency Map

```
Phase 1: Tracing Constants and Helper Methods (2 hours)
├── 5.1 Update TracingConstants.cs (30 min)
├── 5.2 Add helper methods to BotActivitySource.cs (1.5 hours)
└── Compile and verify no errors

Phase 2: TTS Service Instrumentation (2 hours)
├── 5.3 Instrument AzureTtsService.SynthesizeSpeechAsync (1 hour)
├── 5.4 Instrument AzureTtsService.ConvertMonoToStereo (30 min)
└── 5.5 Instrument AzureTtsService.GetAvailableVoicesAsync (30 min)

Phase 3: Controller and Audio Streaming (1.5 hours)
├── 5.6 Instrument PortalTtsController.SendTts streaming section (1 hour)
└── 5.7 Test TTS flow end-to-end with traces (30 min)

Phase 4: Soundboard Enhancement (2 hours)
├── 5.8 Enhance PlaybackService.StreamAudioAsync (1.5 hours)
└── 5.9 Test soundboard playback with traces (30 min)

Phase 5: Voice Connection Enhancement (1 hour)
├── 5.10 Enhance AudioService.JoinChannelAsync (30 min)
└── 5.11 Enhance AudioService.LeaveChannelAsync (30 min)

Phase 6: Testing and Validation (1.5 hours)
├── 5.12 Manual testing checklist (1 hour)
└── 5.13 Documentation updates (30 min)
```

**Parallelization Opportunities:**
- Phases 2 and 4 can be developed in parallel (different services)
- Phase 5 can start after Phase 1 completes
- Testing in Phase 6 should cover all previous phases

---

## 6. Acceptance Criteria

### 6.1 Azure Speech Tracing

- [ ] `azure_speech.synthesize` span created for TTS synthesis
- [ ] Span includes text length, voice name, and region
- [ ] Audio size recorded on successful synthesis
- [ ] Cancellation reason recorded on failure
- [ ] Exceptions are properly recorded with context

### 6.2 Voice Retrieval Tracing

- [ ] `azure_speech.get_voices` span created for voice list fetch
- [ ] Voice count recorded on success
- [ ] Cache hits bypass tracing (no unnecessary spans)

### 6.3 Audio Conversion Tracing

- [ ] `tts.audio_convert` span created for mono-to-stereo conversion
- [ ] Input and output byte counts recorded
- [ ] Format names included as attributes

### 6.4 TTS Streaming Tracing

- [ ] `discord.audio_stream` span created for PCM writes
- [ ] Duration and bytes written recorded
- [ ] Buffer count tracked
- [ ] Span is child of HTTP request span

### 6.5 Soundboard Tracing

- [ ] `soundboard.ffmpeg_transcode` span wraps FFmpeg process
- [ ] FFmpeg process ID and exit code recorded
- [ ] `soundboard.audio_stream` child span tracks PCM writes
- [ ] Filter application and fallback recorded
- [ ] Sound metadata (name, file path, duration) included

### 6.6 Voice Connection Tracing

- [ ] Voice channel ID and name recorded in join span
- [ ] Connection timestamp recorded
- [ ] Connection duration calculated on leave
- [ ] Spans are children of service operation spans

### 6.7 Span Hierarchy

- [ ] TTS spans correctly parent to HTTP request
- [ ] Audio conversion is child of synthesis span
- [ ] Streaming spans are siblings to synthesis
- [ ] FFmpeg transcode contains audio stream child
- [ ] All voice spans have guild ID attribute

### 6.8 Performance

- [ ] No noticeable performance degradation from tracing
- [ ] Spans do not block audio streaming
- [ ] Activity creation overhead is minimal

---

## 7. Testing Strategy

### 7.1 Manual Testing Checklist

**TTS Portal Flow:**
- [ ] Send TTS message through portal and verify trace appears
- [ ] Check `azure_speech.synthesize` span shows ~10s duration
- [ ] Verify `tts.audio_convert` span shows conversion time
- [ ] Verify `discord.audio_stream` span shows streaming time
- [ ] Check all attributes are populated correctly
- [ ] Trigger Azure Speech error and verify cancellation_reason

**Soundboard Flow:**
- [ ] Play sound without filter and verify trace
- [ ] Play sound with bass_boost filter and verify filter attribute
- [ ] Verify `soundboard.ffmpeg_transcode` contains process details
- [ ] Verify `soundboard.audio_stream` tracks bytes written
- [ ] Check FFmpeg exit code is recorded
- [ ] Trigger filter fallback and verify both attempts traced

**Voice Connection Flow:**
- [ ] Join voice channel and verify attributes
- [ ] Check channel name and ID are recorded
- [ ] Leave channel and verify connection duration calculated
- [ ] Verify timestamps are in ISO 8601 format

**Trace Hierarchy:**
- [ ] Verify parent-child relationships in Jaeger UI
- [ ] Check correlation IDs link logs to traces
- [ ] Verify trace IDs propagate through entire flow
- [ ] Confirm attributes are visible in trace viewer

### 7.2 Integration Testing

```csharp
// Example integration test
[Fact]
public async Task TtsSynthesis_CreatesTraceWithChildSpans()
{
    // Arrange
    var exportedActivities = new List<Activity>();
    using var tracerProvider = Sdk.CreateTracerProviderBuilder()
        .AddSource(BotActivitySource.SourceName)
        .AddInMemoryExporter(exportedActivities)
        .Build();

    // Act - Simulate TTS synthesis
    using var synthesisActivity = BotActivitySource.StartAzureSpeechActivity(
        textLength: 100,
        voice: "en-US-JennyNeural",
        region: "eastus");

    using var conversionActivity = BotActivitySource.StartAudioConversionActivity(
        fromFormat: "mono",
        toFormat: "stereo",
        bytesIn: 96000);

    BotActivitySource.SetSuccess(conversionActivity);
    BotActivitySource.SetSuccess(synthesisActivity);

    // Force export
    tracerProvider.ForceFlush();

    // Assert
    Assert.Equal(2, exportedActivities.Count);

    var synthesisSpan = exportedActivities.First(a => a.OperationName == "azure_speech.synthesize");
    var conversionSpan = exportedActivities.First(a => a.OperationName == "tts.audio_convert");

    Assert.Equal(synthesisSpan.TraceId, conversionSpan.TraceId);
    Assert.Equal(synthesisSpan.SpanId, conversionSpan.ParentSpanId);
    Assert.Equal("100", synthesisSpan.GetTagItem("tts.text_length"));
}
```

---

## 8. Risks and Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| TTS text content in traces | Low | High | Never include full text; only length |
| Large audio spans affecting performance | Low | Low | Spans are async and don't block I/O |
| FFmpeg process tracking overhead | Low | Low | Process ID lookup is minimal |
| File paths revealing system structure | Low | Medium | Use relative paths only |
| Trace volume increase | Medium | Medium | Existing sampling (10% prod) applies |
| Activity disposal in streaming loops | Medium | High | Use `using` statements consistently |

---

## 9. File Summary

### Files to Modify

| File | Changes | Lines Modified |
|------|---------|----------------|
| `src/DiscordBot.Bot/Tracing/TracingConstants.cs` | Add TTS/audio attributes and span names | ~40 new lines |
| `src/DiscordBot.Bot/Tracing/BotActivitySource.cs` | Add 6 new helper methods | ~120 new lines |
| `src/DiscordBot.Bot/Services/AzureTtsService.cs` | Instrument 3 methods | ~50 lines changed |
| `src/DiscordBot.Bot/Controllers/PortalTtsController.cs` | Instrument streaming section | ~30 lines changed |
| `src/DiscordBot.Bot/Services/PlaybackService.cs` | Enhance `StreamAudioAsync` | ~60 lines changed |
| `src/DiscordBot.Bot/Services/AudioService.cs` | Enhance voice connection spans | ~30 lines changed |

**Total estimated changes:** ~330 lines (mostly additions)

---

## 10. Example Trace Output

When viewing a TTS trace in Jaeger, you should see:

```
POST PortalTts/SendTts [11.2s]
├── http.method: POST
├── http.route: /api/portal/tts/{guildId}/send
├── correlation.id: a1b2c3d4e5f6g7h8
│
├── db.select GuildSettings [2ms]
│   └── ... (existing database span)
│
├── azure_speech.synthesize [10.5s]
│   ├── tts.text_length: 256
│   ├── tts.voice: en-US-JennyNeural
│   ├── tts.region: eastus
│   ├── tts.speed: 1.0
│   ├── tts.pitch: 1.0
│   ├── tts.volume: 1.0
│   ├── tts.audio_size_bytes: 1008000
│   ├── tts.synthesis_result: SynthesizingAudioCompleted
│   │
│   └── tts.audio_convert [45ms]
│       ├── audio.format_from: mono_48khz_16bit
│       ├── audio.format_to: stereo_48khz_16bit
│       ├── audio.bytes_in: 1008000
│       └── audio.bytes_out: 2016000
│
├── discord.audio_stream [5.2s]
│   ├── discord.guild.id: 123456789012345678
│   ├── audio.duration_seconds: 5.2
│   ├── audio.bytes_written: 2016000
│   └── audio.buffer_count: 525
│
└── db.insert TtsMessage [3ms]
    └── ... (existing database span)
```

When viewing a soundboard trace:

```
service.playback.play [2.8s]
├── sound.id: a1b2c3d4-...
├── sound.name: airhorn
├── playback.queue_enabled: false
├── playback.filter: bass_boost
│
└── soundboard.ffmpeg_transcode [2.8s]
    ├── sound.file_path: airhorn.mp3
    ├── audio.filter: bass_boost
    ├── ffmpeg.process_id: 12345
    ├── ffmpeg.exit_code: 0
    ├── ffmpeg.arguments: -hide_banner -loglevel warning -i "..." -af "bass=..." -ac 2 -f s16le -ar 48000 pipe:1
    │
    └── soundboard.audio_stream [2.7s]
        ├── discord.guild.id: 123456789012345678
        ├── sound.id: a1b2c3d4-...
        ├── audio.bytes_streamed: 1036800
        └── audio.buffer_count: 270
```

---

*Document prepared by: Systems Architect Agent*
*Review status: Ready for implementation*
