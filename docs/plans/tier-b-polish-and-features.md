# Milestone 48 — Batch 2: Polish & User-Value Features

> **Status:** Planning
> **Date:** 2026-03-12
> **Issues:** #1806, #1800, #1799, #1778, #1779, #1787
> **Predecessor:** Batch 1 (Previews) — F12, F13, F14 ✅

## Overview

Six issues across two tracks. Three tech-debt items clean up duplication introduced during the preview features; three user-facing features add ownership, discoverability, and personalization to the audio portal.

| # | Title | Priority | Scope | Track |
|---|-------|----------|-------|-------|
| #1806 | Prevent double-click on soundboard upload button | Medium | Small | Tech Debt |
| #1800 | Eliminate TTS desktop/mobile control duplication | Medium | Medium | Tech Debt |
| #1799 | Extract shared SSML parsing logic | Medium | Medium | Tech Debt |
| #1778 | F16: Self-deletion for sound uploaders | Medium | Medium | Feature |
| #1779 | F17: Soundboard sort options | Medium | Medium | Feature |
| #1787 | F24: Custom TTS preset saving | Medium | Large | Feature |

---

## Implementation Sequence

```
Phase 1 (parallel, no dependencies):
  #1806 — Double-click prevention          (small, ~30 min)
  #1800 — TTS control dedup               (medium, ~2 hours)

Phase 2 (after Phase 1):
  #1799 — SSML parsing extraction          (medium, ~1.5 hours)

Phase 3 (parallel, after Phase 2):
  #1778 — Sound self-deletion              (medium, ~3 hours)
  #1779 — Soundboard sort options          (medium, ~2 hours)
    (coordinate PortalSoundViewModel changes — #1778 first)

Phase 4 (after Phase 2, can overlap Phase 3):
  #1787 — Custom TTS preset saving         (large, ~5 hours)
```

---

## Issue Details

### #1806: Prevent Double-Click on Soundboard Upload Button

**Current state:** `uploadFile()` in `Pages/Portal/Soundboard/Index.cshtml` has an `isUploading` guard and calls `updateUploadButton()`, but a fast double-click can fire before the DOM disables the button.

**Files to modify:**
- `src/DiscordBot.Bot/Pages/Portal/Soundboard/Index.cshtml` — the inline `uploadFile()` function

**Implementation:**
1. Add `document.getElementById('uploadBtn').disabled = true;` as the first line after the guard check, before `isUploading = true`
2. Also add `pointer-events: none` style during upload for edge-case protection
3. Verify the button is re-enabled in both `xhr.onload` and `xhr.onerror` handlers (already done via `updateUploadButton()`)

**Acceptance criteria:**
- Rapidly clicking the upload button triggers only one upload
- Button shows disabled state immediately on first click
- On success or failure, button returns to normal state

---

### #1800: Eliminate TTS Desktop/Mobile Control Duplication

**Current state:** `Pages/Portal/TTS/Index.cshtml` renders voice controls twice — desktop (lines ~559-613) and mobile (lines ~483-556, inside a collapsible section). Bidirectional sync JS (lines ~687-742) keeps them in sync. The mobile version uses a plain `<select>` instead of the shared `_VoiceSelector` component.

**Files to modify:**
- `src/DiscordBot.Bot/Pages/Portal/TTS/Index.cshtml` — remove duplicate mobile controls, make desktop controls responsive

**Implementation:**
1. Remove the entire `mobile-collapsible-section` div containing duplicate voice/speed/pitch controls
2. Remove the `desktop-voice-controls` wrapper but keep its contents
3. Wrap the single set of voice controls in a container that is inline on desktop (≥768px) and collapses into an accordion on mobile (<768px) using CSS
4. Remove the bidirectional sync JavaScript (lines ~687-742)
5. Remove the `voiceSelectMobile` option list
6. Modify `toggleVoiceSettings()` to work with the single set of controls
7. Verify `portal-tts.js` still finds elements by original IDs (`speedSlider`, `pitchSlider`, `voiceSelect`)

