# Bridge OTel Activities to Elastic APM Spans for Portal Audio Playback

**Status:** Planned
**Date:** 2026-02-21

## Context

Portal audio playback (soundboard, TTS, VOX) triggers Discord playback via HTTP API endpoints. These HTTP requests are auto-instrumented by `UseAllElasticApm` as APM transactions, but the **internal service spans** (PlaybackService, VoxService, AzureTtsService) only create OpenTelemetry Activities — they never appear in Kibana. The `EnrichCurrentApmTransaction`/`EnrichCurrentApmSpan` helpers exist in `BotActivitySource` but are never called.

The proven bridging pattern exists in `DiscordApiTracingHandler.cs`: call `Agent.Tracer.CurrentTransaction?.StartSpan(...)` to add child APM spans under the auto-instrumented HTTP transaction.

## Approach

Create a `ServiceActivityScope` (like the existing `BackgroundServiceActivityScope` but for APM **spans** instead of transactions), add a `StartServiceActivityWithApm` helper, and use it at key points in the playback pipeline.

---

## Changes

### 1. Add `ServiceActivityScope` to `BotActivitySource.cs`

New class alongside `BackgroundServiceActivityScope`:

```csharp
public sealed class ServiceActivityScope : IDisposable
{
    public Activity? Activity { get; }
    public ISpan? ApmSpan { get; }

    public ServiceActivityScope(Activity? activity, ISpan? apmSpan)
    {
        Activity = activity;
        ApmSpan = apmSpan;
    }

    public void SetSuccess()
    {
        if (ApmSpan != null) ApmSpan.Outcome = Outcome.Success;
    }

    public void RecordException(Exception ex)
    {
        Activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        Activity?.RecordException(ex);
        ApmSpan?.CaptureException(ex);
        if (ApmSpan != null) ApmSpan.Outcome = Outcome.Failure;
    }

    public void Dispose()
    {
        Activity?.Dispose();
        ApmSpan?.End();  // CRITICAL: null-conditional — CurrentTransaction may be null
    }
}
```

**Key design decisions (from observability review):**

- Pairs `Activity` + `ISpan` (not `ITransaction`) — portal requests already have an HTTP transaction from APM middleware.
- All APM object access uses null-conditional — `CurrentTransaction` can be null when APM is disabled, sampling drops the request, or the path is filtered.

### 2. Add `StartServiceActivityWithApm` to `BotActivitySource`

New static method:

```csharp
public static ServiceActivityScope StartServiceActivityWithApm(
    string operationName,
    string serviceName,
    ActivityKind kind = ActivityKind.Internal,
    IEnumerable<KeyValuePair<string, object?>>? tags = null)
{
    var activity = StartServiceActivity(operationName, serviceName, kind, tags);

    // APM span type is "app" (internal logic), NOT "external"
    var apmSpan = Agent.Tracer.CurrentTransaction?.StartSpan(
        operationName,
        "app",      // type — internal application logic
        "audio");   // subtype

    // Copy tags to APM labels
    if (apmSpan != null && tags != null)
    {
        foreach (var tag in tags)
        {
            if (tag.Value != null)
                apmSpan.SetLabel(tag.Key, tag.Value.ToString());
        }
    }

    return new ServiceActivityScope(activity, apmSpan);
}
```

**Important (from observability review):** APM span type must be `"app"` with subtype `"audio"`, not `ApiConstants.TypeExternal`. Using `"external"` would misclassify internal audio operations as outbound dependencies in the Kibana service map.

Add similar wrappers for audio-specific factory methods (`StartSoundboardStreamActivity`, `StartFfmpegTranscodeActivity`, `StartAzureSpeechActivity`, etc.) that return `ServiceActivityScope` instead of raw `Activity?`.

### 3. Update `PlaybackService.cs` — Replace Key Spans

Replace `StartServiceActivity` with `StartServiceActivityWithApm` at the **four key entry points**:

| Method | Line | Span Name |
|--------|------|-----------|
| `PlayAsync` | ~68 | `playback/play` |
| `PlaySoundAsync` | ~417 | `playback/play_sound` |
| `StopAsync` | ~168 | `playback/stop` |
| `RemoveFromQueueAsync` | ~240 | `playback/remove_from_queue` |

**Additional entry point (from audio review):** `PlaybackLoopAsync` (line ~324) — this fire-and-forget background loop needs an APM **transaction** (not span) to parent all `PlaySoundAsync` child spans. Without it, those spans are orphaned. Use the existing `BackgroundServiceActivityScope` pattern here since there is no ambient HTTP transaction.

Bridge the FFmpeg/stream spans:
- `StreamFromCacheAsync` — `StartSoundboardStreamActivity` -> add APM span
- `StreamFromFfmpegAsync` — `StartFfmpegTranscodeActivity` + `StartSoundboardStreamActivity` -> add APM spans

**Performance note (from audio review):** All spans wrap streaming sessions (one per play operation), never individual 20ms buffer writes. The existing Activity structure already follows correct granularity — no hot path concern.

### 4. Update `VoxService.cs` — Bridge VOX Spans

