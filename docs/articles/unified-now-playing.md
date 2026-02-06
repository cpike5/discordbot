# Unified Now Playing Component

**Version:** 1.0
**Date:** 2026-02-05
**Status:** Implemented

---

## Overview

The Unified Now Playing component consolidates playback status display across all audio portals (Soundboard, TTS, VOX) into a single, reusable `_VoiceChannelPanel` partial view. Prior to this unification, three separate portal implementations existed with different CSS styling, different update mechanisms (SignalR vs polling), and approximately 430 lines of duplicated code.

This document describes the architecture, configuration, and migration path for the unified approach.

---

## Problem Statement

Before unification, three independent Now Playing implementations created maintenance challenges:

- **Code Duplication**: ~430 lines of duplicate HTML and CSS across three portals
- **Inconsistent Styling**: Different CSS class names and visual treatments per portal
- **Different Update Mechanisms**: TTS used 3-second polling; Soundboard and VOX used SignalR
- **TTS Performance Issue**: Polling-based updates created unnecessary network traffic
- **Single Point of Truth**: Changes to one portal's Now Playing didn't consistently apply to others
- **Testing Burden**: Each portal required separate testing of identical functionality

---

## Solution Architecture

The unified solution extends the existing `_VoiceChannelPanel` component with two new boolean properties that control visibility and behavior independently of the layout mode:

### Component Location

```
src/DiscordBot.Bot/Pages/Shared/Components/_VoiceChannelPanel.cshtml
```

### ViewModel

```
src/DiscordBot.Bot/ViewModels/Components/VoiceChannelPanelViewModel.cs
```

---

## Core Properties

### ShowNowPlaying

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ShowNowPlaying` | `bool` | `true` | Controls visibility of the Now Playing section independently of compact mode. When `false`, the entire Now Playing UI is hidden. When `true`, the section displays if content is currently playing. |

**Usage:**
- Set to `true` on all user portals (Soundboard, TTS, VOX)
- Set to `false` on admin pages where a separate Now Playing display exists (prevents duplication)

### ShowProgress

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ShowProgress` | `bool` | `true` | Determines the playback status display format. When `true`: shows progress bar with position/duration timestamps. When `false`: shows "Playing..." text. Use `false` for content without known duration (TTS, VOX). |

**Usage:**
- Set to `true` for content with known duration (Soundboard audio files)
- Set to `false` for content without duration information (TTS, VOX clips)

---

## CSS Architecture

### Compact Mode Styling

The `.voice-panel-compact` class controls layout for sidebar portals:

```css
.voice-panel-compact {
    border: none !important;
    background-color: transparent !important;
}

.voice-panel-compact .voice-queue-section {
    display: none !important;  /* Hide Queue in compact mode */
}
```

**Effect:** Reduces padding, removes borders, hides the Queue section. Now Playing remains visible unless explicitly hidden with `ShowNowPlaying = false`.

### Now Playing Visibility Control

The `.voice-panel-hide-now-playing` class controls Now Playing visibility:

```css
.voice-panel-hide-now-playing #now-playing-section {
    display: none !important;
}
```

**Applied when:** `ShowNowPlaying = false`

---

## Portal Configuration

### Portal Usage Matrix

| Portal | IsCompact | ShowNowPlaying | ShowProgress | Layout | Content Type | Update Mechanism |
|--------|-----------|----------------|--------------|--------|--------------|------------------|
| **Soundboard** | `false` | `true` | `true` | Full panel with queue | Audio files (known duration) | SignalR |
| **TTS** | `true` | `true` | `false` | Compact sidebar | Text-to-speech (no duration) | SignalR |
| **VOX** | `true` | `true` | `false` | Compact sidebar | Concatenated clips (no duration) | SignalR |
| **Admin Soundboard** | `false` | `false` | N/A | Admin page | Audio files | SignalR (separate display) |
| **Admin TTS** | `false` | `false` | N/A | Admin page | TTS messages | SignalR (separate display) |

---

## Implementation Examples

### Soundboard Portal (Full Panel with Progress)

**PageModel** (`Pages/Portal/Soundboard/Index.cshtml.cs`):

```csharp
VoicePanel = new VoiceChannelPanelViewModel
{
    GuildId = guildId,
    IsCompact = false,  // Full layout
    ShowNowPlaying = true,  // Default (play display on)
    ShowProgress = true,  // Default (show progress bar)
    IsConnected = isConnected,
    ConnectedChannelId = connectedChannelId,
    ConnectedChannelName = connectedChannelName,
    ChannelMemberCount = channelMemberCount,
    AvailableChannels = BuildVoiceChannelList(context.SocketGuild),
    NowPlaying = nowPlayingInfo,
    Queue = queueItems
};
```

