# VOX System Specification

**Version:** 2.0
**Date:** 2026-02-02
**Status:** Design Document
**Target Framework:** .NET 8, Discord.Net

---

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Clip Library](#clip-library)
4. [Audio Pipeline](#audio-pipeline)
5. [Slash Commands](#slash-commands)
6. [Configuration](#configuration)
7. [Dependency Injection](#dependency-injection)
8. [API Endpoints](#api-endpoints)
9. [Security & Rate Limiting](#security--rate-limiting)
10. [Future Expansion](#future-expansion)
11. [Related Documentation](#related-documentation)

---

## Overview

### What is VOX?

VOX is a Half-Life-style concatenated clip announcement system for Discord. It plays pre-recorded word clips in sequence to create robotic, word-by-word announcements - the iconic PA system sound from Half-Life 1.

Unlike the existing soundboard (which plays one clip at a time), VOX:

1. **Parses input text** into individual word tokens
2. **Looks up pre-built audio clips** from a static library organized in 3 groups
3. **Concatenates matching clips** with configurable silence gaps between words
4. **Streams the result** to Discord voice channels

### How VOX Differs from the Soundboard

| Feature | Soundboard | VOX |
|---------|-----------|-----|
| **Input** | Select one sound by name | Type a sentence of multiple words |
| **Playback** | Single clip at a time | Concatenated sequence of clips |
| **Sound Source** | Per-guild uploaded files (DB-tracked) | Global static clip library (filesystem) |
| **Management** | Upload/delete per guild | Pre-built, ships with bot |
| **Audio Processing** | Optional filters | Word gap insertion + concatenation |
| **Commands** | `/play <sound>` | `/vox`, `/fvox`, `/hgrunt` |

### Clip Groups

Three independent libraries of pre-recorded MP3 clips, each corresponding to a Half-Life sound set:

| Group | Folder | Command | Description | Estimated Clips |
|-------|--------|---------|-------------|-----------------|
| **VOX** | `sounds/vox/` | `/vox` | Half-Life VOX announcement system clips | 100-200 |
| **FVOX** | `sounds/fvox/` | `/fvox` | Half-Life HEV suit (female VOX) clips | 100-200 |
| **HGrunt** | `sounds/hgrunt/` | `/hgrunt` | Half-Life military grunt radio clips | 100-200 |

### Key Capabilities

- **3 clip groups** with dedicated slash commands
- **Text-to-clip-sequence** - Type words, matching clips are concatenated
- **Configurable word gap** - 20-200ms silence between clips
- **Autocomplete** - Slash command parameter suggests available clip names
- **Clip browser UI** - Portal page with search, grid, and autocomplete input
- **Unknown word handling** - Words without matching clips are skipped with feedback
- **Reuses existing PlaybackService** - Same audio streaming infrastructure as soundboard

---

## Architecture

### Overview

VOX follows the existing bot architecture, reusing services where possible:

```
┌──────────────────────────────────────────────────────┐
│ Bot Layer (Commands + UI)                            │
├──────────────────────────────────────────────────────┤
│ VoxModule / FvoxModule / HgruntModule (slash cmds)   │
│ VoxController (portal API)                           │
│ Portal/VOX Razor Page                                │
├──────────────────────────────────────────────────────┤
│ Services                                             │
├──────────────────────────────────────────────────────┤
│ IVoxService (orchestrator)                           │
│ IVoxClipLibrary (clip inventory + file access)       │
│ IVoxConcatenationService (audio joining via FFmpeg)  │
│ IPlaybackService (existing - streams to Discord)     │
└──────────────────────────────────────────────────────┘
```

No new database tables are required. Clip inventory is built by scanning the filesystem at startup and held in memory.

### Service Responsibilities

#### IVoxClipLibrary / VoxClipLibrary

**Purpose**: Manages the static clip inventory. Scans clip folders at startup, provides lookup and search.

**Lifetime**: Singleton (loads once, holds inventory in memory)

```csharp
public interface IVoxClipLibrary
{
    /// <summary>
    /// Initialize by scanning clip folders. Called at startup.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all available clip names for a group.
    /// </summary>
    IReadOnlyList<VoxClipInfo> GetClips(VoxClipGroup group);

    /// <summary>
    /// Look up a single clip by name within a group.
    /// Returns null if no matching clip exists.
    /// </summary>
    VoxClipInfo? GetClip(VoxClipGroup group, string clipName);

    /// <summary>
    /// Search clips by prefix/substring for autocomplete.
    /// Returns up to maxResults matches.
    /// </summary>
    IReadOnlyList<VoxClipInfo> SearchClips(VoxClipGroup group, string query, int maxResults = 25);

    /// <summary>
    /// Get the full file path for a clip.
    /// </summary>
    string GetClipFilePath(VoxClipGroup group, string clipName);

    /// <summary>
    /// Get clip count per group.
    /// </summary>
    int GetClipCount(VoxClipGroup group);
}
```

**Data Structures**:

```csharp
public enum VoxClipGroup
{
    Vox,
    Fvox,
    Hgrunt
}

public record VoxClipInfo
{
    /// <summary>Clip name (filename without extension, e.g. "warning").</summary>
    public string Name { get; init; } = "";

    /// <summary>Which group this clip belongs to.</summary>
    public VoxClipGroup Group { get; init; }

    /// <summary>File size in bytes.</summary>
    public long FileSizeBytes { get; init; }

    /// <summary>Audio duration in seconds (extracted via FFprobe at startup).</summary>
    public double DurationSeconds { get; init; }
}
```

**Initialization**:
- On startup, scan `sounds/vox/`, `sounds/fvox/`, `sounds/hgrunt/`
- For each `.mp3` file found, extract name (filename without extension) and duration (via FFprobe)
- Store in `Dictionary<VoxClipGroup, Dictionary<string, VoxClipInfo>>`
- Log clip counts per group
- Duration extraction can be parallelized for startup performance

**Search**:
- Prefix match first (clips starting with query), then substring match
- Case-insensitive
- Used by both slash command autocomplete and portal UI

---

#### IVoxConcatenationService / VoxConcatenationService

**Purpose**: Concatenate multiple MP3 clips into a single audio stream with silence gaps.

**Lifetime**: Singleton (stateless)

```csharp
public interface IVoxConcatenationService
{
    /// <summary>
    /// Concatenate a sequence of clips with silence gaps between them.
    /// Returns a temporary file path to the concatenated audio.
    /// </summary>
    Task<string> ConcatenateAsync(
        IReadOnlyList<string> clipFilePaths,
        int wordGapMs,
        CancellationToken cancellationToken = default);
}
```

**Implementation**: Uses FFmpeg to:
1. Decode each MP3 clip
2. Insert silence (zero samples) between clips based on `wordGapMs`
3. Output concatenated result as a temporary file (PCM or MP3)

**FFmpeg Approach** - Use the `concat` demuxer or filter_complex with `adelay`/`apad`:

```
ffmpeg -f concat -safe 0 -i filelist.txt -af "apad=pad_dur={gapMs}ms" -f s16le -ar 48000 -ac 2 output.pcm
```

Or build a filter graph that intersperses silence:
```
ffmpeg -i clip1.mp3 -i clip2.mp3 -i clip3.mp3 \
  -filter_complex "[0:a]apad=pad_dur=50ms[a0];[1:a]apad=pad_dur=50ms[a1];[2:a][a0][a1]concat=n=3:v=0:a=1" \
  -f s16le -ar 48000 -ac 2 output.pcm
```

The simplest reliable approach: generate a concat file list with silence entries, or append silence bytes programmatically between decoded PCM segments.

**Silence Gap Calculation** (for raw PCM 48kHz, 16-bit, stereo):
```
Silence bytes = gapMs * 48000 * 2 (bytes/sample) * 2 (channels) / 1000
              = gapMs * 192
```

---

#### IVoxService / VoxService

**Purpose**: Main orchestrator. Parses input, resolves clips, concatenates, and plays.

**Lifetime**: Scoped (per-request, coordinates the pipeline)

```csharp
public interface IVoxService
{
    /// <summary>
    /// Process a VOX message: tokenize, resolve clips, concatenate, and play.
    /// Returns result with details about which words were found/skipped.
    /// </summary>
    Task<VoxPlaybackResult> PlayAsync(
        ulong guildId,
        string message,
        VoxClipGroup group,
        VoxPlaybackOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Parse a message and return token info (for UI preview without playing).
    /// </summary>
    VoxTokenPreview TokenizePreview(string message, VoxClipGroup group);
}
```

**Data Structures**:

```csharp
public record VoxPlaybackOptions
{
    /// <summary>Word gap in milliseconds (20-200).</summary>
    public int WordGapMs { get; init; } = 50;
}

public record VoxPlaybackResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public List<string> MatchedWords { get; init; } = new();
    public List<string> SkippedWords { get; init; } = new();
    public double EstimatedDurationSeconds { get; init; }
}

public record VoxTokenPreview
{
    public List<VoxTokenInfo> Tokens { get; init; } = new();
    public int MatchedCount { get; init; }
    public int SkippedCount { get; init; }
    public double EstimatedDurationSeconds { get; init; }
}

public record VoxTokenInfo
{
    public string Word { get; init; } = "";
    public bool HasClip { get; init; }
    public double DurationSeconds { get; init; }
}
```

**Pipeline**:
1. Tokenize input (split on whitespace, lowercase, strip punctuation)
2. Look up each token in `IVoxClipLibrary` for the requested group
3. Collect file paths for matched clips; track skipped words
4. Call `IVoxConcatenationService.ConcatenateAsync()` with matched clip paths
5. Create a temporary `Sound`-like object from the concatenated file
6. Call `IPlaybackService.PlayAsync()` to stream to Discord
7. Clean up temporary file
8. Return result with matched/skipped word lists

---

### Integration with Existing Services

#### PlaybackService (Reused As-Is)

VOX calls the existing `IPlaybackService.PlayAsync()` to stream concatenated audio to Discord. The concatenated output is wrapped in a `Sound` object:

```csharp
// Create a transient Sound object for the concatenated audio
var voxSound = new Sound
{
    Id = Guid.NewGuid(),
    GuildId = guildId,
    Name = $"vox-{DateTime.UtcNow:yyyyMMddHHmmss}",
    FileName = concatenatedFilePath,
    FileSizeBytes = new FileInfo(concatenatedFilePath).Length,
    DurationSeconds = estimatedDuration
};

await _playbackService.PlayAsync(guildId, voxSound, queueEnabled: false, cancellationToken: cancellationToken);
```

Note: The `FileName` here would be an absolute path to the temp file. This may require minor adaptation if `PlaybackService` assumes files are in the guild sounds directory. An alternative is to have the concatenation service output to a known temp location that `PlaybackService` can access.

#### AudioService (Reused As-Is)

Voice channel connection management is unchanged. VOX uses the existing audio connection.

---

## Clip Library

### Folder Structure

```
sounds/
  vox/                          # Half-Life VOX announcements
    warning.mp3
    alert.mp3
    attention.mp3
    all.mp3
    personnel.mp3
    security.mp3
    breach.mp3
    detected.mp3
    in.mp3
    sector.mp3
    ...
  fvox/                         # HEV suit (female VOX)
    health.mp3
    critical.mp3
    morphine.mp3
    administered.mp3
    seek.mp3
    medical.mp3
    attention.mp3
    ...
  hgrunt/                       # Military grunt radio
    copy.mp3
    roger.mp3
    affirmative.mp3
    negative.mp3
    target.mp3
    eliminated.mp3
    move.mp3
    ...
```

### File Naming Convention

- Filenames = clip name + `.mp3` extension
- Lowercase, alphanumeric + hyphens/underscores
- No spaces (use hyphens or underscores)
- Examples: `warning.mp3`, `all-clear.mp3`, `sector_seven.mp3`
- The clip name used in commands/UI is the filename without extension

### Clip Discovery

At application startup, `VoxClipLibrary` scans each folder:

```csharp
// Pseudocode for initialization
foreach (var group in Enum.GetValues<VoxClipGroup>())
{
    var folderPath = Path.Combine(_options.BasePath, group.ToString().ToLowerInvariant());
    if (!Directory.Exists(folderPath)) continue;

    var mp3Files = Directory.GetFiles(folderPath, "*.mp3");
    foreach (var file in mp3Files)
    {
        var name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
        var duration = await GetDurationAsync(file); // FFprobe
        var size = new FileInfo(file).Length;

        _clips[group][name] = new VoxClipInfo
        {
            Name = name,
            Group = group,
            FileSizeBytes = size,
            DurationSeconds = duration
        };
    }
}
```

### Tokenization Rules

Input text is parsed into tokens for clip lookup:

1. Split on whitespace
2. Convert to lowercase
3. Strip leading/trailing punctuation (periods, commas, exclamation marks, etc.)
4. Each token is looked up in the clip library for the active group
5. Tokens with no matching clip are skipped (not an error - just omitted from output)
6. Empty input or zero matched clips returns an error

**Examples**:

| Input | Tokens | Matched (assuming vox group) |
|-------|--------|------------------------------|
| `"warning security breach"` | `[warning, security, breach]` | All matched |
| `"attention all personnel!"` | `[attention, all, personnel]` | All matched |
| `"hello world"` | `[hello, world]` | Depends on clip availability |
| `"sector 7"` | `[sector, 7]` | `sector` matched, `7` skipped if no `7.mp3` |

---

## Audio Pipeline

### Complete Flow

```
User: /vox message:"warning security breach detected in sector c"
  |
  v
1. VoxModule receives command
   - User in voice channel? Check
   - Rate limit OK? Check
   |
   v
2. VoxService.PlayAsync()
   |
   v
3. Tokenize: "warning security breach detected in sector c"
   -> [warning, security, breach, detected, in, sector, c]
   |
   v
4. VoxClipLibrary.GetClip() for each token (vox group)
   - warning   -> sounds/vox/warning.mp3   (found)
   - security  -> sounds/vox/security.mp3  (found)
   - breach    -> sounds/vox/breach.mp3    (found)
   - detected  -> sounds/vox/detected.mp3  (found)
   - in        -> sounds/vox/in.mp3        (found)
   - sector    -> sounds/vox/sector.mp3    (found)
   - c         -> sounds/vox/c.mp3         (found)
   |
   v
5. VoxConcatenationService.ConcatenateAsync()
   Concatenate 7 MP3 clips with 50ms silence gaps:
   [warning.mp3][50ms gap][security.mp3][50ms gap]...
   -> Temporary concatenated audio file
   |
   v
6. PlaybackService.PlayAsync()
   Stream concatenated audio to Discord via FFmpeg -> Opus
   |
   v
7. Clean up temporary file

8. Return result:
   { matched: [warning,security,breach,detected,in,sector,c], skipped: [] }
```

### Performance Characteristics

| Operation | Time | Notes |
|-----------|------|-------|
| Tokenization | <1ms | String split + lookup |
| Clip resolution | <1ms | Dictionary lookup per token |
| Concatenation (FFmpeg) | 100-500ms | Depends on clip count and sizes |
| Playback start | ~100ms | Existing PlaybackService latency |
| **Total** | ~200-600ms | From command to first audio |

---

## Slash Commands

### /vox

Play a VOX announcement using clips from the `vox` group.

```csharp
[SlashCommand("vox", "Play a VOX announcement (Half-Life PA system)")]
[RequireGuildActive]
[RequireAudioEnabled]
[RequireVoiceChannel]
[RateLimit(5, 10)]
public async Task VoxAsync(
    [Summary("message", "Words to announce (space-separated)")]
    [MaxLength(500)]
    string message,

    [Summary("gap", "Silence between words in ms (20-200, default 50)")]
    [MinValue(20)]
    [MaxValue(200)]
    int? gap = null)
```

### /fvox

Play a FVOX announcement using clips from the `fvox` group.

```csharp
[SlashCommand("fvox", "Play a FVOX announcement (Half-Life HEV suit)")]
[RequireGuildActive]
[RequireAudioEnabled]
[RequireVoiceChannel]
[RateLimit(5, 10)]
public async Task FvoxAsync(
    [Summary("message", "Words to announce (space-separated)")]
    [MaxLength(500)]
    string message,

    [Summary("gap", "Silence between words in ms (20-200, default 50)")]
    [MinValue(20)]
    [MaxValue(200)]
    int? gap = null)
```

### /hgrunt

Play an HGrunt announcement using clips from the `hgrunt` group.

```csharp
[SlashCommand("hgrunt", "Play an HGrunt announcement (Half-Life military radio)")]
[RequireGuildActive]
[RequireAudioEnabled]
[RequireVoiceChannel]
[RateLimit(5, 10)]
public async Task HgruntAsync(
    [Summary("message", "Words to announce (space-separated)")]
    [MaxLength(500)]
    string message,

    [Summary("gap", "Silence between words in ms (20-200, default 50)")]
    [MinValue(20)]
    [MaxValue(200)]
    int? gap = null)
```

### Autocomplete

Each command's `message` parameter uses an autocomplete handler that suggests available clip names as the user types. Since Discord autocomplete fires on partial input, the handler provides word-level suggestions for the last word being typed.

```csharp
public class VoxAutocompleteHandler : AutocompleteHandler
{
    private readonly IVoxClipLibrary _clipLibrary;
    private readonly VoxClipGroup _group;

    public override async Task<AutocompletionResult> GenerateSuggestionsAsync(
        IInteractionContext context,
        IAutocompleteInteraction interaction,
        IParameterInfo parameter,
        IServiceProvider services)
    {
        var currentInput = interaction.Data.Current.Value?.ToString() ?? "";

        // Get the last word being typed (for mid-sentence autocomplete)
        var words = currentInput.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lastWord = words.LastOrDefault() ?? "";
        var prefix = words.Length > 1
            ? string.Join(' ', words.Take(words.Length - 1)) + " "
            : "";

        var suggestions = _clipLibrary.SearchClips(_group, lastWord, 25)
            .Select(clip => new AutocompleteResult(
                $"{prefix}{clip.Name}",     // Full text with suggestion appended
                $"{prefix}{clip.Name}"))
            .ToList();

        return AutocompletionResult.FromSuccess(suggestions);
    }
}
```

Three derived autocomplete handlers (one per group), or a single handler with group resolution based on the command name.

### Module Structure

All three commands can live in a single module or separate modules. Single module approach:

```csharp
[Group("vox")]  // Not actually grouped - each is a top-level command
public class VoxModule : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IVoxService _voxService;

    // Each method delegates to a shared helper with the appropriate group
    private async Task PlayVoxAsync(string message, VoxClipGroup group, int? gap)
    {
        var options = new VoxPlaybackOptions
        {
            WordGapMs = gap ?? 50
        };

        await DeferAsync();

        var result = await _voxService.PlayAsync(
            Context.Guild.Id, message, group, options);

        if (result.Success)
        {
            var response = $"Playing: {string.Join(" ", result.MatchedWords)}";
            if (result.SkippedWords.Any())
                response += $"\nSkipped (no clip): {string.Join(", ", result.SkippedWords)}";
            await FollowupAsync(response, ephemeral: true);
        }
        else
        {
            await FollowupAsync($"Error: {result.ErrorMessage}", ephemeral: true);
        }
    }
}
```

---

## Configuration

### appsettings.json

```json
{
  "Vox": {
    "BasePath": "./sounds",
    "DefaultWordGapMs": 50,
    "MaxMessageWords": 50,
    "MaxMessageLength": 500
  }
}
```

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `BasePath` | string | `"./sounds"` | Base directory containing `vox/`, `fvox/`, `hgrunt/` folders |
| `DefaultWordGapMs` | int | `50` | Default silence between clips (ms) |
| `MaxMessageWords` | int | `50` | Maximum words per message |
| `MaxMessageLength` | int | `500` | Maximum characters per message |

### Options Class

```csharp
public class VoxOptions
{
    public const string SectionName = "Vox";

    public string BasePath { get; set; } = "./sounds";
    public int DefaultWordGapMs { get; set; } = 50;
    public int MaxMessageWords { get; set; } = 50;
    public int MaxMessageLength { get; set; } = 500;
}
```

---

## Dependency Injection

### Registration Extension

```csharp
public static class VoxServiceExtensions
{
    public static IServiceCollection AddVox(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<VoxOptions>(
            configuration.GetSection(VoxOptions.SectionName));

        // Clip library (singleton - loaded once at startup, held in memory)
        services.AddSingleton<IVoxClipLibrary, VoxClipLibrary>();

        // Concatenation service (singleton - stateless FFmpeg operations)
        services.AddSingleton<IVoxConcatenationService, VoxConcatenationService>();

        // VOX orchestrator (scoped - per-request pipeline coordination)
        services.AddScoped<IVoxService, VoxService>();

        return services;
    }
}
```

### Startup Initialization

```csharp
// In Program.cs
builder.Services.AddVox(builder.Configuration);

// After app.Build(), initialize the clip library
var clipLibrary = app.Services.GetRequiredService<IVoxClipLibrary>();
await clipLibrary.InitializeAsync();
```

Or use `IHostedService` to initialize during startup:

```csharp
public class VoxClipLibraryInitializer : IHostedService
{
    private readonly IVoxClipLibrary _clipLibrary;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _clipLibrary.InitializeAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

---

## API Endpoints

### Portal VOX Endpoints

#### `GET /api/portal/vox/{guildId}/clips`

Get available clips for a group with optional search.

**Query Parameters**:
- `group` (required): `vox`, `fvox`, or `hgrunt`
- `search` (optional): Filter clips by name

**Response**:
```json
{
  "group": "vox",
  "clips": [
    { "name": "attention", "durationSeconds": 0.8, "fileSizeBytes": 12800 },
    { "name": "warning", "durationSeconds": 0.6, "fileSizeBytes": 9600 },
    { "name": "all", "durationSeconds": 0.3, "fileSizeBytes": 4800 }
  ],
  "totalClips": 187
}
```

#### `GET /api/portal/vox/{guildId}/preview`

Parse a message and return token preview (which words have clips, which don't).

**Query Parameters**:
- `message` (required): Text to tokenize
- `group` (required): `vox`, `fvox`, or `hgrunt`

**Response**:
```json
{
  "tokens": [
    { "word": "attention", "hasClip": true, "durationSeconds": 0.8 },
    { "word": "all", "hasClip": true, "durationSeconds": 0.3 },
    { "word": "personnel", "hasClip": true, "durationSeconds": 1.1 },
    { "word": "hello", "hasClip": false, "durationSeconds": 0.0 }
  ],
  "matchedCount": 3,
  "skippedCount": 1,
  "estimatedDurationSeconds": 2.35
}
```

#### `POST /api/portal/vox/{guildId}/play`

Play a VOX announcement.

**Request**:
```json
{
  "message": "attention all personnel security breach",
  "group": "vox",
  "wordGapMs": 50
}
```

**Response**:
```json
{
  "success": true,
  "matchedWords": ["attention", "all", "personnel", "security", "breach"],
  "skippedWords": [],
  "estimatedDurationSeconds": 3.5
}
```

#### `POST /api/portal/vox/{guildId}/stop`

Stop current playback (delegates to existing PlaybackService stop).

**Response**:
```json
{
  "success": true,
  "message": "Playback stopped"
}
```

---

## Security & Rate Limiting

### Authorization

**Portal Pages**:
- `[Authorize(Policy = "PortalGuildMember")]` - Guild membership required
- Rate limiting enforced per-user

**Slash Commands**:
- `[RequireGuildActive]` - Guild must be active
- `[RequireAudioEnabled]` - Audio must be enabled for guild
- `[RequireVoiceChannel]` - User must be in voice channel

### Rate Limiting

```csharp
[RateLimit(5, 10)]  // 5 invocations per 10 seconds (matches existing soundboard)
```

### Input Validation

| Input | Validation | Error |
|-------|-----------|-------|
| Message text | Max 500 chars, max 50 words | 400 Bad Request |
| Word gap | 20-200 ms | 400 Bad Request |
| Group | Must be `vox`, `fvox`, or `hgrunt` | 400 Bad Request |
| Empty message | At least 1 word required | 400 Bad Request |
| Zero matched clips | At least 1 clip must match | 400 (with list of unmatched words) |

### Filesystem Safety

Clip names are derived from filenames at startup (not user input), so path traversal is not a risk for clip resolution. User input is only used as dictionary keys for lookup - never directly in file paths.

---

## Future Expansion

The simplified design leaves room for the following enhancements (see v1.0 spec for detailed designs):

- **PA System Filters** - FFmpeg bandpass/compression/distortion filter chain for authentic Half-Life sound (Off/Light/Heavy presets)
- **Azure TTS word generation** - Generate new clips on-demand for words not in the static library
- **Per-guild custom clips** - Allow guilds to upload/generate their own word clips
- **Sentence Builder UI** - Drag-and-drop composition interface
- **Word bank management** - Admin UI for bulk generation, import/export
- **Custom filter parameter UI** - Sliders for highpass, lowpass, compression, distortion
- **VoxMessage audit logging** - Database tracking of VOX usage

---

## Related Documentation

- **[vox-ui-spec.md](vox-ui-spec.md)** - VOX Portal UI/UX design
- **[tts-support.md](tts-support.md)** - Existing TTS system
- **[audio-dependencies.md](audio-dependencies.md)** - FFmpeg, libsodium, opus setup
- **[component-api.md](component-api.md)** - Razor UI components
- **[design-system.md](design-system.md)** - Design tokens and colors

---

## Implementation Notes

### Phased Rollout

**Phase 1**: Core services (clip library, concatenation, VoxService) + slash commands
**Phase 2**: Portal UI (clip browser, autocomplete input, playback)
**Phase 3**: Polish (error handling edge cases, performance tuning)

### Testing Strategy

**Unit Tests**:
- Tokenization (whitespace splitting, punctuation stripping, edge cases)
- Clip library search (prefix match, substring match, case insensitivity)
- Options validation

**Integration Tests**:
- Concatenation with real MP3 files via FFmpeg
- Full pipeline: tokenize -> resolve -> concatenate -> verify output audio

---

**Last Updated**: 2026-02-02
**Version**: 2.0 (Simplified from v1.0 - static clip library, no TTS generation)
