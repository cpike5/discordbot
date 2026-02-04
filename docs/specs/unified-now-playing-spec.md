# Unified Now Playing Component Specification

**Version:** 1.1
**Date:** 2026-02-03
**Status:** Design (Revised)

## Executive Summary

This specification defines a unified "Now Playing" component to replace three separate implementations across the Soundboard, TTS, and VOX portal pages. The solution extends the existing `_VoiceChannelPanel` component with a `ShowNowPlaying` property, allowing portal pages to use compact voice controls while also displaying Now Playing status.

## Problem Statement

### Current Implementations

**Soundboard Portal** (`Pages/Portal/Soundboard/Index.cshtml`):
- Uses `_VoiceChannelPanel` in Compact mode for voice controls
- Has **separate custom Now Playing section** because Compact mode hides Now Playing
- CSS Classes: `.now-playing-container`, `.now-playing-item`, `.now-playing-icon`, etc.
- Update Mechanism: SignalR via `voice-channel-panel.js`

**TTS Portal** (`Pages/Portal/TTS/Index.cshtml`):
- Uses `_VoiceChannelPanel` in Compact mode for voice controls
- Has **separate custom Now Playing section** because Compact mode hides Now Playing
- CSS Classes: `.tts-now-playing`, `.tts-now-playing-title`, etc.
- Update Mechanism: **Polling (3s interval)** via `portal-tts.js`

**VOX Portal** (`Pages/Portal/VOX/Index.cshtml`):
- Uses `_VoiceChannelPanel` in Compact mode for voice controls
- Has **separate custom Now Playing section** because Compact mode hides Now Playing
- CSS Classes: `.vox-now-playing`, `.now-playing-message`, etc.
- Update Mechanism: Server-rendered + SignalR updates

### Root Cause

The `_VoiceChannelPanel` component in Compact mode unconditionally hides the Now Playing section (lines 81-85):

```css
/* Hide Now Playing and Queue sections in compact mode */
.voice-panel-compact #now-playing-section,
.voice-panel-compact .voice-queue-section {
    display: none !important;
}
```

Portal pages need **both** compact voice controls AND Now Playing display, so each implements its own custom Now Playing section.

### Issues with Current State

1. **Code Duplication:** Three separate HTML structures, CSS rulesets, and update handlers
2. **Inconsistent UX:** Different styling, layouts, and interaction patterns
3. **Performance:** TTS uses inefficient polling instead of SignalR push
4. **Maintenance Burden:** Changes to Now Playing require updates in three places

## Architecture Decision

**Selected Approach:** Add `ShowNowPlaying` property to control Now Playing visibility independently of Compact mode.

### Rationale

1. **Minimal Change:** Add one boolean property instead of introducing a new enum
2. **Makes Sense:** Portal pages get compact controls + Now Playing (what they actually need)
3. **Non-Breaking:** Existing `IsCompact` usage unchanged; new property is additive
4. **Leverage Existing Infrastructure:** Reuses existing Now Playing section and SignalR integration

### Rejected Alternative

**Option: Add `VoiceChannelPanelMode.NowPlayingOnly` enum value**
- Would show ONLY Now Playing, hiding voice controls
- Portal pages need BOTH voice controls AND Now Playing
- This mode would never actually be used by any page
- Adds unnecessary complexity

## Component Specification

### Enhanced `VoiceChannelPanelViewModel`

**File:** `src/DiscordBot.Bot/ViewModels/Components/VoiceChannelPanelViewModel.cs`

```csharp
public record VoiceChannelPanelViewModel
{
    // ... existing properties ...

    /// <summary>
    /// When true, renders the panel in compact sidebar mode.
    /// Stacks controls vertically, hides Queue section.
    /// </summary>
    public bool IsCompact { get; init; } = false;

    /// <summary>
    /// Controls visibility of the Now Playing section.
    /// Default: true (shown in Full mode), but Compact mode CSS previously hid it.
    /// Set explicitly to override Compact mode behavior.
    /// </summary>
    public bool ShowNowPlaying { get; init; } = true;

    /// <summary>
    /// When true, shows progress bar with position/duration.
    /// When false, shows "Playing..." text instead.
    /// Use false for content without known duration (TTS, VOX).
    /// </summary>
    public bool ShowProgress { get; init; } = true;
}
```

### Updated Compact Mode CSS

**File:** `src/DiscordBot.Bot/Pages/Shared/Components/_VoiceChannelPanel.cshtml`

**Change:** Remove Now Playing from the Compact mode hide rule:

```css
/* Before: Hide Now Playing and Queue in compact mode */
.voice-panel-compact #now-playing-section,
.voice-panel-compact .voice-queue-section {
    display: none !important;
}

/* After: Only hide Queue in compact mode; Now Playing controlled by ShowNowPlaying */
.voice-panel-compact .voice-queue-section {
    display: none !important;
}
```

