# Tier A Implementation Plan: Audio Preview & VOX History

**Issues:** #1774 (F12), #1775 (F13), #1776 (F14)
**Milestone:** 48 — Audio Portal UX Improvements
**Date:** 2026-03-12

---

## Overview

Three high-priority features that add client-side audio preview for Soundboard/TTS and message history + favorites for VOX. F12 and F13 share infrastructure (browser audio playback); F14 is independent (data layer + UI).

**Recommended execution order:** F12 → F13 → F14 (or F14 in parallel with F12/F13).

---

## F12: Sound Preview (Client-Side Playback Before Broadcasting)

**Goal:** Let users hear a sound in their browser before it plays in the voice channel.

### Backend Changes

#### 1. New endpoint: `GET /api/portal/soundboard/{guildId}/sounds/{soundId}/audio`

**File:** `src/DiscordBot.Bot/Controllers/PortalSoundboardController.cs`

Add a streaming endpoint that serves the raw sound file to the browser:

```csharp
[HttpGet("sounds/{soundId}/audio")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetSoundAudio(ulong guildId, Guid soundId, CancellationToken cancellationToken)
```

**Implementation details:**
- Look up `Sound` entity via `_soundService.GetByIdAsync(soundId)` — verify it belongs to `guildId`
- Resolve file path via `ISoundFileService.GetSoundFilePath(guildId, sound.FileName)`
- Verify file exists via `ISoundFileService.SoundFileExists(guildId, sound.FileName)`
- Return `PhysicalFile(filePath, contentType)` where `contentType` is derived from extension:
  - `.mp3` → `audio/mpeg`
  - `.wav` → `audio/wav`
  - `.ogg` → `audio/ogg`
  - `.m4a` → `audio/mp4`
- Inject `ISoundFileService` into the controller (add to constructor). It's not currently injected — the controller uses `ISoundboardOrchestrationService` for playback which handles file resolution internally.
- Apply the same `IsAudioGloballyEnabledAsync()` and guild audio settings checks that other endpoints use.
- **Do NOT** increment `PlayCount` — previews are not plays.

**Security considerations:**
- Path traversal: `GetSoundFilePath` already resolves from a controlled base path + guildId + fileName, so no user-controlled path segments.
- Authorization: Existing `[Authorize(Policy = "PortalGuildMember")]` on the controller is sufficient.
- Add `[ResponseCache(Duration = 300)]` for 5-minute client caching since sound files are immutable.

### Frontend Changes

**File:** `src/DiscordBot.Bot/Pages/Portal/Soundboard/Index.cshtml`

#### 2. Modify sound card HTML — add Preview button

Current sound card structure has a single click target that calls `playSound(soundId)`. Change to two buttons:

```html
<!-- Preview button (headphone icon) — plays in browser -->
<button class="btn btn-sm btn-outline-secondary preview-btn"
        onclick="previewSound('@sound.Id', '@sound.Name')"
        title="Preview in browser">
    <i class="bi bi-headphones"></i>
</button>

<!-- Play button (speaker icon) — broadcasts to channel (existing behavior) -->
<button class="btn btn-sm btn-primary play-btn"
        onclick="playSound('@sound.Id')"
        title="Play in voice channel">
    <i class="bi bi-play-fill"></i>
</button>
```

#### 3. Add client-side audio preview JavaScript

**File:** `src/DiscordBot.Bot/wwwroot/js/portal-soundboard.js` (or inline `<script>` in the Razor page, matching current pattern)

```javascript
let previewAudio = null; // Single shared Audio element

function previewSound(soundId, soundName) {
    // Stop any current preview
    if (previewAudio) {
        previewAudio.pause();
        previewAudio.currentTime = 0;
    }

    const btn = document.querySelector(`[onclick*="previewSound('${soundId}'"]`);
    if (btn) btn.disabled = true;

    previewAudio = new Audio(`/api/portal/soundboard/${guildId}/sounds/${soundId}/audio`);
    previewAudio.onended = () => { if (btn) btn.disabled = false; };
    previewAudio.onerror = () => {
        showToast('Failed to preview sound', 'error');
        if (btn) btn.disabled = false;
    };
    previewAudio.play();
}
```