**View** (`Pages/Portal/Soundboard/Index.cshtml`):

```html
@if (Model.VoicePanel != null)
{
    @await Html.PartialAsync("../../Shared/Components/_VoiceChannelPanel", Model.VoicePanel)
}
```

### TTS Portal (Compact Sidebar, No Progress)

**PageModel** (`Pages/Portal/TTS/Index.cshtml.cs`):

```csharp
VoicePanel = new VoiceChannelPanelViewModel
{
    GuildId = guildId,
    IsCompact = true,  // Compact sidebar
    ShowNowPlaying = true,  // Show Now Playing section
    ShowProgress = false,  // No progress bar (TTS has no duration)
    IsConnected = isConnected,
    ConnectedChannelId = connectedChannelId,
    ConnectedChannelName = connectedChannelName,
    ChannelMemberCount = channelMemberCount,
    AvailableChannels = BuildVoiceChannelList(context.SocketGuild),
    NowPlaying = nowPlayingInfo,
    Queue = queueItems
};
```

**Why ShowProgress = false:**
- TTS messages don't have a known duration upfront
- Azure Cognitive Services generates audio dynamically
- Real-time progress updates via SignalR provide better UX than a synthetic progress bar
- "Playing..." text communicates status without misleading duration information

### VOX Portal (Compact Sidebar, No Progress)

**PageModel** (`Pages/Portal/VOX/Index.cshtml.cs`):

```csharp
VoicePanel = new VoiceChannelPanelViewModel
{
    GuildId = guildId,
    IsCompact = true,  // Compact sidebar
    ShowNowPlaying = true,  // Show Now Playing section
    ShowProgress = false,  // No progress bar (concatenated clips, dynamic duration)
    IsConnected = isConnected,
    ConnectedChannelId = connectedChannelId,
    ConnectedChannelName = connectedChannelName,
    ChannelMemberCount = channelMemberCount,
    AvailableChannels = BuildVoiceChannelList(context.SocketGuild),
    NowPlaying = nowPlayingInfo,
    Queue = queueItems
};
```

**Why ShowProgress = false:**
- VOX clips are dynamically concatenated with variable duration
- Duration depends on user input and clip library content
- Progress tracking requires more complex FFmpeg integration
- Simple "Playing..." status provides adequate feedback

---

## JavaScript Integration

### Module Location

```
src/DiscordBot.Bot/wwwroot/js/voice-channel-panel.js
```

### SignalR Event Flow

The Voice Channel Panel JavaScript module handles real-time updates via SignalR. It listens for audio events on the `DashboardHub` and gracefully handles optional progress elements.

#### Event Handlers

| Event | Handler | Effect |
|-------|---------|--------|
| `PlaybackStarted` | `handlePlaybackStarted()` | Displays Now Playing section, updates name |
| `PlaybackProgress` | `handlePlaybackProgress()` | Updates progress bar and time display (if elements exist) |
| `PlaybackFinished` | `handlePlaybackFinished()` | Hides Now Playing section |
| `AudioConnected` | `handleAudioConnected()` | Updates connection status, shows Leave button |
| `AudioDisconnected` | `handleAudioDisconnected()` | Hides Now Playing, clears queue, updates status |
| `QueueUpdated` | `handleQueueUpdated()` | Refreshes queue display |

### Graceful Handling of Missing Progress Elements

The `updatePlaybackProgress()` function checks for progress elements before updating them:

```javascript
function updatePlaybackProgress(position, duration) {
    // Progress elements may not exist if ShowProgress = false
    if (nowPlayingProgress) {
        const percent = duration > 0 ? Math.round((position / duration) * 100) : 0;
        nowPlayingProgress.style.width = `${percent}%`;
    }

    if (nowPlayingPosition) {
        nowPlayingPosition.textContent = formatDuration(position);
    }

    if (nowPlayingDuration) {
        nowPlayingDuration.textContent = formatDuration(duration);
    }
}
```

**Key Behavior:**
- When `ShowProgress = true`: Progress bar elements exist, all three updates execute
- When `ShowProgress = false`: Progress bar elements don't render, only the null checks prevent errors
- Server-side "Playing..." text remains visible as fallback

### Initialization Flow

1. DOM loads, `_VoiceChannelPanel.cshtml` renders
2. JavaScript module initializes via `DOMContentLoaded` event
3. Module caches DOM element references (progress elements may be `null`)
4. Module connects to SignalR `DashboardHub`
5. Module joins guild-specific audio group
6. SignalR events trigger updates, which gracefully handle missing elements

---

## Initial State via Server-Side Rendering (SSR)

The component supports populating initial playback state server-side to avoid empty UI on page load:

