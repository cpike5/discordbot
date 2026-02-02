# VOX System Specification

**Version:** 1.0
**Date:** 2026-02-02
**Status:** Design Document
**Target Framework:** .NET 8, Discord.Net, Azure Cognitive Services Speech

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Data Model](#data-model)
4. [Word Bank System](#word-bank-system)
5. [Audio Pipeline](#audio-pipeline)
6. [Slash Commands](#slash-commands)
7. [Configuration](#configuration)
8. [Dependency Injection](#dependency-injection)
9. [Database Schema](#database-schema)
10. [API Endpoints](#api-endpoints)
11. [Security & Rate Limiting](#security--rate-limiting)
12. [Cost Management](#cost-management)
13. [Related Documentation](#related-documentation)

---

## Overview

### What is VOX?

VOX is a Half-Life-style concatenated word-clip announcement system for Discord bots. Instead of synthesizing full sentences with natural prosody (like traditional TTS), VOX:

1. **Parses input text** into individual word tokens
2. **Generates/retrieves word clips** via Azure TTS, cached in a per-guild word bank
3. **Concatenates clips** with configurable silence gaps between words
4. **Applies PA-system audio filters** (bandpass, compression, distortion) via FFmpeg
5. **Streams the result** to Discord voice channels with the iconic robotic, word-by-word announcer sound

### How VOX Differs from TTS

| Feature | TTS Portal | VOX Portal |
|---------|-----------|-----------|
| **Input Mode** | Free-form text | Words parsed into tokens; two input modes (free text & sentence builder) |
| **Generation Strategy** | Real-time synthesis per message | Word-by-word with persistent cache |
| **Audio Output** | Smooth, natural speech | Robotic, concatenated word clips |
| **Audio Processing** | None (or basic volume) | PA-system filters (bandpass, compression, distortion) |
| **Reusability** | No caching (regenerated each time) | Full word bank with persistent cache (generate once, play many) |
| **Cost Impact** | Higher (synthesized per use) | Lower (amortized across uses) |
| **Visual Feedback** | Character counter | Token preview strip with cache status (green/orange/red) |

### Key Capabilities

- **Free Text Mode** - Type natural text; VOX automatically tokenizes it into words
- **Sentence Builder Mode** - Drag-and-drop composition of predefined word clips; add manual pauses between words
- **Word Bank Caching** - Persistent per-guild cache of individual word clips (stored on disk, tracked in database)
- **Predefined Word Packs** - Common English 500, NATO Phonetic, Numbers 0-100, Half-Life Classic VOX phrases
- **PA Filter Presets** - Off, Light, Heavy (classic Half-Life sound), or Custom with manual filter parameters
- **Word Gap Control** - 20-200ms configurable silence between words
- **Real-time Token Preview** - See which words are cached (green), will be generated (orange), or invalid (red)
- **Bulk Word Generation** - Generate entire word packs at once for cost optimization
- **Import/Export** - Share word banks between guilds as ZIP files

---

## Architecture

### Overview

VOX follows the three-layer clean architecture of the bot:

```
┌─────────────────────────────────────────────────────┐
│ Bot Layer (UI/Orchestration)                        │
├─────────────────────────────────────────────────────┤
│ IVoxService (orchestrator)                          │
│ IVoxWordBankService (cache management)              │
│ IVoxTokenizer (text parsing)                        │
│ IVoxConcatenationService (audio joining)            │
│ IVoxFilterService (audio processing)                │
├─────────────────────────────────────────────────────┤
│ Infrastructure Layer (Data Access)                  │
├─────────────────────────────────────────────────────┤
│ IVoxWordRepository (word bank persistence)          │
│ IVoxMessageRepository (audit log persistence)       │
│ IGuildVoxSettingsRepository (settings persistence)  │
├─────────────────────────────────────────────────────┤
│ Core Layer (Domain Models)                          │
├─────────────────────────────────────────────────────┤
│ Entities: GuildVoxSettings, VoxWord, VoxMessage    │
│ DTOs: VoxOptions, VoxToken, VoxClip, VoxComposition│
│ Enums: PaFilterPreset, TokenStatus                  │
└─────────────────────────────────────────────────────┘
```

### Service Responsibilities

#### IVoxService / VoxService

**Purpose**: Main orchestrator coordinating the entire VOX pipeline.

**Lifetime**: Singleton (manages connection state and caches)

**Key Methods**:

```csharp
public interface IVoxService
{
    /// <summary>
    /// Synthesize VOX audio from free text.
    /// Parses text → tokenizes → looks up/generates word clips → concatenates → filters
    /// </summary>
    Task<byte[]> SynthesizeVoxAsync(
        ulong guildId,
        string text,
        VoxOptions options,
        IProgress<VoxGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synthesize VOX audio from manually composed word sequence.
    /// Uses pre-selected words and pauses (from sentence builder)
    /// </summary>
    Task<byte[]> SynthesizeVoxAsync(
        ulong guildId,
        VoxComposition composition,
        VoxOptions options,
        IProgress<VoxGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current configuration for a guild.
    /// </summary>
    Task<GuildVoxSettings> GetSettingsAsync(ulong guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update configuration for a guild.
    /// </summary>
    Task UpdateSettingsAsync(
        ulong guildId,
        GuildVoxSettings settings,
        CancellationToken cancellationToken = default);
}
```

**Responsibilities**:
- Coordinate text parsing → word lookup → **parallel generation** → concatenation → filtering
- Dispatch uncached words to `IVoxWordBankService` for parallel synthesis before concatenation
- Validate input and rate limits
- Log VOX messages to audit trail
- Handle partial generation failures gracefully (skip failed words, continue with available clips)
- Implement progress reporting for multi-word generation

---

#### IVoxWordBankService / VoxWordBankService

**Purpose**: Manages cached word clips (lookup, generation, bulk operations, import/export).

**Lifetime**: Singleton (manages disk I/O and database caching)

**Key Methods**:

```csharp
public interface IVoxWordBankService
{
    /// <summary>
    /// Get cached word clip (PCM bytes) if available.
    /// Returns null if not cached.
    /// </summary>
    Task<byte[]?> GetClipAsync(ulong guildId, string word, string voice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate a word clip via Azure TTS and cache it.
    /// </summary>
    Task<byte[]> GenerateClipAsync(
        ulong guildId,
        string word,
        string voice,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk generate multiple words with progress reporting.
    /// Words are synthesized in parallel using SemaphoreSlim to limit
    /// concurrency (default: WordGenerationConcurrency = 3).
    /// </summary>
    Task<BulkGenerateResult> GenerateBulkAsync(
        ulong guildId,
        IEnumerable<string> words,
        string voice,
        IProgress<BulkGenerationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check cache status for a list of words.
    /// Returns dictionary mapping word -> TokenStatus (Cached, WillGenerate, Error)
    /// </summary>
    Task<Dictionary<string, TokenStatus>> GetCacheStatusAsync(
        ulong guildId,
        IEnumerable<string> words,
        string voice,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a single cached word clip.
    /// </summary>
    Task DeleteClipAsync(ulong guildId, Guid wordId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Purge all cached words for a voice (or entire cache if voice is null).
    /// </summary>
    Task<int> PurgeCacheAsync(ulong guildId, string? voice = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get word bank statistics (total words, size, voices used).
    /// </summary>
    Task<WordBankStats> GetStatsAsync(ulong guildId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Import word bank from ZIP file (extracted to disk, added to database).
    /// </summary>
    Task<ImportResult> ImportAsync(ulong guildId, Stream zipStream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Export word bank as ZIP file (all clips for a voice, or all if voice is null).
    /// </summary>
    Task<Stream> ExportAsync(ulong guildId, string? voice = null, CancellationToken cancellationToken = default);
}
```

**Responsibilities**:
- Cache storage at `sounds/vox/{guildId}/{voice}/`
- Database tracking of word metadata (filename, size, duration)
- Azure TTS integration for individual word synthesis
- **Parallel word generation** using `SemaphoreSlim` to throttle concurrent Azure TTS calls
- Import/export ZIP file handling
- Disk space management and cleanup

**Parallel Generation Strategy**:

Both bulk generation and on-demand missing-word generation process words concurrently. A `SemaphoreSlim` (sized to `WordGenerationConcurrency`, default 3) gates Azure TTS calls to avoid rate-limit errors while maximizing throughput.

```csharp
// Parallel generation implementation pattern
public async Task<BulkGenerateResult> GenerateBulkAsync(
    ulong guildId,
    IEnumerable<string> words,
    string voice,
    IProgress<BulkGenerationProgress>? progress = null,
    CancellationToken cancellationToken = default)
{
    var semaphore = new SemaphoreSlim(_options.Value.WordGenerationConcurrency);
    var results = new ConcurrentBag<WordGenerationResult>();

    var tasks = words.Select(async word =>
    {
        await semaphore.WaitAsync(cancellationToken);
        try
        {
            var clip = await GenerateClipAsync(guildId, word, voice, cancellationToken);
            results.Add(new WordGenerationResult(word, Success: true));
            progress?.Report(new BulkGenerationProgress { /* ... */ });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate word '{Word}'", word);
            results.Add(new WordGenerationResult(word, Success: false));
        }
        finally
        {
            semaphore.Release();
        }
    });

    await Task.WhenAll(tasks);
    return new BulkGenerateResult(results);
}
```

The same pattern applies during `SynthesizeVoxAsync` when the tokenizer identifies uncached words -- they are generated in parallel before concatenation begins.

**Word Clip Storage Format**:
- **Location**: `sounds/vox/{guildId}/{voice}/{word-hash}.pcm`
- **Format**: PCM 48kHz, 16-bit, stereo (mono converted to stereo like TTS)
- **Filename**: Derived from word hash to avoid filesystem issues with special characters
- **Metadata**: Stored in database (`VoxWord` entity)

---

#### IVoxTokenizer / VoxTokenizer

**Purpose**: Parse free-form text into word tokens with pause handling.

**Lifetime**: Transient (stateless, thread-safe)

**Key Methods**:

```csharp
public interface IVoxTokenizer
{
    /// <summary>
    /// Tokenize text into words and pause tokens.
    /// Whitespace-separated words, punctuation maps to pauses.
    /// </summary>
    List<VoxToken> Tokenize(string text);
}

public record VoxToken
{
    public string Word { get; init; } = ""; // Word text (lowercase, trimmed)
    public TokenType Type { get; init; } = TokenType.Word;
    public int PauseDurationMs { get; init; } = 0; // For Pause type only
}

public enum TokenType
{
    Word,  // Regular word
    Pause  // Silence gap (period, ellipsis, comma, etc.)
}
```

**Responsibilities**:
- Split input text by whitespace
- Trim and lowercase words
- Detect punctuation and insert pause tokens:
  - `.` (period) = 200ms pause
  - `,` (comma) = 150ms pause
  - `...` (ellipsis) = 250ms pause
  - `-` (dash) = 100ms pause
- Validate words (alphanumeric only, max 30 chars)
- Number-to-word expansion (optional: "123" → "one two three" or keep as-is based on config)
- Handle contractions (e.g., "don't" → "don t" or normalize to "dont")

---

#### IVoxConcatenationService / VoxConcatenationService

**Purpose**: Join multiple word clips into a single audio stream with silence gaps.

**Lifetime**: Singleton (stateless, pure computation)

**Key Methods**:

```csharp
public interface IVoxConcatenationService
{
    /// <summary>
    /// Concatenate word clips with silence gaps.
    /// </summary>
    Task<byte[]> ConcatenateAsync(
        IEnumerable<VoxClip> clips,
        int wordGapMs,
        CancellationToken cancellationToken = default);
}

public record VoxClip
{
    public string Word { get; init; } = "";
    public byte[] PcmData { get; init; } = Array.Empty<byte>();
    public double DurationSeconds { get; init; } = 0;
}
```

**Responsibilities**:
- Receive ordered list of word clips (PCM byte arrays)
- Insert silence (zero bytes) between clips based on `wordGapMs`
- Handle variable-length clips
- Return concatenated PCM data (48kHz, 16-bit, stereo)

**Silence Gap Calculation**:
```
Silence bytes = gapMs * 48000 * 2 (bytes per sample) * 2 (stereo) / 1000
              = gapMs * 192

Example: 50ms gap = 50 * 192 = 9,600 bytes of zeros
```

---

#### IVoxFilterService / VoxFilterService

**Purpose**: Apply PA-system audio effects (bandpass, compression, distortion) via FFmpeg.

**Lifetime**: Singleton (manages FFmpeg process pool)

**Key Methods**:

```csharp
public interface IVoxFilterService
{
    /// <summary>
    /// Apply preset filter (Off, Light, Heavy).
    /// </summary>
    Task<byte[]> ApplyFilterAsync(
        byte[] pcmData,
        PaFilterPreset preset,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply custom filter with manual parameters.
    /// </summary>
    Task<byte[]> ApplyFilterAsync(
        byte[] pcmData,
        CustomFilterSettings settings,
        CancellationToken cancellationToken = default);
}

public enum PaFilterPreset
{
    Off,     // No processing (passthrough)
    Light,   // Subtle PA effect (mild bandpass)
    Heavy,   // Classic Half-Life sound (full effect)
    Custom   // User-defined settings
}

public record CustomFilterSettings
{
    public int HighpassHz { get; init; } = 300;
    public int LowpassHz { get; init; } = 3400;
    public float CompressionRatio { get; init; } = 4.0f;
    public float Distortion { get; init; } = 0.2f; // 0.0-1.0
}
```

**Responsibilities**:
- Pipe PCM audio through FFmpeg filter chain
- Support preset configurations
- Allow custom filter parameters

**Filter Chain for "Heavy" Preset** (Half-Life classic sound):
```
highpass=f=300,lowpass=f=3400,acompressor=threshold=-20dB:ratio=4:attack=5:release=50,volume=1.2
```

**Preset Configurations**:

| Preset | Highpass | Lowpass | Compression | Distortion |
|--------|----------|---------|-------------|------------|
| **Off** | None | None | None | None |
| **Light** | 200 Hz | 5000 Hz | 2.0:1 | 0.1 (10%) |
| **Heavy** | 300 Hz | 3400 Hz | 4.0:1 | 0.2 (20%) |

---

### Integration with Existing Services

#### PlaybackService (Unchanged)

VOX will use the existing `IPlaybackService.PlayAsync()` method to stream final PCM audio to Discord.

```csharp
// VOX will call:
await _playbackService.PlayAsync(
    guildId,
    sound,                  // Synthesized VOX audio as Sound object
    queueEnabled: true,
    cancellationToken
);
```

#### AudioService (Unchanged)

Voice channel connection management remains unchanged. VOX uses existing `IAudioService` to manage voice connections.

#### AzureTtsService (Reused)

VOX will call `ITtsService.SynthesizeSpeechAsync()` for individual word synthesis with flat-prosody SSML:

```csharp
var ssml = @"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis'>
  <voice name='en-US-GuyNeural'>
    <prosody rate='-10%' pitch='-5%' volume='loud'>
      word
    </prosody>
  </voice>
</speak>";

var stream = await _ttsService.SynthesizeSpeechAsync(ssml, options, SynthesisMode.Ssml);
```

---

## Data Model

### Database Entities

#### GuildVoxSettings

Per-guild VOX configuration (analogous to `GuildTtsSettings`).

```csharp
public class GuildVoxSettings
{
    /// <summary>Primary key: Discord guild ID.</summary>
    public ulong GuildId { get; set; }

    /// <summary>Whether VOX is enabled for this guild.</summary>
    public bool VoxEnabled { get; set; } = false;

    /// <summary>Default voice identifier (e.g., "en-US-GuyNeural").</summary>
    public string DefaultVoice { get; set; } = "en-US-GuyNeural";

    /// <summary>Default PA filter preset (Off, Light, Heavy, Custom).</summary>
    public string DefaultPaFilter { get; set; } = "Heavy";

    /// <summary>Default word gap in milliseconds (20-200).</summary>
    public int DefaultWordGapMs { get; set; } = 50;

    /// <summary>Maximum words per message (prevents spam).</summary>
    public int MaxMessageWords { get; set; } = 50;

    /// <summary>Max VOX messages per user per minute (rate limiting).</summary>
    public int RateLimitPerMinute { get; set; } = 5;

    /// <summary>Auto-generate uncached words when needed (vs. skip them).</summary>
    public bool AutoGenerateMissingWords { get; set; } = true;

    /// <summary>When settings were created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When settings were last updated (UTC).</summary>
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Guild? Guild { get; set; }
}
```

#### VoxWord

Metadata for cached word clips (actual audio stored on disk).

```csharp
public class VoxWord
{
    /// <summary>Primary key: Unique identifier for this word clip.</summary>
    public Guid Id { get; set; }

    /// <summary>Guild ID this word is cached for (per-guild cache).</summary>
    public ulong GuildId { get; set; }

    /// <summary>The word text (lowercase, alphanumeric only).</summary>
    public string Word { get; set; } = string.Empty;

    /// <summary>Voice identifier used for this clip (e.g., "en-US-GuyNeural").</summary>
    public string Voice { get; set; } = string.Empty;

    /// <summary>File path relative to cache root (e.g., "en-US-GuyNeural/attention.pcm").</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; set; }

    /// <summary>Audio duration in seconds (derived from file size and sample rate).</summary>
    public double DurationSeconds { get; set; }

    /// <summary>When this word was cached (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Guild? Guild { get; set; }
}
```

**Database Index**: `(GuildId, Word, Voice)` for fast lookups during tokenization.

#### VoxMessage

Audit log of VOX messages sent (analogous to `TtsMessage`).

```csharp
public class VoxMessage
{
    /// <summary>Primary key: Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Guild ID where message was sent.</summary>
    public ulong GuildId { get; set; }

    /// <summary>User ID who sent the message.</summary>
    public ulong UserId { get; set; }

    /// <summary>Username at time of sending (for display, since usernames change).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>The original message text (or composition description).</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Number of words in the announcement.</summary>
    public int WordCount { get; set; }

    /// <summary>Total audio duration in seconds.</summary>
    public double DurationSeconds { get; set; }

    /// <summary>Voice used.</summary>
    public string Voice { get; set; } = string.Empty;

    /// <summary>PA filter applied (Heavy, Light, Off, Custom).</summary>
    public string PaFilter { get; set; } = "Heavy";

    /// <summary>When the message was sent (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Guild? Guild { get; set; }
}
```

### DTOs and Models

#### VoxOptions

Configuration for VOX synthesis (analogous to `TtsOptions`).

```csharp
public record VoxOptions
{
    /// <summary>Voice identifier (e.g., "en-US-GuyNeural").</summary>
    public string Voice { get; init; } = "en-US-GuyNeural";

    /// <summary>Word gap in milliseconds (20-200).</summary>
    public int WordGapMs { get; init; } = 50;

    /// <summary>PA filter preset or custom.</summary>
    public PaFilterPreset PaFilter { get; init; } = PaFilterPreset.Heavy;

    /// <summary>Custom filter settings (only if PaFilter = Custom).</summary>
    public CustomFilterSettings? CustomFilterSettings { get; init; }

    /// <summary>Speech speed multiplier (0.75-1.5).</summary>
    public float Speed { get; init; } = 1.0f;
}
```

#### VoxComposition

Word sequence from sentence builder mode.

```csharp
public record VoxComposition
{
    /// <summary>List of words and pauses in order.</summary>
    public List<CompositionItem> Items { get; init; } = new();
}

public record CompositionItem
{
    /// <summary>Unique ID for drag-drop reordering.</summary>
    public string Id { get; init; } = "";

    /// <summary>Type: Word or Pause.</summary>
    public CompositionItemType Type { get; init; }

    /// <summary>Word text (if Type = Word).</summary>
    public string Word { get; init; } = "";

    /// <summary>Pause duration in ms (if Type = Pause).</summary>
    public int PauseDurationMs { get; init; } = 100;
}

public enum CompositionItemType
{
    Word,
    Pause
}
```

#### VoxGenerationProgress

Progress reporting during multi-word generation.

```csharp
public record VoxGenerationProgress
{
    /// <summary>Total words to generate.</summary>
    public int TotalWords { get; init; }

    /// <summary>Words generated so far.</summary>
    public int GeneratedWords { get; init; }

    /// <summary>Words already cached (no generation needed).</summary>
    public int CachedWords { get; init; }

    /// <summary>Words that failed to generate.</summary>
    public int FailedWords { get; init; }

    /// <summary>Current stage (Tokenizing, CheckingCache, Generating, Concatenating, Filtering).</summary>
    public GenerationStage Stage { get; init; }
}

public enum GenerationStage
{
    Tokenizing,
    CheckingCache,
    Generating,
    Concatenating,
    Filtering
}
```

#### Enums

```csharp
public enum TokenStatus
{
    Cached,        // Word clip is cached
    WillGenerate,  // Will be generated on-demand
    Error          // Cannot synthesize (invalid word)
}

public enum PaFilterPreset
{
    Off,    // No audio processing
    Light,  // Subtle PA effect
    Heavy,  // Classic Half-Life sound
    Custom  // User-defined parameters
}
```

---

## Word Bank System

### Storage Architecture

**Cache Location**: `sounds/vox/{guildId}/{voice}/`

**File Structure**:
```
sounds/
  vox/
    123456789012345678/          # Guild ID
      en-US-GuyNeural/           # Voice identifier
        attention.pcm            # Word clips (48kHz, 16-bit, stereo PCM)
        all.pcm
        personnel.pcm
        warning.pcm
        ...
      en-US-AriaNeural/
        attention.pcm
        ...
```

**File Naming**: Word → filename mapping (sanitized for filesystem):
- Lowercase word text
- Replace invalid characters with underscores
- Hash for collision avoidance
- Example: "don't" → "dont.pcm" or "dont_hash.pcm"

### Word Synthesis via Azure TTS

Individual words are synthesized using flat-prosody SSML to ensure emotionless, robotic delivery:

```xml
<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis">
  <voice name="en-US-GuyNeural">
    <prosody rate="-10%" pitch="-5%" volume="loud">
      {word}
    </prosody>
  </voice>
</speak>
```

**Settings**:
- **Rate**: -10% (slower speech) for clarity
- **Pitch**: -5% (lower pitch) for authoritative tone
- **Volume**: loud (emphasis)

**Output Format**: PCM 48kHz, 16-bit, stereo (mono converted to stereo like existing TTS)

### Predefined Word Packs

Four standard packs available for bulk generation:

| Pack | Word Count | Contents | Use Case |
|------|-----------|----------|----------|
| **Common English 500** | ~500 | Most frequent English words (the, a, is, that, etc.) | General announcements |
| **NATO Phonetic** | 26 | Alpha through Zulu | Code/ID announcements |
| **Numbers 0-100** | 101 | Zero through one hundred | Numeric announcements |
| **Half-Life Classic** | ~87 | Iconic HL1 VOX phrases | Themed announcements |

**Half-Life Pack Contents** (examples):
- Alerts: `warning`, `alert`, `caution`, `attention`
- Locations: `sector`, `area`, `level`, `facility`, `chamber`, `lab`
- Status: `containment`, `lockdown`, `evacuation`, `emergency`, `hazard`
- Technical: `system`, `failure`, `malfunction`, `reactor`, `core`, `power`
- Actions: `detected`, `initiated`, `commence`, `complete`, `terminated`
- Personnel: `personnel`, `employee`, `scientist`, `security`, `intruder`

### Unknown Word Handling

When a word is not in the cache:

1. **If `AutoGenerateMissingWords = true`**:
   - Generate clip on-demand via Azure TTS
   - Cache for future use
   - Include in output
   - Cost: Incurred at message time

2. **If `AutoGenerateMissingWords = false`**:
   - Skip the word (omit from output)
   - Log a warning
   - Continue processing remaining words
   - Cost: Zero

3. **If word is invalid** (special characters, too long, etc.):
   - Return error status
   - Skip the word
   - Log validation error

### Import/Export

**Export Format**: ZIP file containing PCM clips and metadata

```
vox-export-en-US-GuyNeural.zip
├── metadata.json
│   {
│     "voice": "en-US-GuyNeural",
│     "exportDate": "2026-02-02T10:30:00Z",
│     "wordCount": 1247,
│     "totalSizeBytes": 25698304,
│     "words": [
│       { "word": "attention", "size": 18648, "duration": 0.8 }
│     ]
│   }
├── clips/
│   ├── attention.pcm
│   ├── all.pcm
│   ├── personnel.pcm
│   └── ...
```

**Import Process**:
1. Extract ZIP
2. Validate metadata
3. Write clips to `sounds/vox/{guildId}/{voice}/`
4. Add entries to `VoxWord` table
5. Report results (imported, skipped, errors)

---

## Audio Pipeline

### Complete Flow

```
User: /vox "warning, biohazard detected in sector c"
  │
  ▼
1. VoxModule validates input
   ├─ VOX enabled? ✓
   ├─ User in voice channel? ✓
   ├─ Rate limit OK? ✓
   └─ Message length valid? ✓
  │
  ▼
2. VoxTokenizer.Tokenize()
   "warning, biohazard detected in sector c"
   → [warning, _pause(150ms), biohazard, detected, in, sector, c]
  │
  ▼
3. VoxWordBankService.GetCacheStatusAsync()
   Check which words are cached:
   ├─ warning → Cached ✓
   ├─ biohazard → WillGenerate ⚠
   ├─ detected → Cached ✓
   ├─ in → Cached ✓
   ├─ sector → Cached ✓
   └─ c → Cached ✓
  │
  ▼
4. VoxWordBankService — Generate missing words IN PARALLEL
   ├─ Partition words into cached vs. uncached
   ├─ Acquire SemaphoreSlim (concurrency = WordGenerationConcurrency)
   ├─ Launch Task.WhenAll() for all uncached words:
   │   ├─ Build SSML with flat prosody
   │   ├─ Call AzureTtsService
   │   ├─ Get PCM stream
   │   ├─ Save to sounds/vox/{guildId}/{voice}/
   │   ├─ Record in VoxWord table
   │   └─ Report progress (thread-safe increment)
   └─ Await all tasks; collect results
  │
  ▼
5. VoxWordBankService.GetClipAsync() for all words
   Return ordered list of VoxClip:
   ├─ VoxClip("warning", pcmBytes[1], 0.6s)
   ├─ VoxClip("biohazard", pcmBytes[2], 0.9s)
   ├─ VoxClip("detected", pcmBytes[3], 0.5s)
   ├─ VoxClip("in", pcmBytes[4], 0.2s)
   ├─ VoxClip("sector", pcmBytes[5], 0.6s)
   └─ VoxClip("c", pcmBytes[6], 0.3s)
  │
  ▼
6. VoxConcatenationService.ConcatenateAsync()
   Join clips with 50ms silence gaps:
   ├─ warning[0.6s]
   ├─ [silence 50ms]
   ├─ 150ms pause from comma
   ├─ [silence 150ms]
   ├─ biohazard[0.9s]
   ├─ [silence 50ms]
   ├─ detected[0.5s]
   ├─ [silence 50ms]
   ├─ in[0.2s]
   ├─ [silence 50ms]
   ├─ sector[0.6s]
   ├─ [silence 50ms]
   └─ c[0.3s]
   → Concatenated PCM bytes (~4.5 seconds total)
  │
  ▼
7. VoxFilterService.ApplyFilterAsync()
   Pipe concatenated PCM through FFmpeg:
   ├─ highpass=f=300
   ├─ lowpass=f=3400
   ├─ acompressor=threshold=-20dB:ratio=4:attack=5:release=50
   ├─ volume=1.2
   → Filtered PCM bytes
  │
  ▼
8. PlaybackService.PlayAsync()
   ├─ Create temporary Sound object from filtered PCM
   ├─ Stream to Discord via AudioOutStream
   ├─ Emit playback events (started, progress, finished)
  │
  ▼
9. VoxMessage logged to database
   ├─ GuildId, UserId, Username, Message
   ├─ WordCount, DurationSeconds, Voice, PaFilter
   ├─ CreatedAt
  │
  ▼
Success! User sees toast notification

```

### Performance Characteristics

| Operation | Time | Notes |
|-----------|------|-------|
| Tokenization | <10ms | Local parsing |
| Cache lookup | ~50-100ms | Batch DB query for all tokens |
| Word generation (per word) | 500-2000ms | Azure TTS latency per call |
| Parallel generation (N words, C concurrency) | ~ceil(N/C) * 1s | e.g. 6 words at concurrency 3 ≈ 2 batches ≈ 2s |
| Bulk generation (500 words, concurrency 3) | ~2.5-5 min | Parallel with SemaphoreSlim throttle |
| Concatenation | 10-50ms | In-memory byte array join |
| Filtering (FFmpeg) | 100-500ms | Depends on audio length and filters |
| **Total (all cached)** | ~200-300ms | Fastest path |
| **Total (3 uncached, concurrency 3)** | ~1.5-3s | Single parallel batch + pipeline |
| **Total (10 uncached, concurrency 3)** | ~3-8s | ~4 parallel batches + pipeline |

---

## Slash Commands

### /vox

Send a VOX announcement.

**Module**: `VoxModule` (Discord.Commands)

**Command Definition**:
```csharp
[RequireGuildActive]
[RequireVoxEnabled]
[RequireVoiceChannel]
[RateLimit(5, 10)]
public async Task VoxAsync(
    [Summary("message", "Announcement text")] string message,
    [Summary("voice", "Voice preset")] string? voice = null,
    [Summary("filter", "PA filter (off/light/heavy)")] string? filter = null,
    [Summary("gap", "Word gap in ms (20-200)")] int? gap = null)
{
    // Implementation
}
```

**Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `message` | string | Yes | - | Announcement text (max 500 chars, validated for word count) |
| `voice` | string | No | Guild default | Voice identifier with autocomplete |
| `filter` | choice | No | Guild default | `off`, `light`, `heavy` |
| `gap` | int | No | Guild default | Word gap 20-200ms |

**Autocomplete**:
- `voice` parameter suggests available Azure voices

**Preconditions**:
- `[RequireGuildActive]` - Guild must be active (bot is in server)
- `[RequireVoxEnabled]` - VOX enabled for guild (settings check)
- `[RequireVoiceChannel]` - User must be in a voice channel
- `[RateLimit(5, 10)]` - Command-level: 5 invocations per 10 seconds + guild-level per-minute

**Behavior**:
1. Validate parameters
2. Check rate limits (command + guild level)
3. Auto-join user's voice channel if needed
4. Tokenize message
5. Check cache status
6. Generate missing words (if auto-generate enabled)
7. Concatenate and filter
8. Stream to Discord
9. Log to audit trail
10. Return ephemeral success/error message

**Example Usage**:
```
/vox message:"attention all personnel security breach detected"
/vox message:"sector seven lockdown initiated" voice:en-US-AriaNeural filter:heavy gap:75
```

### /vox-admin (Admin Only)

Administrative commands for VOX configuration and word bank management.

**Module**: `VoxAdminModule`

**Precondition**: `[RequireAdmin]` policy

#### /vox-admin config

View/edit VOX settings for guild.

```csharp
[SlashCommand("config", "View or edit VOX configuration")]
public async Task ConfigAsync() { ... }
```

Opens a modal or ephemeral embed showing current settings with edit buttons (triggers separate commands or slash command groups).

#### /vox-admin wordbank-status

Show word bank statistics.

```csharp
[SlashCommand("wordbank-status", "Show word bank statistics")]
public async Task WordBankStatusAsync() { ... }
```

**Response** (ephemeral embed):
```
📊 VOX Word Bank Statistics
━━━━━━━━━━━━━━━━━━━━━━━━
Total Words: 1,247
Total Size: 24.5 MB
Voices: 3
  • en-US-GuyNeural: 450 words
  • en-US-AriaNeural: 500 words
  • en-GB-RyanNeural: 297 words
```

#### /vox-admin generate-pack

Bulk generate a predefined word pack.

```csharp
[SlashCommand("generate-pack", "Generate a word pack")]
public async Task GeneratePackAsync(
    [Summary("pack", "Word pack to generate")]
    [Choice("Common English 500", "common-500")]
    [Choice("NATO Phonetic", "nato-phonetic")]
    [Choice("Numbers 0-100", "numbers")]
    [Choice("Half-Life Classic", "halflife-classic")]
    string packId,
    [Summary("voice", "Voice to use")] string? voice = null)
{ ... }
```

Shows progress modal during generation.

---

## Configuration

### appsettings.json

```json
{
  "Vox": {
    "DefaultVoice": "en-US-GuyNeural",
    "DefaultWordGapMs": 50,
    "DefaultPaFilter": "Heavy",
    "MaxWordLength": 30,
    "MaxMessageWords": 50,
    "CachePath": "./sounds/vox",
    "SupportedFormats": ["pcm"],
    "AutoGenerateMissingWords": true,
    "WordGenerationConcurrency": 3
  }
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `DefaultVoice` | string | `"en-US-GuyNeural"` | Default voice for new guilds |
| `DefaultWordGapMs` | int | `50` | Default silence between words (ms) |
| `DefaultPaFilter` | string | `"Heavy"` | Default PA filter preset |
| `MaxWordLength` | int | `30` | Maximum characters per word |
| `MaxMessageWords` | int | `50` | Default max words per message |
| `CachePath` | string | `"./sounds/vox"` | Base directory for word cache |
| `SupportedFormats` | array | `["pcm"]` | Audio format types |
| `AutoGenerateMissingWords` | bool | `true` | Generate words on-demand by default |
| `WordGenerationConcurrency` | int | `3` | Parallel word generation limit |

### Binding to Options Pattern

```csharp
public class VoxOptions
{
    public const string SectionName = "Vox";

    public string DefaultVoice { get; set; } = "en-US-GuyNeural";
    public int DefaultWordGapMs { get; set; } = 50;
    public string DefaultPaFilter { get; set; } = "Heavy";
    public int MaxWordLength { get; set; } = 30;
    public int MaxMessageWords { get; set; } = 50;
    public string CachePath { get; set; } = "./sounds/vox";
    public List<string> SupportedFormats { get; set; } = new() { "pcm" };
    public bool AutoGenerateMissingWords { get; set; } = true;
    public int WordGenerationConcurrency { get; set; } = 3;
}
```

---

## Dependency Injection

### Registration Extension

Create `src/DiscordBot.Bot/Extensions/VoxServiceExtensions.cs`:

```csharp
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DiscordBot.Bot.Extensions;

/// <summary>
/// Extension methods for registering VOX services.
/// </summary>
public static class VoxServiceExtensions
{
    /// <summary>
    /// Adds all VOX-related services to the service collection.
    /// </summary>
    public static IServiceCollection AddVox(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<VoxOptions>(
            configuration.GetSection(VoxOptions.SectionName));

        // Core VOX orchestrator (singleton - manages state)
        services.AddSingleton<IVoxService, VoxService>();

        // Word bank manager (singleton - manages disk I/O and cache)
        services.AddSingleton<IVoxWordBankService, VoxWordBankService>();

        // Audio processing (singleton - stateless)
        services.AddSingleton<IVoxConcatenationService, VoxConcatenationService>();
        services.AddSingleton<IVoxFilterService, VoxFilterService>();

        // Text tokenization (transient - thread-safe, stateless)
        services.AddTransient<IVoxTokenizer, VoxTokenizer>();

        // Data services (scoped - per-request)
        services.AddScoped<IVoxSettingsService, VoxSettingsService>();
        services.AddScoped<IVoxHistoryService, VoxHistoryService>();

        return services;
    }
}
```

### Registration in Program.cs

```csharp
// In Program.cs, after voice support:
builder.Services.AddVox(builder.Configuration);
```

---

## Database Schema

### EF Core Migrations

Create migration:
```bash
dotnet ef migrations add AddVoxSystem \
  --project src/DiscordBot.Infrastructure \
  --startup-project src/DiscordBot.Bot
```

### Table Definitions

#### GuildVoxSettings
```sql
CREATE TABLE GuildVoxSettings (
    GuildId BIGINT PRIMARY KEY,
    VoxEnabled BIT NOT NULL DEFAULT 0,
    DefaultVoice NVARCHAR(100) NOT NULL DEFAULT 'en-US-GuyNeural',
    DefaultPaFilter NVARCHAR(20) NOT NULL DEFAULT 'Heavy',
    DefaultWordGapMs INT NOT NULL DEFAULT 50,
    MaxMessageWords INT NOT NULL DEFAULT 50,
    RateLimitPerMinute INT NOT NULL DEFAULT 5,
    AutoGenerateMissingWords BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (GuildId) REFERENCES Guilds(Id)
);
```

#### VoxWords
```sql
CREATE TABLE VoxWords (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    GuildId BIGINT NOT NULL,
    Word NVARCHAR(100) NOT NULL,
    Voice NVARCHAR(100) NOT NULL,
    FilePath NVARCHAR(500) NOT NULL,
    FileSizeBytes BIGINT NOT NULL,
    DurationSeconds FLOAT NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (GuildId) REFERENCES Guilds(Id),
    UNIQUE INDEX IX_VoxWords_GuildIdWordVoice (GuildId, Word, Voice)
);

CREATE INDEX IX_VoxWords_GuildId ON VoxWords(GuildId);
CREATE INDEX IX_VoxWords_Voice ON VoxWords(Voice);
```

#### VoxMessages
```sql
CREATE TABLE VoxMessages (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    GuildId BIGINT NOT NULL,
    UserId BIGINT NOT NULL,
    Username NVARCHAR(255) NOT NULL,
    Message NVARCHAR(MAX) NOT NULL,
    WordCount INT NOT NULL,
    DurationSeconds FLOAT NOT NULL,
    Voice NVARCHAR(100) NOT NULL,
    PaFilter NVARCHAR(20) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (GuildId) REFERENCES Guilds(Id)
);

CREATE INDEX IX_VoxMessages_GuildId ON VoxMessages(GuildId);
CREATE INDEX IX_VoxMessages_UserId ON VoxMessages(UserId);
CREATE INDEX IX_VoxMessages_CreatedAt ON VoxMessages(CreatedAt);
```

---

## API Endpoints

### Portal VOX Endpoints

Portal pages reference the [vox-ui-spec.md](vox-ui-spec.md) for full request/response schemas.

#### `GET /api/portal/vox/{guildId}/status`

Get current VOX system status for a guild.

**Response**:
```json
{
  "isConnected": true,
  "currentChannelId": "123456789012345678",
  "currentChannelName": "General Voice",
  "isPlaying": false,
  "currentMessage": null,
  "maxMessageWords": 50,
  "rateLimitPerMinute": 5,
  "defaultVoice": "en-US-GuyNeural",
  "defaultPaFilter": "heavy",
  "defaultWordGapMs": 50
}
```

#### `POST /api/portal/vox/{guildId}/send`

Send a VOX announcement.

**Request**:
```json
{
  "mode": "freetext",
  "message": "attention all personnel security breach in sector c",
  "voice": "en-US-GuyNeural",
  "wordGapMs": 50,
  "paFilter": "heavy",
  "customFilterSettings": null
}
```

**Response**:
```json
{
  "success": true,
  "message": "VOX announcement queued",
  "generatedWords": ["security", "breach"],
  "cachedWords": ["attention", "all", "personnel", "in", "sector", "c"],
  "estimatedDurationSeconds": 4.2
}
```

#### `GET /api/portal/vox/{guildId}/token-preview`

Parse a message and return token preview with cache status.

**Query Parameters**:
- `message` (required): Text to parse
- `voice` (optional): Voice to check cache against

**Response**:
```json
{
  "tokens": [
    {
      "word": "attention",
      "status": "cached",
      "durationSeconds": 0.8
    },
    {
      "word": "security",
      "status": "will_generate",
      "durationSeconds": 0.0
    }
  ],
  "totalWords": 7,
  "cachedWords": 5,
  "willGenerate": 1,
  "errorWords": 0,
  "estimatedDurationSeconds": 4.2
}
```

#### `POST /api/portal/vox/{guildId}/stop`

Stop current VOX playback.

**Response**:
```json
{
  "success": true,
  "message": "Playback stopped"
}
```

### Admin VOX Endpoints

#### `GET /api/guilds/{guildId}/vox/config`

Get VOX configuration.

**Response**:
```json
{
  "isEnabled": true,
  "defaultVoice": "en-US-GuyNeural",
  "defaultPaFilter": "heavy",
  "defaultWordGapMs": 50,
  "maxMessageWords": 50,
  "rateLimitPerMinute": 5,
  "autoGenerateMissingWords": true
}
```

#### `PUT /api/guilds/{guildId}/vox/config`

Update VOX configuration.

**Request**: Same as GET response.

#### `GET /api/guilds/{guildId}/vox/wordbank`

Get word bank with pagination and search.

**Query Parameters**:
- `page` (default 1)
- `pageSize` (default 50)
- `search` (optional)
- `voice` (optional)
- `sort` (default "dateAdded")
- `order` (default "desc")

**Response**:
```json
{
  "stats": {
    "totalWords": 1247,
    "totalSizeBytes": 25698304,
    "voicesUsed": 3
  },
  "words": [
    {
      "id": "uuid-here",
      "word": "attention",
      "voice": "en-US-GuyNeural",
      "fileSizeBytes": 18648,
      "durationSeconds": 0.8,
      "dateAdded": "2026-01-15T10:30:00Z"
    }
  ],
  "pagination": {
    "currentPage": 1,
    "totalPages": 25,
    "totalItems": 1247,
    "pageSize": 50
  }
}
```

#### `POST /api/guilds/{guildId}/vox/wordbank/generate`

Bulk generate words.

**Request**:
```json
{
  "words": ["attention", "all", "personnel"],
  "voice": "en-US-GuyNeural",
  "packId": null
}
```

Or use predefined pack:
```json
{
  "words": null,
  "voice": "en-US-GuyNeural",
  "packId": "common-500"
}
```

**Response**:
```json
{
  "success": true,
  "message": "Generated 453 words",
  "generated": 453,
  "alreadyCached": 47,
  "failed": 0
}
```

#### `DELETE /api/guilds/{guildId}/vox/wordbank/{wordId}`

Delete a word from word bank.

#### `POST /api/guilds/{guildId}/vox/wordbank/purge`

Purge word bank cache.

**Request**:
```json
{
  "voice": "en-US-GuyNeural",
  "all": false
}
```

#### `POST /api/guilds/{guildId}/vox/wordbank/import`

Import word bank from ZIP file.

**Request**: `multipart/form-data` with file field.

#### `GET /api/guilds/{guildId}/vox/wordbank/export`

Export word bank as ZIP file.

**Query Parameters**:
- `voice` (optional): Export specific voice

---

## Security & Rate Limiting

### Authorization

**Portal Pages**:
- `[Authorize(Policy = "PortalGuildMember")]`
- Guild membership verified via Discord API
- Rate limiting enforced per-user

**Admin Pages**:
- `[Authorize(Policy = "RequireAdmin")]`
- Admin role required
- No rate limiting (admins can bulk generate)

### Rate Limiting

**Command-Level**:
```csharp
[RateLimit(5, 10)]  // 5 invocations per 10 seconds
public async Task VoxAsync(...) { ... }
```

**Guild-Level**:
- Key: `vox:{guildId}:{userId}`
- Value: `GuildVoxSettings.RateLimitPerMinute` (default: 5)
- Duration: 1 minute sliding window
- Enforced at API endpoint level

### Input Validation

| Input | Validation | Error |
|-------|-----------|-------|
| Message text | Max 500 chars, max 50 words | 400 Bad Request |
| Word text | Alphanumeric only, max 30 chars | Error token status |
| Voice identifier | Must exist in Azure voices | 400 Bad Request |
| Word gap | 20-200 ms | 400 Bad Request |
| Filter preset | Off/Light/Heavy/Custom | 400 Bad Request |

### File Path Sanitization

Word clips stored in `sounds/vox/{guildId}/{voice}/`:
- Sanitize word text (alphanumeric + underscore)
- Use hash for collision avoidance
- Never allow directory traversal (`../`, `..\\`)
- Restrict to PCM format only

### Azure Speech Key Protection

- Never commit `AzureSpeech:SubscriptionKey` to source control
- Use user secrets for local development
- Use environment variables or Azure Key Vault for production

---

## Cost Management

### Azure Speech Pricing (as of 2026)

- **Neural Voices**: ~$16 per 1 million characters
- **Free Tier**: 0.5 million characters free per month

### Cost Optimization Strategies

1. **Word Bank Caching**
   - Generate once, play many times
   - Reuse common words across messages
   - Example: "attention" generated once, used 100 times = 0.1% cost of non-cached

2. **Bulk Generation**
   - Generate popular word packs upfront
   - Example: Generate 500-word pack once during off-hours
   - Cost: ~$8 for 500 words = much cheaper than generating on-demand

3. **Rate Limiting**
   - Enforce per-minute limits to prevent spam
   - Default: 5 messages per user per minute
   - Prevent bot abuse

4. **Max Message Words**
   - Default: 50 words max per message
   - Limits generation cost per message
   - Configurable per guild

5. **Monitoring**
   - Track usage via `VoxMessage` table
   - Alert on unusual spikes
   - Disable VOX for inactive guilds

### Usage Monitoring Query

```sql
SELECT
    GuildId,
    COUNT(*) AS MessageCount,
    SUM(WordCount) AS TotalWords,
    SUM(DurationSeconds) AS TotalDurationSeconds,
    COUNT(DISTINCT UserId) AS UniqueUsers
FROM VoxMessages
WHERE CreatedAt >= CAST(CAST(GETUTCDATE() AS DATE) - 30 AS DATETIME2)
GROUP BY GuildId
ORDER BY TotalWords DESC;
```

---

## Related Documentation

- **[vox-ui-spec.md](vox-ui-spec.md)** - VOX Portal UI/UX design and component specifications
- **[tts-support.md](tts-support.md)** - Existing TTS system (VOX builds on this)
- **[audio-dependencies.md](audio-dependencies.md)** - FFmpeg, libsodium, opus setup
- **[database-schema.md](database-schema.md)** - Entity relationships and schema patterns
- **[component-api.md](component-api.md)** - Razor UI components
- **[design-system.md](design-system.md)** - Design tokens and colors
- **[authorization-policies.md](authorization-policies.md)** - Role-based access control
- **[Azure Speech Service Documentation](https://learn.microsoft.com/azure/ai-services/speech-service/)** - Official Azure docs
- **[FFmpeg Documentation](https://ffmpeg.org/documentation.html)** - Audio filter documentation

---

## Implementation Notes

### Phased Rollout

**Phase 1**: Core services (tokenizer, word bank, concatenation, filter)
**Phase 2**: Portal UI (free text mode, then sentence builder)
**Phase 3**: Admin UI (settings, word bank management)
**Phase 4**: Slash commands and public API
**Phase 5**: Polish, testing, documentation

### Testing Strategy

**Unit Tests**:
- Tokenizer (punctuation, validation, edge cases)
- Filter service (FFmpeg integration mocks)
- Options validation

**Integration Tests**:
- Word generation via Azure TTS
- Word bank caching (write, read, delete)
- Concatenation with various clip lengths
- Full pipeline with real audio

**E2E Tests**:
- Slash command execution
- Portal page workflow
- Admin operations

### Known Limitations & Future Enhancements

**Current Scope**:
- English text only (extensible to other languages)
- PCM 48kHz 16-bit stereo only
- FFmpeg-based filtering (can add more effects)
- Per-guild word banks (not shared across guilds)

**Future Enhancements**:
- Multi-language support
- Custom voice cloning
- Advanced effect chains (reverb, echo, distortion plugins)
- Shared community word packs
- VOX message scheduling
- VOX template library (e.g., "Security Alert: {location}")

---

**Last Updated**: 2026-02-02
**Version**: 1.0