**Key behavior:**
- Only one preview plays at a time (clicking another stops the current one).
- Preview button shows disabled state while audio is playing.
- No voice channel connection required for preview.
- Reuse existing `showToast()` for errors.
- `guildId` is already available as a JS variable on the page (from the Razor model).

#### 4. CSS for button layout

Ensure the two buttons fit in the sound card without breaking layout. The preview button should be visually secondary (outline style) and the play button remains primary.

### Files Modified (F12)

| File | Change |
|------|--------|
| `src/DiscordBot.Bot/Controllers/PortalSoundboardController.cs` | Add `GetSoundAudio` endpoint, inject `ISoundFileService` |
| `src/DiscordBot.Bot/Pages/Portal/Soundboard/Index.cshtml` | Add preview button to sound cards, add `previewSound()` JS |

### Acceptance Criteria

- [ ] User can preview a sound in the browser without being in a voice channel
- [ ] Clicking "Play" still broadcasts to the voice channel (no regression)
- [ ] Preview and Play buttons have distinct icons and tooltips
- [ ] Only one preview plays at a time
- [ ] Mobile touch targets are adequate (min 44x44px)
- [ ] Preview does not increment play count

---

## F13: TTS Message Preview (Play to Browser, Not Channel)

**Goal:** Let users hear their TTS message locally before sending to the voice channel.

### Backend Changes

#### 1. New endpoint: `POST /api/portal/tts/{guildId}/preview`

**File:** `src/DiscordBot.Bot/Controllers/PortalTtsController.cs`

Add a preview endpoint that synthesizes speech and returns audio to the browser instead of streaming to Discord:

```csharp
[HttpPost("preview")]
[ProducesResponseType(StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorDto), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> PreviewTts(
    ulong guildId,
    [FromBody] SendTtsRequest request,
    CancellationToken cancellationToken)
```

**Implementation details:**
- Reuse the **exact same synthesis logic** from the existing `SendTts` method. The three synthesis branches already exist (lines 283-331 of the current controller):
  1. SSML provided → `SynthesizeSpeechAsync(request.Ssml, null, SynthesisMode.Ssml, ct)`
  2. Style provided → Build SSML with `ISsmlBuilder`, then synthesize
  3. Plain text → `SynthesizeSpeechAsync(request.Message, options, ct)`
- **Extract a private helper** to avoid duplicating the synthesis branching logic:
  ```csharp
  private async Task<Stream> SynthesizeFromRequestAsync(SendTtsRequest request, CancellationToken ct)
  ```
  Both `SendTts` and `PreviewTts` call this helper.
- The `ITtsService.SynthesizeSpeechAsync` returns PCM audio (48kHz, 16-bit, stereo). For browser playback, convert to WAV by prepending a WAV header. PCM streams have a known format, so write a 44-byte WAV header with:
  - Sample rate: 48000
  - Bits per sample: 16
  - Channels: 2 (stereo)
  - Data size: stream length
- Return `File(wavStream, "audio/wav")`.
- **Do NOT** require voice channel connection (preview is local-only).
- **Do NOT** save to TTS message history (previews are not sends).
- Apply `IsAudioGloballyEnabledAsync()` and TTS enabled checks, but skip the voice channel connection check.
- Apply the same rate limiting consideration as `SendTts` — synthesis costs Azure credits. Consider a stricter rate limit for preview (e.g., 5 per minute per user) via a simple in-memory throttle using the existing `ConcurrentDictionary` pattern.

**WAV header helper** — add a small private method or static utility:

