# Portal Page Quality Fixes — Implementation Plan

## Context

The three guild portal pages (Soundboard, TTS, VOX) have significant quality issues discovered during review: ~3,000 lines of duplicated/inline CSS, ~1,700 lines of inline JS, bypassed shared components, functional bugs, dead code, and hardcoded colors that break theming. These are public-facing pages served to guild members. This plan addresses all 8 recommendations from the review without changing any portal behavior or features.

---

## Phase 1: Extract Shared Landing Page CSS

**Goal:** Eliminate ~247 lines of identical `.landing-*` CSS duplicated between Soundboard and TTS.

**Create:** `wwwroot/css/portal-landing.css`
- Copy lines 7-254 from `Portal/Soundboard/Index.cshtml` (all `.landing-container`, `.landing-card`, `.landing-guild-icon`, `.landing-badge`, `.landing-title`, responsive `@media` queries)

**Modify:**
- `Pages/Portal/_PortalLayout.cshtml` — Add `<link rel="stylesheet" href="~/css/portal-landing.css" asp-append-version="true" />` after the `tab-panel.css` link
- `Portal/Soundboard/Index.cshtml` — Delete lines 7-254 (landing CSS block)
- `Portal/TTS/Index.cshtml` — Delete lines 8-254 (identical landing CSS block)

**Note:** VOX uses a completely different landing page structure (`.portal-landing` + feature cards grid) — not extractable with the others; left as-is.

---

## Phase 2: Extract Inline CSS/JS to External Files

**Goal:** Move remaining inline `<style>` and `<script>` blocks to external files for browser caching and maintainability.

### New files (5):

| File | Source | ~Lines |
|------|--------|--------|
| `wwwroot/css/portal-soundboard.css` | Soundboard `<style>` lines 255-1315 (post-Phase 1) | 1,060 |
| `wwwroot/js/portal-soundboard.js` | Soundboard `<script>` lines 1566-2391 | 823 |
| `wwwroot/css/portal-tts.css` | TTS `<style>` lines 255-942 (post-Phase 1) | 687 |
| `wwwroot/css/portal-vox.css` | VOX `@section Styles` lines 23-989 | 967 |
| `wwwroot/js/portal-vox.js` | VOX `@section Scripts` lines 1000-1825 | 825 |

TTS already has external `portal-tts.js` (648 lines) — only CSS needs extraction.

### Modify each page:

**Soundboard/Index.cshtml** — Replace inline blocks with:
```razor
@section Styles {
    <link rel="stylesheet" href="~/css/portal-soundboard.css" asp-append-version="true" />
}
@section Scripts {
    <script src="~/lib/signalr/signalr.min.js"></script>
    <script src="~/js/dashboard-hub.js"></script>
    <script src="~/js/voice-channel-panel.js"></script>
    <script>
        window.guildId = '@Model.GuildId';
        window.initialSoundCount = @Model.CurrentSoundCount;
        window.maxSounds = @Model.MaxSounds;
        window.maxFileSizeMB = @Model.MaxFileSizeMB;
    </script>
    <script src="~/js/portal-soundboard.js" asp-append-version="true"></script>
}
```

**TTS/Index.cshtml** — Replace inline `<style>` with CSS link; keep existing script section structure.

**VOX/Index.cshtml** — Replace inline `<style>` and `<script>` with external file links. Keep small inline `<script>` for `window.guildId = '@Model.GuildId'`.

---

## Phase 3: Adopt Shared Components

**Goal:** Replace hand-rolled HTML with existing `_Alert` and `_EmptyState` partials for accessibility and consistency.

### Soundboard/Index.cshtml:
- **Audio Disabled Warning** (~17 lines raw HTML) → `_Alert` partial with `AlertVariant.Warning`, Title="Audio Features Disabled". Gains `role="alert"` and `aria-live="polite"`.
- **Empty State** (~8 lines raw HTML) → `_EmptyState` partial with `EmptyStateType.NoResults`, wrapped in `id="emptyState"` div for JS visibility toggling.

### TTS/Index.cshtml:
- **Audio Disabled Warning** (~18 lines raw HTML) → Same `_Alert` replacement as Soundboard.

### VOX/Index.cshtml:
- Empty/error states are JS-injected for the SPA-like clip browser — leave as-is (correct for the pattern).

### Toast System:
- `PortalToast` is Soundboard-only and uses a different container/positioning than the shared `_ToastContainer`. **Do NOT replace** — just extract to external JS file (done in Phase 2). Remove the dead legacy `showToast` function (Phase 5).

---

## Phase 4: Bug Fixes

### Fix 4: Soundboard stale `currentSoundCount`
**File:** `wwwroot/js/portal-soundboard.js`
- Change `const currentSoundCount` → `let currentSoundCount = window.initialSoundCount`
- In `handleSoundUploaded`: add `currentSoundCount++`
- In `handleSoundDeleted`: add `currentSoundCount--`