**Add:** CSS class for hiding Now Playing when `ShowNowPlaying = false`:

```css
/* Hide Now Playing when ShowNowPlaying is false */
.voice-panel-hide-now-playing #now-playing-section {
    display: none !important;
}
```

### Updated Panel Class Logic

```cshtml
@{
    var panelClasses = new List<string>();
    if (Model.IsCompact) panelClasses.Add("voice-panel-compact");
    if (!Model.ShowNowPlaying) panelClasses.Add("voice-panel-hide-now-playing");
    var panelClass = string.Join(" ", panelClasses);
}
```

### Now Playing Section Updates

Add conditional rendering for progress vs "Playing..." text:

```cshtml
<div class="flex-1 min-w-0">
    <p id="now-playing-name" class="text-sm font-medium text-text-primary truncate"
       title="@(Model.NowPlaying?.Name ?? "Nothing playing")">
        @(Model.NowPlaying?.Name ?? "Nothing playing")
    </p>
    @if (Model.ShowProgress)
    {
        <div class="mt-1.5">
            <div class="w-full bg-bg-tertiary rounded-full h-1.5">
                <div id="now-playing-progress" class="bg-accent-blue h-1.5 rounded-full transition-all duration-300"
                     style="width: @(Model.NowPlaying?.ProgressPercent ?? 0)%"></div>
            </div>
            <div class="flex justify-between mt-1 text-xs text-text-tertiary">
                <span id="now-playing-position">@FormatDuration(Model.NowPlaying?.PositionSeconds ?? 0)</span>
                <span id="now-playing-duration">@FormatDuration(Model.NowPlaying?.DurationSeconds ?? 0)</span>
            </div>
        </div>
    }
    else
    {
        <div id="now-playing-status" class="text-xs text-text-secondary mt-1">Playing...</div>
    }
</div>
```

### JavaScript Updates

**File:** `src/DiscordBot.Bot/wwwroot/js/voice-channel-panel.js`

Update `updateNowPlaying()` to handle `ShowProgress` mode:

```javascript
function updateNowPlaying(nowPlaying) {
    if (!nowPlayingSection) return;

    if (nowPlaying) {
        nowPlayingSection.classList.remove('hidden');

        if (nowPlayingName) {
            nowPlayingName.textContent = nowPlaying.name;
            nowPlayingName.title = nowPlaying.name;
        }

        // Only update progress if progress elements exist
        if (nowPlayingProgress && nowPlayingPosition && nowPlayingDuration) {
            updatePlaybackProgress(nowPlaying.positionSeconds || 0, nowPlaying.durationSeconds || 0);
        }
        // Otherwise, "Playing..." text is already rendered server-side
    } else {
        nowPlayingSection.classList.add('hidden');
    }
}
```

## Migration Plan

### Phase 1: Extend `_VoiceChannelPanel` Component

**Changes:**
1. Add `ShowNowPlaying` property to `VoiceChannelPanelViewModel`
2. Add `ShowProgress` property to `VoiceChannelPanelViewModel`
3. Update Compact mode CSS to NOT hide Now Playing
4. Add `.voice-panel-hide-now-playing` CSS class
5. Update panel class logic to apply hide class when `ShowNowPlaying = false`
6. Update Now Playing section with progress/status conditional
7. Update JavaScript to handle ShowProgress mode

**Backwards Compatibility:**
- Existing usages with `IsCompact = true` will now show Now Playing (behavior change)
- If this is undesired, add `ShowNowPlaying = false` explicitly
- Admin pages (`Guilds/Soundboard`, `Guilds/TextToSpeech`) may need `ShowNowPlaying = false`

### Phase 2: Migrate Soundboard Portal

**File:** `src/DiscordBot.Bot/Pages/Portal/Soundboard/Index.cshtml`

**Before:** Custom Now Playing section + `_VoiceChannelPanel` with `IsCompact = true`

**After:** Single `_VoiceChannelPanel` call:
```cshtml
@await Html.PartialAsync("../../Shared/Components/_VoiceChannelPanel", new VoiceChannelPanelViewModel
{
    GuildId = Model.GuildId,
    IsCompact = true,
    ShowNowPlaying = true,
    ShowProgress = true,  // Soundboard has known duration
    IsConnected = Model.VoicePanel.IsConnected,
    ConnectedChannelId = Model.VoicePanel.ConnectedChannelId,
    ConnectedChannelName = Model.VoicePanel.ConnectedChannelName,
    AvailableChannels = Model.VoicePanel.AvailableChannels,
    NowPlaying = Model.VoicePanel.NowPlaying
})
```

**Cleanup:**
- Delete custom Now Playing HTML
- Delete custom Now Playing CSS (`.now-playing-container`, etc.)

**Estimated Reduction:** ~90 lines

### Phase 3: Migrate TTS Portal

**File:** `src/DiscordBot.Bot/Pages/Portal/TTS/Index.cshtml`