```csharp
private static MemoryStream WrapPcmAsWav(Stream pcmStream, int sampleRate = 48000, int bitsPerSample = 16, int channels = 2)
{
    var pcmData = new MemoryStream();
    pcmStream.CopyTo(pcmData);
    var dataLength = (int)pcmData.Length;

    var wav = new MemoryStream(44 + dataLength);
    using var writer = new BinaryWriter(wav, System.Text.Encoding.UTF8, leaveOpen: true);
    // RIFF header
    writer.Write("RIFF"u8);
    writer.Write(36 + dataLength);
    writer.Write("WAVE"u8);
    // fmt chunk
    writer.Write("fmt "u8);
    writer.Write(16); // chunk size
    writer.Write((short)1); // PCM format
    writer.Write((short)channels);
    writer.Write(sampleRate);
    writer.Write(sampleRate * channels * bitsPerSample / 8); // byte rate
    writer.Write((short)(channels * bitsPerSample / 8)); // block align
    writer.Write((short)bitsPerSample);
    // data chunk
    writer.Write("data"u8);
    writer.Write(dataLength);
    pcmData.Position = 0;
    pcmData.CopyTo(wav);
    wav.Position = 0;
    return wav;
}
```

### Frontend Changes

**File:** `src/DiscordBot.Bot/wwwroot/js/portal-tts.js`

#### 2. Add Preview button to TTS UI

**File:** `src/DiscordBot.Bot/Pages/Portal/TTS/Index.cshtml`

Add a "Preview" button next to the existing "Send" button:

```html
<button id="previewBtn" class="btn btn-outline-secondary" onclick="previewTts()" title="Preview in browser">
    <i class="bi bi-headphones"></i> Preview
</button>
<button id="sendBtn" class="btn btn-primary" onclick="sendTts()">
    <i class="bi bi-send"></i> Send
</button>
```

#### 3. Add preview JavaScript

**File:** `src/DiscordBot.Bot/wwwroot/js/portal-tts.js`

```javascript
let ttsPreviewAudio = null;

async function previewTts() {
    const message = document.getElementById('ttsMessage').value.trim();
    if (!message) return;

    const previewBtn = document.getElementById('previewBtn');
    previewBtn.disabled = true;
    previewBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span> Generating...';

    // Stop any current preview
    if (ttsPreviewAudio) {
        ttsPreviewAudio.pause();
        ttsPreviewAudio = null;
    }

    try {
        const body = buildTtsRequestBody(); // reuse existing helper that collects voice/speed/pitch/style/ssml
        const response = await fetch(`/api/portal/tts/${guildId}/preview`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });

        if (!response.ok) {
            const err = await response.json();
            showToast(err.detail || 'Preview failed', 'error');
            return;
        }

        const blob = await response.blob();
        const url = URL.createObjectURL(blob);
        ttsPreviewAudio = new Audio(url);
        ttsPreviewAudio.onended = () => URL.revokeObjectURL(url);
        ttsPreviewAudio.play();
    } catch (e) {
        showToast('Preview failed: ' + e.message, 'error');
    } finally {
        previewBtn.disabled = false;
        previewBtn.innerHTML = '<i class="bi bi-headphones"></i> Preview';
    }
}
```

**Key behavior:**
- Shows loading spinner during Azure synthesis (can take 1-3 seconds).
- Reuses the same request body builder as `sendTts()` so all settings (voice, speed, pitch, style, SSML) are included.
- Does NOT require voice channel connection.
- Blob URL is revoked after playback to avoid memory leaks.

**Important:** Extract the existing `sendTts()` request body construction into a shared `buildTtsRequestBody()` function if one doesn't already exist. Both Send and Preview need the same payload.

### Files Modified (F13)

| File | Change |
|------|--------|
| `src/DiscordBot.Bot/Controllers/PortalTtsController.cs` | Add `PreviewTts` endpoint, extract `SynthesizeFromRequestAsync` helper, add `WrapPcmAsWav` utility |
| `src/DiscordBot.Bot/Pages/Portal/TTS/Index.cshtml` | Add Preview button next to Send |
| `src/DiscordBot.Bot/wwwroot/js/portal-tts.js` | Add `previewTts()`, extract `buildTtsRequestBody()` |

### Acceptance Criteria