```csharp
// In PageModel, populate NowPlaying if content is currently playing
NowPlaying = await _playbackService.GetCurrentlyPlayingAsync(guildId);
```

```html
<!-- Rendered in _VoiceChannelPanel.cshtml -->
<div id="now-playing-section" class="@(Model.NowPlaying == null ? "hidden" : "")">
    <p id="now-playing-name">@(Model.NowPlaying?.Name ?? "Nothing playing")</p>
    @if (Model.ShowProgress && Model.NowPlaying != null)
    {
        <div id="now-playing-progress" style="width: @(Model.NowPlaying.ProgressPercent)%"></div>
        <span id="now-playing-position">@FormatDuration(Model.NowPlaying.PositionSeconds)</span>
    }
    @if (!Model.ShowProgress)
    {
        <p>Playing...</p>
    }
</div>
```

**Benefit:** Users see current playback status immediately on page load, before SignalR updates arrive.

---

## Migration Guide

To add the unified Now Playing component to a new portal page:

### Step 1: Create ViewModel

In the PageModel, initialize `VoiceChannelPanelViewModel`:

```csharp
public VoiceChannelPanelViewModel? VoicePanel { get; set; }

public async Task OnGetAsync(ulong guildId)
{
    var context = await _botService.GetGuildContextAsync(guildId);
    var isConnected = await _audioService.IsConnectedAsync(guildId);
    var connectedChannelId = await _audioService.GetConnectedChannelIdAsync(guildId);
    var channelMemberCount = await _audioService.GetChannelMemberCountAsync(guildId);

    VoicePanel = new VoiceChannelPanelViewModel
    {
        GuildId = guildId,
        IsCompact = true,  // Adjust based on layout
        ShowNowPlaying = true,  // Usually true for portals
        ShowProgress = true,  // Adjust based on content type
        IsConnected = isConnected,
        ConnectedChannelId = connectedChannelId,
        ConnectedChannelName = await _audioService.GetChannelNameAsync(guildId),
        ChannelMemberCount = channelMemberCount,
        AvailableChannels = BuildVoiceChannelList(context.SocketGuild),
        NowPlaying = await _playbackService.GetCurrentlyPlayingAsync(guildId),
        Queue = await _queueService.GetQueueAsync(guildId)
    };
}
```

### Step 2: Render in View

```cshtml
@if (Model.VoicePanel != null)
{
    @await Html.PartialAsync("../../Shared/Components/_VoiceChannelPanel", Model.VoicePanel)
}
```

### Step 3: Choose Configuration

| Scenario | IsCompact | ShowProgress |
|----------|-----------|--------------|
| Full page with queue | `false` | `true` |
| Compact sidebar, known duration | `true` | `true` |
| Compact sidebar, no duration | `true` | `false` |
| Admin page (hide Now Playing) | any | N/A |

### Step 4: Ensure SignalR Integration

Verify that:
- `DashboardHub` is loaded and connected (typically in `_Layout.cshtml`)
- Audio events are subscribed to in the Signaling service
- `voice-channel-panel.js` is loaded after DOM

### Step 5: Delete Custom Code

Remove:
- Custom Now Playing HTML/CSS
- Custom JavaScript handlers for audio updates
- Polling-based update timers

---

## Benefits of Unification

### Code Reduction

- Eliminated ~340 lines of duplicated HTML/CSS
- Single source of truth for Now Playing display logic
- Centralized JavaScript event handling

### Performance Improvement

- **TTS Portal**: Migrated from 3-second polling to SignalR (real-time, reduced network overhead)
- **SignalR Efficiency**: Single event subscription handles all portals
- **DOM Efficiency**: Single component, shared CSS

### Consistency

- Identical visual treatment across all portals
- Standardized response to audio events
- Uniform configuration options

### Maintainability

- Changes to Now Playing display propagate to all portals
- Consolidated testing and bug fixes
- Clearer component responsibilities

---

## Data Flow Diagram