**After:**
```cshtml
@await Html.PartialAsync("../../Shared/Components/_VoiceChannelPanel", new VoiceChannelPanelViewModel
{
    GuildId = Model.GuildId,
    IsCompact = true,
    ShowNowPlaying = true,
    ShowProgress = false,  // TTS has no known duration
    IsConnected = Model.VoicePanel.IsConnected,
    ConnectedChannelId = Model.VoicePanel.ConnectedChannelId,
    ConnectedChannelName = Model.VoicePanel.ConnectedChannelName,
    AvailableChannels = Model.VoicePanel.AvailableChannels,
    NowPlaying = Model.NowPlaying
})
```

**Cleanup:**
- Delete custom Now Playing HTML/CSS
- Delete polling code from `portal-tts.js` (~150 lines)
- Delete `updateNowPlayingUI()`, `startStatusPolling()`, `pollStatus()`, `stopStatusPolling()`

**Estimated Reduction:** ~240 lines (90 HTML/CSS + 150 JS)

### Phase 4: Migrate VOX Portal

**File:** `src/DiscordBot.Bot/Pages/Portal/VOX/Index.cshtml`

**After:**
```cshtml
@await Html.PartialAsync("../../Shared/Components/_VoiceChannelPanel", new VoiceChannelPanelViewModel
{
    GuildId = Model.GuildId,
    IsCompact = true,
    ShowNowPlaying = true,
    ShowProgress = false,  // VOX has no known duration
    IsConnected = Model.VoicePanel.IsConnected,
    ConnectedChannelId = Model.VoicePanel.ConnectedChannelId,
    ConnectedChannelName = Model.VoicePanel.ConnectedChannelName,
    AvailableChannels = Model.VoicePanel.AvailableChannels,
    NowPlaying = string.IsNullOrEmpty(Model.NowPlayingMessage)
        ? null
        : new NowPlayingInfo { Name = Model.NowPlayingMessage }
})
```

**Cleanup:**
- Delete custom Now Playing HTML/CSS

**Estimated Reduction:** ~50 lines

### Phase 5: Backend Integration

Ensure page models populate `NowPlaying` for initial server-side rendering.

**TTS Portal:** Add `NowPlaying` property, populate from TTS service state.

**VOX Portal:** Already has `NowPlayingMessage`, just map to `NowPlayingInfo`.

**Soundboard Portal:** Likely already populates via `VoicePanel.NowPlaying`.

### Phase 6: Testing & Validation

**Functional Testing:**
- All three portals show Now Playing when audio is playing
- Progress bar works for Soundboard
- "Playing..." text works for TTS/VOX
- Stop button works across all portals
- SignalR updates work in real-time
- **TTS: NO polling requests** (verify in network tab)

**Cross-Portal Testing:**
- Multiple tabs show same Now Playing state via SignalR

**Backwards Compatibility:**
- Admin pages still work correctly
- Existing `IsCompact = true` usages reviewed

### Phase 7: Documentation Updates

- Update `docs/articles/component-api.md` with new properties
- Update portal-specific docs to reference unified component
- Create `docs/articles/unified-now-playing.md` architecture guide

## File Change Summary

| File | Lines Added | Lines Removed | Net Change |
|------|-------------|---------------|------------|
| `VoiceChannelPanelViewModel.cs` | +15 | 0 | +15 |
| `_VoiceChannelPanel.cshtml` | +25 | -5 | +20 |
| `voice-channel-panel.js` | +10 | -5 | +5 |
| `Portal/Soundboard/Index.cshtml` | +5 | -95 | -90 |
| `Portal/TTS/Index.cshtml` | +5 | -95 | -90 |
| `portal-tts.js` | 0 | -150 | -150 |
| `Portal/VOX/Index.cshtml` | +5 | -55 | -50 |
| **Total** | **+65** | **-405** | **-340** |

**Result:** Net reduction of ~340 lines, 63% reduction in Now Playing related code.

## Acceptance Criteria

### Must Have

1. **Single Component:** All three portals use `_VoiceChannelPanel` with `ShowNowPlaying = true`
2. **SignalR Everywhere:** TTS portal eliminates polling, uses SignalR push
3. **Code Reduction:** Minimum 300 lines removed across all three portals
4. **Visual Consistency:** Identical Now Playing layout/styling across all portals
5. **Functional Parity:** All existing features work (stop button, real-time updates, empty states)
6. **Progress Control:** `ShowProgress` correctly shows progress bar vs "Playing..." text
7. **Backwards Compatibility:** Existing admin pages continue to work

### Should Have

1. **Accessibility:** ARIA labels on stop button, proper focus management
2. **Performance:** No visual flicker during SignalR updates
3. **Responsive:** Now Playing adapts to sidebar widths
4. **Tooltips:** Full message text shown on hover for truncated names

---

**End of Specification**