- [ ] User can preview TTS message in the browser without voice channel connection
- [ ] Preview uses the same voice, speed, pitch, style, and SSML settings as Send
- [ ] Preview button shows loading spinner during synthesis
- [ ] Send button still works identically (no regression)
- [ ] Preview does not save to TTS message history
- [ ] Audio plays correctly in Chrome, Firefox, and Safari (WAV format is universally supported)
- [ ] SSML emphasis markers are included in preview audio

---

## F14: VOX Message History and Saved Favorites

**Goal:** Store recent VOX messages and let users save favorites for one-click replay.

### Data Layer Changes

#### 1. New entity: `VoxMessageHistory`

**File:** `src/DiscordBot.Core/Entities/VoxMessageHistory.cs` (new)

```csharp
namespace DiscordBot.Core.Entities;

public class VoxMessageHistory
{
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ClipGroup { get; set; } = string.Empty;   // "vox", "fvox", "hgrunt"
    public int WordGapMs { get; set; }                       // preserve playback settings
    public bool IsFavorite { get; set; }
    public DateTime PlayedAt { get; set; }
    public Guild? Guild { get; set; }
}
```

**Design notes:**
- Follows the `UserSoundFavorite` and `TtsMessage` patterns already in the codebase.
- `IsFavorite` is a flag on the history row, not a separate entity. This keeps the model simple — favorites are just history entries that are pinned.
- `ClipGroup` and `WordGapMs` are stored so replay uses the original settings.
- The issue suggests considering a shared `AudioMessageHistory` base entity. **Don't do this yet** — YAGNI. TTS already has `TtsMessage` with different fields (Voice, DurationSeconds). Keep them separate and refactor later if/when F25 (TTS history with replay) ships and the pattern is proven.

#### 2. New repository interface: `IVoxMessageHistoryRepository`

**File:** `src/DiscordBot.Core/Interfaces/IVoxMessageHistoryRepository.cs` (new)

```csharp
namespace DiscordBot.Core.Interfaces;

public interface IVoxMessageHistoryRepository : IRepository<VoxMessageHistory>
{
    Task<IReadOnlyList<VoxMessageHistory>> GetRecentAsync(
        ulong userId, ulong guildId, int limit = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VoxMessageHistory>> GetFavoritesAsync(
        ulong userId, ulong guildId,
        CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(int id, bool isFavorite,
        CancellationToken cancellationToken = default);
}
```

#### 3. Repository implementation

**File:** `src/DiscordBot.Infrastructure/Data/Repositories/VoxMessageHistoryRepository.cs` (new)

Follow the same pattern as `UserSoundFavoriteRepository.cs`:
- Inherit from `Repository<VoxMessageHistory>` base class
- Implement the three custom methods
- `GetRecentAsync`: `OrderByDescending(x => x.PlayedAt).Take(limit)` filtered by userId + guildId
- `GetFavoritesAsync`: Same filter + `Where(x => x.IsFavorite)`, ordered by `PlayedAt` descending
- `SetFavoriteAsync`: Load by ID, set `IsFavorite`, save

#### 4. EF Core configuration

**File:** `src/DiscordBot.Infrastructure/Data/Configurations/VoxMessageHistoryConfiguration.cs` (new)

Follow the pattern from `UserSoundFavoriteConfiguration.cs`:

```csharp
public class VoxMessageHistoryConfiguration : IEntityTypeConfiguration<VoxMessageHistory>
{
    public void Configure(EntityTypeBuilder<VoxMessageHistory> builder)
    {
        builder.ToTable("VoxMessageHistory");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GuildId).IsRequired();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.Message).IsRequired().HasMaxLength(500);
        builder.Property(x => x.ClipGroup).IsRequired().HasMaxLength(20);
        builder.Property(x => x.WordGapMs).IsRequired();
        builder.Property(x => x.IsFavorite).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.PlayedAt).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.GuildId, x.PlayedAt });
        builder.HasIndex(x => new { x.UserId, x.GuildId, x.IsFavorite });
    }
}
```

#### 5. Register in DbContext

**File:** `src/DiscordBot.Infrastructure/Data/BotDbContext.cs`

Add `DbSet<VoxMessageHistory> VoxMessageHistory { get; set; }` and apply configuration in `OnModelCreating`.