```
┌─────────────────────────────────────────┐
│  User Portal (Soundboard/TTS/VOX)       │
│                                         │
│  ┌───────────────────────────────────┐ │
│  │ _VoiceChannelPanel.cshtml         │ │
│  │ (IsCompact, ShowProgress)         │ │
│  │                                   │ │
│  │  ┌─────────────────────────────┐ │ │
│  │  │ Connection Status Section   │ │ │
│  │  │ Channel Selector            │ │ │
│  │  ├─────────────────────────────┤ │ │
│  │  │ Now Playing Section         │ │ │
│  │  │ (controlled by              │ │ │
│  │  │  ShowNowPlaying)            │ │ │
│  │  │                             │ │ │
│  │  │ [Name]                      │ │ │
│  │  │ [Progress Bar or "Playing"] │ │ │
│  │  │ (controlled by              │ │ │
│  │  │  ShowProgress)              │ │ │
│  │  ├─────────────────────────────┤ │ │
│  │  │ Queue Section               │ │ │
│  │  │ (hidden if IsCompact=true)  │ │ │
│  │  └─────────────────────────────┘ │ │
│  └───────────────────────────────────┘ │
│                  ▲                      │
│                  │                      │
│  ┌───────────────┴──────────────────┐  │
│  │ voice-channel-panel.js           │  │
│  │ - Handles DOM updates            │  │
│  │ - Manages SignalR events         │  │
│  │ - Gracefully handles missing     │  │
│  │   progress elements              │  │
│  └────────────┬─────────────────────┘  │
└───────────────┼────────────────────────┘
                │
         ┌──────▼──────┐
         │ DashboardHub│ (SignalR)
         │ (Real-time) │
         └──────┬──────┘
                │
         ┌──────▼──────────────┐
         │ Audio Service       │
         │ - PlaybackStarted   │
         │ - PlaybackProgress  │
         │ - PlaybackFinished  │
         │ - QueueUpdated      │
         │ - Audio Connected   │
         │ - Audio Disconnected│
         └─────────────────────┘
```

---

## Server-Side Rendering Considerations

### Initial State Population

On page load, the component displays initial state from server:

```csharp
// Fetch current playback state for this guild
var nowPlaying = await _playbackService.GetCurrentlyPlayingAsync(guildId);
var queue = await _queueService.GetQueueAsync(guildId);

VoicePanel = new VoiceChannelPanelViewModel
{
    // ... connection info ...
    NowPlaying = nowPlaying,  // Populated from database/cache
    Queue = queue              // Populated from queue service
};
```

### SignalR Sync

After initial render, SignalR events update the UI:

1. Page loads with server-populated state
2. JavaScript initializes and joins SignalR group
3. First `PlaybackProgress` event updates progress bar
4. Subsequent events keep UI in sync
5. If a user opens the portal and playback started 30 seconds ago, `PlaybackProgress` event corrects the position

---

## Testing Strategy

### Unit Tests

- ViewModel property validation (ShowNowPlaying, ShowProgress defaults)
- CSS class application based on configuration
- NowPlayingInfo calculation (ProgressPercent)

### Integration Tests

- Component renders with various configurations
- Progress bar only renders when ShowProgress=true
- "Playing..." text only renders when ShowProgress=false
- Queue section hidden when IsCompact=true
- Now Playing section hidden when ShowNowPlaying=false

### JavaScript Tests

- `updatePlaybackProgress()` handles missing elements gracefully
- SignalR events trigger correct DOM updates
- Event handlers filter by guild ID
- Initial state persists until first SignalR event

### E2E Tests

- Soundboard portal: Play sound, verify progress bar updates
- TTS portal: Speak message, verify "Playing..." text, no progress bar
- VOX portal: Play clip, verify display updates via SignalR
- Switch between portals, verify separate panel state per guild

---

## Performance Notes

### Before Unification

- TTS: 3-second polling (requests every 3 seconds × N active portals)
- Soundboard: SignalR (event-driven)
- VOX: SignalR (event-driven)
- DOM: 3 separate Now Playing implementations, 3 separate JavaScript modules

### After Unification

- All portals: SignalR (event-driven, real-time)
- DOM: 1 shared Now Playing implementation
- JavaScript: 1 module, shared across all portals
- Network: Eliminated TTS polling overhead

### Optimization Opportunities

1. **Throttle Progress Updates**: If high-frequency progress events occur, throttle DOM updates (currently not needed)
2. **Lazy Load Queue**: For large queues, paginate the display
3. **SSR Cache**: Cache initial NowPlaying state to reduce first paint latency

---

## See Also

- [Component API Usage Guide](component-api.md) - Comprehensive component library documentation
- [Soundboard Feature](soundboard.md) - Soundboard Portal with Now Playing integration
- [Text-to-Speech Support](tts-support.md) - TTS Portal integration details
- [VOX System Specification](vox-system-spec.md) - VOX Portal architecture
- [Design System](design-system.md) - Color palette and styling conventions
- [Interactive Components](interactive-components.md) - Discord component patterns and SignalR integration

---

## Changelog

### v1.0 (2026-02-05)

- Initial unification: Consolidated 3 portal Now Playing implementations into single component
- Added `ShowNowPlaying` property to independently control visibility
- Added `ShowProgress` property to toggle progress bar vs "Playing..." text
- Migrated TTS from polling to SignalR
- Reduced duplicated code by ~340 lines
- Updated Soundboard, TTS, and VOX portals to use unified component