**Acceptance criteria:**
- Voice, style, speed, and pitch controls appear exactly once in the DOM
- Desktop (≥768px): controls visible inline as before
- Mobile (<768px): controls in a collapsible accordion section
- All `portal-tts.js` functionality works identically
- No JavaScript console errors at any viewport size

---

### #1799: Extract Shared SSML Parsing Logic

**Current state:** `MARKER_RE` regex and parsing logic are duplicated in:
1. `Pages/Shared/Components/_EmphasisToolbar.cshtml` (line ~288) — used for "clear formatting"
2. `wwwroot/js/portal-tts.js` (line ~656) — used for building SSML elements

Both use: `/\*\*(.+?)\*\*(?!\*)|\*(?!\*)(.+?)\*(?!\*)|\[#\s(.+?)\s#\]|\[📅\s(.+?)\s📅\]|\[⏸️\s(\d+)ms\]/g`

**Files to create:**
- `src/DiscordBot.Bot/wwwroot/js/ssml-markers.js` — shared module

**Files to modify:**
- `src/DiscordBot.Bot/Pages/Shared/Components/_EmphasisToolbar.cshtml` — use `SsmlMarkers.stripMarkers()`
- `src/DiscordBot.Bot/wwwroot/js/portal-tts.js` — use `SsmlMarkers.parseMarkers()`

**Implementation:**
1. Create `ssml-markers.js` exposing on `window.SsmlMarkers`:
   - `SsmlMarkers.MARKER_RE` — the regex constant
   - `SsmlMarkers.parseMarkers(text)` — returns the elements array (currently built in portal-tts.js lines ~657-679)
   - `SsmlMarkers.stripMarkers(text)` — strips all markers, returning plain text
2. In `portal-tts.js`, replace inline `MARKER_RE` and parsing loop with `SsmlMarkers.parseMarkers(message)`
3. In `_EmphasisToolbar.cshtml`, replace inline `MARKER_RE` and `replace()` call with `SsmlMarkers.stripMarkers()`
4. Add `<script src="~/js/ssml-markers.js">` to TTS Portal before `portal-tts.js`
5. Note: pages using `_EmphasisToolbar` must include `ssml-markers.js`

**Acceptance criteria:**
- `MARKER_RE` regex defined in exactly one place
- Emphasis toolbar "clear" formatting still works
- Pro mode SSML building still works
- No regressions in preview or send functionality

---

### #1778: F16 — Self-Deletion for Sound Uploaders

**Current state:**
- `Sound` entity already has `UploadedById` (`ulong?`) — null for filesystem-discovered sounds, set for portal uploads
- `PortalSoundboardController` has no DELETE endpoint
- `ISoundboardOrchestrationService.DeleteSoundAsync(guildId, soundId)` already exists
- Portal sound cards have no delete affordance
- `GetSounds` response does not include `uploadedById`

**Backend files to modify:**
- `src/DiscordBot.Bot/Controllers/PortalSoundboardController.cs`:
  - Modify `UploadSound` to extract `discord:user_id` from claims and pass to orchestration
  - Add `DELETE /api/portal/soundboard/{guildId}/sounds/{soundId}` — validates `UploadedById` matches requesting user
  - Modify `GetSounds` response to include `uploadedById` (as string per snowflake ID rules)
- `src/DiscordBot.Bot/Interfaces/ISoundboardOrchestrationService.cs` — add optional `uploadedById` param to `UploadSoundAsync`
- `src/DiscordBot.Bot/Services/SoundboardOrchestrationService.cs` — pass `UploadedById` through to entity creation

**Frontend files to modify:**
- `src/DiscordBot.Bot/Pages/Portal/Soundboard/Index.cshtml` — delete button on cards where uploader matches current user; JS for delete confirmation + API call
- `src/DiscordBot.Bot/Pages/Portal/Soundboard/Index.cshtml.cs` — pass current user's Discord ID to page model