VoxService uses its own `ActivitySource("DiscordBot.Vox")` with raw `StartActivity` calls. Add APM child spans alongside existing Activities for:

| Span | Assessment |
|------|-----------|
| `VoxCommand` (top-level) | Correct |
| `Tokenization` | Fast, no hot path concern |
| `ClipLookup` | Fast dictionary lookup, no concern |
| `Playback` | Correctly wraps streaming |

**Additional span (from audio review):** `StreamPcmToDiscordAsync` (line ~426) — has no OTel Activity at all. Add both an OTel Activity and an APM companion span for consistency with `TtsPlaybackService.StartDiscordAudioStreamActivity` and `PlaybackService.StartSoundboardStreamActivity`.

**Structural note:** VoxService's separate `ActivitySource("DiscordBot.Vox")` means APM child spans only work if `CurrentTransaction` propagates from the calling context (portal controller HTTP transaction). This is fine for portal paths. For VOX slash commands (no HTTP transaction), spans will silently be null — acceptable behavior.

Also register `"DiscordBot.Vox"` in `OpenTelemetryExtensions.cs` `AddSource(...)` so OTel also captures it.

> **Alternative consideration (from observability review):** VoxService could migrate to using `BotActivitySource.Source` directly (`"DiscordBot.Bot"`) to eliminate the orphaned private source entirely. This is a cleaner long-term fix but out of scope for this change.

### 5. Update TTS Service Spans

**`AzureTtsService`** — Add companion APM spans alongside existing Activities:

| Activity | Purpose |
|----------|---------|
| `StartAzureSpeechActivity` (line ~176) | Azure network call — high value for latency measurement |
| `StartAudioConversionActivity` (line ~486) | Synchronous CPU work, bounded |
| `StartGetVoicesActivity` (line ~358) | Azure network call — **added per audio review**, omitted from original plan |

**`TtsPlaybackService`** — Two changes:

1. `StartDiscordAudioStreamActivity` (line ~74) — add companion APM span.
2. **Add outer span at `PlayAsync` level (line ~36)** — per audio review, the streaming APM span will be orphaned without a parent span wrapping the full TTS playback operation including history logging.

### 6. Controller-Level APM Transaction Enrichment

Add `Agent.Tracer.CurrentTransaction?.SetLabel(...)` calls to tag the HTTP transaction with audio-specific context. Labels are set at the point where data becomes available.

#### `PortalSoundboardController.PlaySound`

```csharp
// Set early — available on entry
Agent.Tracer.CurrentTransaction?.SetLabel("guild_id", guildId.ToString());

// Set after result check (line ~254) — sound name available here
Agent.Tracer.CurrentTransaction?.SetLabel("sound_name", result.Sound!.Name);
Agent.Tracer.CurrentTransaction?.SetLabel("sound_id", soundId.ToString());
```

#### `PortalTtsController.SendTts`

```csharp
Agent.Tracer.CurrentTransaction?.SetLabel("guild_id", guildId.ToString());
Agent.Tracer.CurrentTransaction?.SetLabel("voice", request.Voice);
Agent.Tracer.CurrentTransaction?.SetLabel("text_length", request.Message.Length);
Agent.Tracer.CurrentTransaction?.SetLabel("synthesis_mode",
    !string.IsNullOrWhiteSpace(request.Ssml) ? "ssml" :
    !string.IsNullOrWhiteSpace(request.Style) ? "styled" : "standard");
```

#### `PortalTtsController.SynthesizeSsml`

**Corrected (from web-ui review):** `SsmlSynthesisRequest` has no `Voice` field. The original plan was wrong here.

```csharp
// Set early — available on entry
Agent.Tracer.CurrentTransaction?.SetLabel("guild_id", guildId.ToString());
Agent.Tracer.CurrentTransaction?.SetLabel("ssml_length", request.Ssml.Length);
Agent.Tracer.CurrentTransaction?.SetLabel("play_in_voice_channel", request.PlayInVoiceChannel);

// Set after validation (line ~677) — DetectedVoices now populated
Agent.Tracer.CurrentTransaction?.SetLabel("voice_count", validationResult.DetectedVoices.Count);
```

#### `PortalVoxController.Play`

**Corrected (from web-ui review):** `message` label dropped — high-cardinality free text with PII risk. Replaced with safe numeric metrics.

```csharp
// After validation, before playback
Agent.Tracer.CurrentTransaction?.SetLabel("guild_id", guildId.ToString());
Agent.Tracer.CurrentTransaction?.SetLabel("clip_group", clipGroup.ToString().ToLowerInvariant());
Agent.Tracer.CurrentTransaction?.SetLabel("message_length", request.Message?.Length ?? 0);
Agent.Tracer.CurrentTransaction?.SetLabel("word_gap_ms", wordGapMs);

// After TokenizePreview (line ~333)
Agent.Tracer.CurrentTransaction?.SetLabel("matched_clips", preview.MatchedCount);
Agent.Tracer.CurrentTransaction?.SetLabel("skipped_words", preview.SkippedCount);
```

---

## Files Modified