### Fix 5: TTS `MaxMessageLength` data flow
**File:** `Portal/TTS/Index.cshtml.cs`
- In `OnGetAsync`, after `BuildSsmlComponentViewModels(settings)`: add `MaxMessageLength = settings.MaxMessageLength;`

**File:** `Portal/TTS/Index.cshtml`
- Replace 3 hardcoded `500` values with `@Model.MaxMessageLength`
- Add `window.maxMessageLength = @Model.MaxMessageLength;` to inline script

**File:** `wwwroot/js/portal-tts.js`
- Change `let maxMessageLength = 500` → `let maxMessageLength = window.maxMessageLength || 500`

### Fix 6: VOX event listener leak
**File:** `wwwroot/js/portal-vox.js`
- Add `const initializedGroups = new Set()` to state
- In `initializeVoxElements(group)`: always update DOM references (`voxEls.*`), but guard `addEventListener` calls with `if (initializedGroups.has(group)) return; initializedGroups.add(group);`

---

## Phase 5: Dead Code Removal & Design Token Adoption

### Dead Code:
**`portal-soundboard.js`:**
- Remove legacy `showToast` function (~24 lines, never called)
- Remove `createElement('style')` runtime injection of `@keyframes spin` — move to `portal-soundboard.css` as static CSS

**`portal-vox.js`:**
- Remove 3 `console.log` statements (development artifacts)

**`VOX/Index.cshtml.cs`:**
- `NowPlayingMessage = "VOX Message"` stub — leave as-is (removing it makes NowPlaying always null, which is worse UX; proper fix requires PlaybackService changes out of scope)

### Design Tokens:

**`portal-soundboard.css`:** Replace `#3b82f6` (9 occurrences) → `var(--color-accent-blue)`. Replace `rgba(59, 130, 246, *)` → `var(--color-accent-blue-muted)`. Replace `rgba(203, 78, 27, 0.3)` → use `var(--color-accent-orange)` with opacity.

**`portal-tts.js`:** Replace `style.color = '#ef4444'` / `'#fbbf24'` / `'#949ba4'` → toggle `.error` / `.warning` CSS classes on the `charCount` span (these classes already exist in the CSS with proper design tokens).

**`portal-vox.css`:** Replace `#10b981` → `var(--color-success)`, `#ef4444` → `var(--color-error)`.

---

## Files Summary

### New files (6):
| File | Purpose |
|------|---------|
| `wwwroot/css/portal-landing.css` | Shared landing page CSS (~247 lines) |
| `wwwroot/css/portal-soundboard.css` | Soundboard CSS (~1,060 lines) |
| `wwwroot/css/portal-tts.css` | TTS CSS (~687 lines) |
| `wwwroot/css/portal-vox.css` | VOX CSS (~967 lines) |
| `wwwroot/js/portal-soundboard.js` | Soundboard JS (~823 lines) |
| `wwwroot/js/portal-vox.js` | VOX JS (~825 lines) |

### Modified files (7):
| File | Changes |
|------|---------|
| `Pages/Portal/_PortalLayout.cshtml` | Add `portal-landing.css` link |
| `Pages/Portal/Soundboard/Index.cshtml` | Remove inline CSS/JS, add external refs, use `_Alert`/`_EmptyState` |
| `Pages/Portal/TTS/Index.cshtml` | Remove inline CSS, add external ref, use `_Alert`, fix `MaxMessageLength` |
| `Pages/Portal/TTS/Index.cshtml.cs` | Set `MaxMessageLength` from settings |
| `Pages/Portal/VOX/Index.cshtml` | Remove inline CSS/JS, add external refs |
| `wwwroot/js/portal-tts.js` | Read `window.maxMessageLength`, replace hardcoded colors with class toggles |
| (new files above) | Bug fixes, dead code removal, design tokens applied during creation |

All paths relative to `src/DiscordBot.Bot/`.

---

## Verification

1. **Build:** `dotnet build` — no compile errors
2. **Unauthenticated landing pages:** Visit each portal URL logged out — landing cards render identically to before
3. **Authenticated portal pages:** Visit each portal logged in as guild member — layout, functionality unchanged
4. **Soundboard upload:** Upload a sound, verify count increments; upload to max limit, verify blocked
5. **TTS character limit:** Verify the character counter shows the server-configured limit, not hardcoded 500
6. **VOX tab switching:** Switch between VOX/FVOX/HGRUNT tabs multiple times, verify no duplicate event handlers (type a message, check autocomplete fires once per keystroke)
7. **Theming:** Switch between dark and purple-dusk themes — verify all portal colors respond correctly (no hardcoded hex values remaining)
8. **Browser caching:** Check network tab — CSS/JS files load with versioned query strings (`?v=...`)