**Implementation:**
1. Add `ulong? uploadedById = null` parameter to `UploadSoundAsync` in interface + implementation
2. In `PortalSoundboardController.UploadSound`, extract user ID from claims and pass through
3. Add `DeleteSound` endpoint: verify sound exists, verify `sound.UploadedById == userId` (403 if not), delegate to orchestration
4. Include `uploadedById` (as string) in `GetSounds` response
5. Add `CurrentUserId` property to page model, pass to JS as `window.currentUserId = '@Model.CurrentUserId'`
6. Add delete button (trash icon) to sound cards, shown via JS when `uploadedById === currentUserId`
7. Add `deleteSound(soundId)` JS function with confirmation → DELETE API → remove card from DOM

**Acceptance criteria:**
- Portal uploads set `UploadedById` correctly
- Delete button appears only for the uploading user's sounds
- Confirmation prompt before deletion
- Card removed from grid without page reload
- 403 returned if user tries to delete someone else's sound
- Filesystem-discovered sounds (null `UploadedById`) cannot be deleted via portal
- SignalR `SoundDeleted` event fires for other connected users

---

### #1779: F17 — Soundboard Sort Options

**Current state:**
- Portal sorts sounds client-side: favorites first, then alphabetical by name (`sortSoundGrid()`)
- Admin Soundboard has server-side sorting with `name-asc`, `name-desc`, `newest`, `oldest` as a reference pattern
- Sound cards have `data-sound-id` and `data-sound-name` attributes; play count and duration are rendered in card HTML but not as data attributes

**Files to modify:**
- `src/DiscordBot.Bot/Pages/Portal/Soundboard/Index.cshtml` — add sort dropdown, data attributes, rewrite `sortSoundGrid()`
- `src/DiscordBot.Bot/Pages/Portal/Soundboard/Index.cshtml.cs` — include `UploadedAt` in sound mapping
- `src/DiscordBot.Bot/ViewModels/Portal/PortalSoundViewModel.cs` — add `DateTime UploadedAt` property

**Implementation:**
1. Add `UploadedAt` property to `PortalSoundViewModel`; include in page model mapping
2. Add `data-play-count` and `data-uploaded-at` attributes to sound cards
3. Add sort dropdown in soundboard header (between search and grid) with options: A-Z, Z-A, Most Played, Newest, Oldest
4. Rewrite `sortSoundGrid()` to use a `currentSort` variable (`name-asc`, `name-desc`, `most-played`, `newest`, `oldest`)
5. Favorites-first remains as a modifier on top of selected sort (not a separate option)
6. Persist sort preference in localStorage (`portal:soundboard:sort:{guildId}`)
7. Load and apply saved sort on page init
8. Update `addSoundToGrid()` to insert new sounds in correct sorted position

**Acceptance criteria:**
- Sort dropdown in soundboard header area
- Five sort options: A-Z, Z-A, Most Played, Newest, Oldest
- Favorites always appear before non-favorites within selected sort
- Sort preference persists across page reloads
- New sounds (upload or SignalR) inserted in correct sorted position
- Default sort is A-Z

---

### #1787: F24 — Custom TTS Preset Saving

**Current state:**
- Built-in presets: `_PresetBar.cshtml` renders 8 hardcoded presets from `PresetBarViewModel.Presets`
- `PresetButtonViewModel` has `Id, Name, Icon, VoiceName, Style, Speed, Pitch, Description`
- No database storage for presets; no custom preset API endpoints
- Pattern to follow: `UserSoundFavorite` entity + `IUserSoundFavoriteRepository`

**Files to create:**

| File | Layer | Description |
|------|-------|-------------|
| `src/DiscordBot.Core/Entities/UserTtsPreset.cs` | Core | Entity: Id, UserId, Name, VoiceName, Style?, Speed, Pitch, Icon?, CreatedAt, UpdatedAt? |
| `src/DiscordBot.Core/Interfaces/IUserTtsPresetRepository.cs` | Core | Repository interface |
| `src/DiscordBot.Infrastructure/Data/Repositories/UserTtsPresetRepository.cs` | Infra | EF Core implementation |
| SQLite migration | Infra | `Migrations/Sqlite/` |
| PostgreSQL migration | Infra | `Migrations/Postgresql/` |