#### 6. Register repository in DI

**File:** `src/DiscordBot.Infrastructure/Extensions/ServiceCollectionExtensions.cs`

Add: `services.AddScoped<IVoxMessageHistoryRepository, VoxMessageHistoryRepository>();`

#### 7. EF Migrations (both providers)

Run both migration commands per CLAUDE.md:

```bash
# SQLite
dotnet ef migrations add AddVoxMessageHistory --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context SqliteBotDbContext -o Migrations/Sqlite

# PostgreSQL
dotnet ef migrations add AddVoxMessageHistory --project src/DiscordBot.Infrastructure --startup-project src/DiscordBot.Bot --context PostgresBotDbContext -o Migrations/Postgresql
```

### Backend Changes

#### 8. Save history on VOX play

**File:** `src/DiscordBot.Bot/Controllers/PortalVoxController.cs`

In the existing `Play` endpoint, after successful playback, save to history:

```csharp
// After successful play, save to history
var userId = User.GetDiscordUserId(); // existing extension method
await _historyRepository.AddAsync(new VoxMessageHistory
{
    GuildId = guildId,
    UserId = userId,
    Message = request.Message,
    ClipGroup = request.Group ?? "vox",
    WordGapMs = request.WordGapMs ?? 50,
    IsFavorite = false,
    PlayedAt = DateTime.UtcNow
}, cancellationToken);
```

Inject `IVoxMessageHistoryRepository` into the controller constructor.

#### 9. New history/favorites API endpoints

**File:** `src/DiscordBot.Bot/Controllers/PortalVoxController.cs`

Add these endpoints:

```
GET  /api/portal/vox/{guildId}/history         → GetHistory(guildId)
GET  /api/portal/vox/{guildId}/favorites        → GetFavorites(guildId)
POST /api/portal/vox/{guildId}/history/{id}/favorite   → ToggleFavorite(guildId, id)
DELETE /api/portal/vox/{guildId}/history/{id}   → DeleteHistoryEntry(guildId, id)
```

- `GetHistory`: Return last 20 messages for the current user in this guild.
- `GetFavorites`: Return all favorited messages for the current user in this guild.
- `ToggleFavorite`: Toggle `IsFavorite` flag. Verify the entry belongs to the requesting user.
- `DeleteHistoryEntry`: Remove a history entry. Verify ownership.

Response DTO shape:

```json
{
  "id": 42,
  "message": "enemy spotted",
  "clipGroup": "vox",
  "wordGapMs": 50,
  "isFavorite": true,
  "playedAt": "2026-03-12T10:30:00Z"
}
```

### Frontend Changes

#### 10. VOX history/favorites UI

**File:** `src/DiscordBot.Bot/Pages/Portal/VOX/Index.cshtml`

Add a collapsible sidebar or dropdown panel for history and favorites. Recommended layout:

```
┌──────────────────────────────────────────────┐
│ VOX Message Input                            │
│ [____________________________] [▶ Play]      │
│                                              │
│ ┌─ Favorites ──────────────────────────────┐ │
│ │ ★ "enemy spotted" (vox)        [▶] [✕]  │ │
│ │ ★ "fire in the hole" (fvox)    [▶] [✕]  │ │
│ └──────────────────────────────────────────┘ │
│                                              │
│ ┌─ Recent ─────────────────────────────────┐ │
│ │ "hello world" (vox)            [▶] [★]   │ │
│ │ "move out" (hgrunt)            [▶] [★]   │ │
│ │ "medic" (vox)                  [▶] [★]   │ │
│ └──────────────────────────────────────────┘ │
└──────────────────────────────────────────────┘
```

- **Play button** `[▶]`: Replays the message by calling the existing `POST /play` endpoint with the stored `message`, `clipGroup`, and `wordGapMs`.
- **Favorite toggle** `[★]`: Calls `POST /history/{id}/favorite` to toggle.
- **Remove** `[✕]` (on favorites only): Unfavorites the entry.
- Favorites section is always visible; Recent section is collapsible.
- Load history on page load via `GET /history` and `GET /favorites`.
- Update history list after each play (prepend new entry, trim to 20).