| File | Change |
|------|--------|
| `src/DiscordBot.Bot/Tracing/BotActivitySource.cs` | Add `ServiceActivityScope` class + `StartServiceActivityWithApm` + audio span wrappers |
| `src/DiscordBot.Bot/Services/PlaybackService.cs` | Replace `StartServiceActivity` at 4 entry points + add APM transaction to `PlaybackLoopAsync` + bridge stream/transcode spans |
| `src/DiscordBot.Bot/Services/VoxService.cs` | Add APM child spans alongside existing Activities + add OTel Activity to `StreamPcmToDiscordAsync` |
| `src/DiscordBot.Infrastructure/Services/AzureTtsService.cs` | Add APM companion spans to `StartAzureSpeechActivity`, `StartAudioConversionActivity`, `StartGetVoicesActivity` |
| `src/DiscordBot.Bot/Services/TtsPlaybackService.cs` | Add outer span at `PlayAsync` level + APM companion to `StartDiscordAudioStreamActivity` |
| `src/DiscordBot.Bot/Controllers/PortalSoundboardController.cs` | Enrich APM transaction with sound metadata labels |
| `src/DiscordBot.Bot/Controllers/PortalTtsController.cs` | Enrich APM transaction with TTS metadata labels (corrected for SynthesizeSsml) |
| `src/DiscordBot.Bot/Controllers/PortalVoxController.cs` | Enrich APM transaction with VOX metadata labels (safe metrics, no PII) |
| `src/DiscordBot.Bot/Extensions/OpenTelemetryExtensions.cs` | Add `tracing.AddSource("DiscordBot.Vox")` |

---

## Pre-Implementation Checklist

- [ ] Confirm `Elastic.Apm` NuGet version >= 1.9 (required for typed `SetLabel` overloads with int/bool)
- [ ] Verify `Agent.Tracer` using directive is available in all modified files (`using Elastic.Apm;` or `using Elastic.Apm.Api;`)

---

## Expected Kibana APM Traces After Implementation

### Portal Soundboard Play

```
POST /api/portal/soundboard/{guildId}/play/{soundId}   [auto HTTP transaction]
  Labels: guild_id, sound_name, sound_id
  ├── playback/play                                     [APM span - NEW, type: app/audio]
  │   └── playback/play_sound                           [APM span - NEW, type: app/audio]
  │       ├── soundboard.ffmpeg.transcode               [APM span - NEW, type: app/audio]
  │       └── soundboard.audio.stream                   [APM span - NEW, type: app/audio]
  └── Discord API POST /channels/{id}/...               [existing APM span, type: external/http]
```

### Portal TTS Send

```
POST /api/portal/tts/{guildId}/send                     [auto HTTP transaction]
  Labels: guild_id, voice, text_length, synthesis_mode
  ├── tts.playback                                      [APM span - NEW, type: app/audio]
  │   ├── azure.speech.synthesize                       [APM span - NEW, type: app/audio]
  │   ├── tts.audio.convert                             [APM span - NEW, type: app/audio]
  │   └── discord.audio.stream                          [APM span - NEW, type: app/audio]
  └── Discord API POST /channels/{id}/...               [existing APM span]
```

### Portal TTS SynthesizeSsml

```
POST /api/portal/tts/{guildId}/synthesize-ssml          [auto HTTP transaction]
  Labels: guild_id, ssml_length, play_in_voice_channel, voice_count
  ├── azure.speech.synthesize                           [APM span - NEW]
  └── tts.audio.convert                                 [APM span - NEW]
```

### Portal VOX Play

```
POST /api/portal/vox/{guildId}/play                     [auto HTTP transaction]
  Labels: guild_id, clip_group, message_length, word_gap_ms, matched_clips, skipped_words
  └── VoxCommand                                        [APM span - NEW, type: app/audio]
      ├── Tokenization                                  [APM span - NEW]
      ├── ClipLookup                                    [APM span - NEW]
      └── Playback                                      [APM span - NEW]
          └── vox.audio.stream                          [APM span - NEW]
```

### Background Playback Loop (non-portal, e.g. Discord command)

```
PlaybackLoop                                            [APM transaction - NEW]
  └── playback/play_sound                               [APM span - NEW]
      ├── soundboard.ffmpeg.transcode                   [APM span - NEW]
      └── soundboard.audio.stream                       [APM span - NEW]
```

---

## Verification

1. `dotnet build` — clean compilation
2. `dotnet test` — existing tracing tests pass
3. Run locally with Elastic APM enabled, trigger each path from portal UI:
   - Play a soundboard clip -> check Kibana APM for child spans under the HTTP transaction
   - Send a TTS message -> verify `tts.playback` parent + `azure.speech.synthesize` + `discord.audio.stream` spans
   - Send a TTS SSML synthesis -> verify labels use corrected fields (no `voice`, uses `ssml_length`/`voice_count`)
   - Play a VOX message -> verify `VoxCommand` + child spans including `vox.audio.stream`
4. Verify the auto-instrumented HTTP transaction still appears and now has enrichment labels
5. Verify with APM disabled (`ElasticApm:Enabled = false`) — no NullReferenceExceptions, OTel Activities still work independently
6. Trigger playback via Discord slash command (no HTTP transaction) — verify spans are silently dropped without errors