**Files to modify:**

| File | Change |
|------|--------|
| `SqliteBotDbContext` / `PostgresBotDbContext` | Add `DbSet<UserTtsPreset>` |
| `ServiceCollectionExtensions.cs` | Register repository in DI |
| `PortalTtsController.cs` | Add CRUD endpoints for custom presets |
| `Pages/Portal/TTS/Index.cshtml` | Add "Save as Preset" UI in preset bar |
| `wwwroot/js/portal-tts.js` | Handle save/load/delete of custom presets |
| `Pages/Shared/Components/_PresetBar.cshtml` | Support rendering custom presets alongside built-in |
| `ViewModels/Components/PresetBarViewModel.cs` | Add custom presets support |

**API endpoints (all require `PortalGuildMember` policy):**
- `GET /api/portal/tts/presets/custom` — returns user's custom presets
- `POST /api/portal/tts/presets/custom` — creates preset (body: `{name, voiceName, style?, speed, pitch, icon?}`)
- `DELETE /api/portal/tts/presets/custom/{presetId}` — deletes preset (validates ownership)

**Constraints:**
- Max 20 presets per user
- Presets are per-user globally (not guild-scoped)
- Entity fields: `Id (int PK auto), UserId (ulong), Name (string max 50), VoiceName (string max 100), Style (string? max 50), Speed (decimal), Pitch (decimal), Icon (string? max 50), CreatedAt (DateTime), UpdatedAt (DateTime?)`

**Implementation:**
1. Create entity following `UserSoundFavorite` pattern
2. Create repository interface with `GetByUserIdAsync`, `GetByIdAsync`, `AddAsync`, `DeleteAsync`
3. Implement repository, register in DI
4. Add DbSet to both contexts; generate migrations for both providers
5. Add API endpoints to `PortalTtsController` with 20-preset limit enforcement
6. Add "Save" button (+ icon) at end of preset bar
7. Custom presets rendered after built-in presets with visual distinction (user icon/badge)
8. Custom presets show delete action (X button) on hover
9. Load custom presets on page init via GET endpoint

**Acceptance criteria:**
- Users can save current TTS settings as a named preset (up to 20)
- Custom presets appear in preset bar alongside built-in presets
- Clicking a custom preset applies all saved settings
- Users can delete their custom presets
- Presets persist across sessions (database-stored)
- Presets are per-user (invisible to other users)
- EF migrations generated for both SQLite and PostgreSQL

---

## Cross-Issue Coordination

| Concern | Issues | Mitigation |
|---------|--------|------------|
| `PortalSoundViewModel` modifications | #1778, #1779 | Implement #1778 first (adds `UploadedById`), then #1779 (adds `UploadedAt`) |
| TTS Portal page modifications | #1800, #1799, #1787 | Execute in order: #1800 → #1799 → #1787 |
| Sound card data attributes | #1778, #1779 | Coordinate markup — #1778 adds `data-uploaded-by`, #1779 adds `data-play-count` and `data-uploaded-at` |

## Risk Areas

| Risk | Impact | Mitigation |
|------|--------|------------|
| #1800 breaks TTS on mobile | High | Test at multiple viewport widths; verify all `portal-tts.js` element lookups resolve |
| #1787 migration conflicts | Medium | Generate both SQLite and PostgreSQL migrations per CLAUDE.md; test both providers |
| #1778 authorization bypass | High | Server-side `UploadedById` check is mandatory, not just UI hiding |
| #1799 script load order | Medium | Ensure `ssml-markers.js` loads before `portal-tts.js` and `_EmphasisToolbar.cshtml` |
| #1779 sort stability with SignalR | Low | Use stable insert based on current sort, not just prepend |