#### 11. VOX history JavaScript

**File:** `src/DiscordBot.Bot/wwwroot/js/portal-vox.js` (or inline script, match existing pattern)

```javascript
async function loadVoxHistory() {
    const [historyRes, favoritesRes] = await Promise.all([
        fetch(`/api/portal/vox/${guildId}/history`),
        fetch(`/api/portal/vox/${guildId}/favorites`)
    ]);
    // Render both lists
}

async function replayFromHistory(entry) {
    // Call existing play endpoint with entry.message, entry.clipGroup, entry.wordGapMs
    await fetch(`/api/portal/vox/${guildId}/play`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
            message: entry.message,
            group: entry.clipGroup,
            wordGapMs: entry.wordGapMs
        })
    });
}

async function toggleFavorite(historyId) {
    await fetch(`/api/portal/vox/${guildId}/history/${historyId}/favorite`, { method: 'POST' });
    await loadVoxHistory(); // Refresh both lists
}
```

### Files Modified (F14)

| File | Change | Type |
|------|--------|------|
| `src/DiscordBot.Core/Entities/VoxMessageHistory.cs` | New entity | New |
| `src/DiscordBot.Core/Interfaces/IVoxMessageHistoryRepository.cs` | New interface | New |
| `src/DiscordBot.Infrastructure/Data/Repositories/VoxMessageHistoryRepository.cs` | New implementation | New |
| `src/DiscordBot.Infrastructure/Data/Configurations/VoxMessageHistoryConfiguration.cs` | EF config | New |
| `src/DiscordBot.Infrastructure/Data/BotDbContext.cs` | Add DbSet | Edit |
| `src/DiscordBot.Infrastructure/Extensions/ServiceCollectionExtensions.cs` | Register repository | Edit |
| `src/DiscordBot.Infrastructure/Migrations/Sqlite/...` | Migration | Generated |
| `src/DiscordBot.Infrastructure/Migrations/Postgresql/...` | Migration | Generated |
| `src/DiscordBot.Bot/Controllers/PortalVoxController.cs` | Save history on play, add 4 endpoints | Edit |
| `src/DiscordBot.Bot/Pages/Portal/VOX/Index.cshtml` | History/favorites UI | Edit |

### Acceptance Criteria

- [ ] Recent VOX messages are stored and displayed (last 20 per user per guild)
- [ ] Users can mark messages as favorites
- [ ] One-click replay from history or favorites
- [ ] Replay uses the original clip group and word gap settings
- [ ] History is per-user, per-guild (users cannot see each other's history)
- [ ] Migrations generated for both SQLite and PostgreSQL
- [ ] Favorite toggle is idempotent (double-click safe)
- [ ] History entry ownership is verified on all mutations

---

## Cross-Cutting Concerns

### Error handling
All three features should use the existing `ApiErrorDto` pattern with specific `ErrorCode` values. Reuse the existing `showToast()` function for frontend error display.

### Testing
- **F12:** Test that `GetSoundAudio` returns correct content type, 404 for missing sounds, 403 for wrong guild
- **F13:** Test that `PreviewTts` returns WAV audio, respects all synthesis modes, doesn't require voice connection
- **F14:** Test repository methods, test ownership verification on mutations, test history limit (20)

### Performance
- **F12:** `[ResponseCache(Duration = 300)]` on sound audio endpoint; sound files are small (typically <1MB)
- **F13:** TTS preview hits Azure each time — consider a short server-side cache keyed on `(message, voice, speed, pitch, style)` hash if abuse becomes a concern, but don't implement preemptively
- **F14:** Composite index on `(UserId, GuildId, PlayedAt)` ensures history queries are fast. Consider a background cleanup job if history grows unbounded — but with 20-entry display limit and per-user scoping, this is low risk

### Agent definition update
After completing these features, update `.claude/agents/audio-voice` agent definition to reference:
- The new sound audio streaming endpoint
- The TTS preview endpoint and WAV header utility
- The VoxMessageHistory entity and repository pattern
